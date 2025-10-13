using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using GetSportAPI.Models.Generated;
using GetSportAPI.Models.Enum;
using GetSportAPI.DTO;
using Net.payOS.Types;
using GetSportAPI.Utils;
using static GetSportAPI.Models.Enum.HostBookingUrl;
using GetSportAPI.Params;
using System.ComponentModel.DataAnnotations;
using GetSportAPI.Helpers;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourtBookingController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly PayOSService _payOSService;
        private readonly string[] _validStatuses = { "Pending", "Confirmed", "Cancelled" };
        private readonly string[] _validSortFields = { "Bookingdate", "Amount", "Createat", "UserName", "CourtLocation" };

        public CourtBookingController(GetSportContext context, PayOSService payOSService)
        {
            _context = context;
            _payOSService = payOSService;
        }

        // DTOs
        public class CourtBookingCreateDto
        {
            [Required(ErrorMessage = "Court ID is required.")]
            public int CourtId { get; set; }

            [Required(ErrorMessage = "Slot ID is required.")]
            public int SlotId { get; set; }

            [Required(ErrorMessage = "Booking date is required.")]
            public DateTime Bookingdate { get; set; }

            [Required(ErrorMessage = "Amount is required.")]
            public decimal Amount { get; set; }

            [StringLength(50, ErrorMessage = "Voucher code cannot exceed 50 characters.")]
            public string? VoucherCode { get; set; }
        }

        public class CourtBookingUpdateDto
        {
            [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
            public string? Status { get; set; }
        }

        public class CourtBookingResponseDto
        {
            public int BookingId { get; set; }
            public int UserId { get; set; }
            public string UserName { get; set; } = null!;
            public int CourtId { get; set; }
            public int? CourtOwnerId { get; set; }
            public string? CourtOwnerName { get; set; }
            public string? CourtLocation { get; set; }
            public List<string> CourtImageUrls { get; set; } = new List<string>();
            public decimal CourtPricePerHour { get; set; }
            public int SlotId { get; set; }
            public DateTime SlotStartTime { get; set; }
            public DateTime SlotEndTime { get; set; }
            public DateTime Bookingdate { get; set; }
            public string? Status { get; set; }
            public decimal Amount { get; set; }
            public DateTime Createat { get; set; }
            public string? VoucherCode { get; set; }
            public decimal? DiscountPercent { get; set; }
            public decimal? DiscountedAmount { get; set; }
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> Create([FromBody] CourtBookingCreateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = new Dictionary<string, string[]>();
                foreach (var state in ModelState)
                {
                    if (state.Value.Errors.Any())
                    {
                        errors[state.Key] = state.Value.Errors.Select(e => e.ErrorMessage).ToArray();
                    }
                }
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid input data.", Errors = errors });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var slot = await _context.Courtslots
                .FirstOrDefaultAsync(s => s.SlotId == dto.SlotId && s.CourtId == dto.CourtId);

            if (slot == null || !slot.Isavailable)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Slot not available." });
            }

            // Validate slot timing
            if (slot.Starttime < DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Cannot book a slot that starts within 1 hour or has already started." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == dto.CourtId && c.Status == CourtStatus.Approved && c.Isactive);
            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or not available." });
            }

            // Validate booking date matches slot date
            if (slot.Starttime.Date != dto.Bookingdate.Date)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Booking date must match the slot's date." });
            }

            var duration = (slot.Endtime - slot.Starttime).TotalHours;
            if (duration <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot duration." });
            }

            var calculatedAmount = (decimal)duration * court.Priceperhour;
            decimal finalAmount = calculatedAmount;
            Voucher? voucher = null;

            if (!string.IsNullOrEmpty(dto.VoucherCode))
            {
                voucher = await _context.Vouchers
                    .Include(v => v.Uservouchers)
                    .FirstOrDefaultAsync(v => v.Code == dto.VoucherCode && v.Isactive && v.Startdate <= DateTime.UtcNow && v.Enddate >= DateTime.UtcNow);

                if (voucher == null)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid or expired voucher code." });
                }

                if (voucher.Usagelimit.HasValue && voucher.Usage >= voucher.Usagelimit.Value)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Voucher usage limit reached." });
                }

                var userVoucher = await _context.Uservouchers
                    .FirstOrDefaultAsync(uv => uv.VoucherId == voucher.VoucherId && uv.UserId == userId && uv.Usedat == null);

                if (userVoucher == null)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Voucher not assigned to this user or already used." });
                }

                finalAmount = calculatedAmount * (1 - voucher.Discountpercent / 100);
            }

            if (Math.Abs(dto.Amount - finalAmount) > 0.01m)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid amount after applying voucher." });
            }

            try
            {
                var booking = new Courtbooking
                {
                    UserId = userId,
                    CourtId = dto.CourtId,
                    SlotId = dto.SlotId,
                    Bookingdate = dto.Bookingdate,
                    Status = "Pending",
                    Amount = finalAmount,
                    Createat = DateTime.UtcNow
                };

                _context.Courtbookings.Add(booking);
                slot.Isavailable = false;

                if (voucher != null)
                {
                    var userVoucher = await _context.Uservouchers
                        .FirstOrDefaultAsync(uv => uv.VoucherId == voucher.VoucherId && uv.UserId == userId && uv.Usedat == null);
                    if (userVoucher != null)
                    {
                        userVoucher.Usedat = DateTime.UtcNow;
                        voucher.Usage++;
                    }
                }

                await _context.SaveChangesAsync();

                List<ItemData> items = new List<ItemData>
                {
                    new ItemData($"Court Booking{(voucher != null ? " with Voucher" : "")}", 1, (int)booking.Amount)
                };

                string cancelUrl = HostBookingUrl.GetCancelUrl(HostEnvironment.Local, booking.BookingId);
                string successUrl = HostBookingUrl.GetSuccessUrl(HostEnvironment.Local, booking.BookingId);

                var paymentResult = await _payOSService.CreatePaymentLink(
                    booking.BookingId,
                    booking.Amount,
                    "Payment for Court Booking",
                    items,
                    cancelUrl,
                    successUrl
                );

                var user = await _context.Accounts.FindAsync(userId);
                var responseData = new CourtBookingResponseDto
                {
                    BookingId = booking.BookingId,
                    UserId = booking.UserId,
                    UserName = user?.Fullname ?? "Unknown",
                    CourtId = booking.CourtId,
                    CourtOwnerId = court.OwnerId,
                    CourtOwnerName = court.Owner?.Fullname ?? "Unknown",
                    CourtLocation = court.Location,
                    CourtImageUrls = court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    CourtPricePerHour = court.Priceperhour,
                    SlotId = booking.SlotId,
                    SlotStartTime = slot.Starttime,
                    SlotEndTime = slot.Endtime,
                    Bookingdate = booking.Bookingdate,
                    Status = booking.Status,
                    Amount = booking.Amount,
                    Createat = booking.Createat,
                    VoucherCode = voucher?.Code,
                    DiscountPercent = voucher?.Discountpercent,
                    DiscountedAmount = voucher != null ? calculatedAmount - finalAmount : null
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Booking created successfully. Please complete payment.", Data = responseData, PaymentLink = paymentResult.checkoutUrl });
            }
            catch (Exception ex)
            {
                slot.Isavailable = true;
                await _context.SaveChangesAsync();
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the booking: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] CourtBookingFilterParams filterParams)
        {
            var userInfo = UserHelper.GetUserInfo(User);

            if (userInfo.Role == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Confirmed, Cancelled." });
            }

            if (!string.IsNullOrEmpty(filterParams.SortBy) && !_validSortFields.Contains(filterParams.SortBy, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid sort field. Allowed values are: {string.Join(", ", _validSortFields)}." });
            }

            if (!string.IsNullOrEmpty(filterParams.SortOrder) && filterParams.SortOrder.ToLower() != "asc" && filterParams.SortOrder.ToLower() != "desc")
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid sort order. Allowed values are: asc, desc." });
            }

            if (filterParams.Page < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Page number must be greater than 0." });
            }

            if (filterParams.PageSize < 1 || filterParams.PageSize > 100)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Page size must be between 1 and 100." });
            }

            var query = _context.Courtbookings
                .Include(b => b.User)
                .Include(b => b.Slot)
                .Include(b => b.Court)
                .ThenInclude(c => c.Owner)
                .AsQueryable();

            if (userInfo.Role == UserRole.Staff)
            {
                query = query.Where(b => b.Court.OwnerId == userInfo.UserId);
            }

            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(b => b.Status == filterParams.Status);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(b => (b.User.Fullname != null && b.User.Fullname.ToLower().Contains(searchLower)) ||
                                         (b.Court.Location != null && b.Court.Location.ToLower().Contains(searchLower)));
            }

            if (filterParams.MinAmount.HasValue)
            {
                query = query.Where(b => b.Amount >= filterParams.MinAmount.Value);
            }

            if (filterParams.MaxAmount.HasValue)
            {
                query = query.Where(b => b.Amount <= filterParams.MaxAmount.Value);
            }

            if (filterParams.StartBookingDate.HasValue)
            {
                query = query.Where(b => b.Bookingdate >= filterParams.StartBookingDate.Value);
            }

            if (filterParams.EndBookingDate.HasValue)
            {
                query = query.Where(b => b.Bookingdate <= filterParams.EndBookingDate.Value);
            }

            var totalCount = await query.CountAsync();

            var sortBy = filterParams.SortBy?.ToLower() ?? "bookingdate";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "amount" => isDescending ? query.OrderByDescending(b => b.Amount) : query.OrderBy(b => b.Amount),
                "createat" => isDescending ? query.OrderByDescending(b => b.Createat) : query.OrderBy(b => b.Createat),
                "username" => isDescending ? query.OrderByDescending(b => b.User.Fullname) : query.OrderBy(b => b.User.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(b => b.Court.Location) : query.OrderBy(b => b.Court.Location),
                _ => isDescending ? query.OrderByDescending(b => b.Bookingdate) : query.OrderBy(b => b.Bookingdate)
            };

            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var bookings = await query.ToListAsync();

            var responseData = new List<CourtBookingResponseDto>();

            foreach (var booking in bookings)
            {
                var userVoucher = await _context.Uservouchers
                    .Include(uv => uv.Voucher)
                    .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.Usedat == booking.Createat);

                var duration = (booking.Slot.Endtime - booking.Slot.Starttime).TotalHours;

                responseData.Add(new CourtBookingResponseDto
                {
                    BookingId = booking.BookingId,
                    UserId = booking.UserId,
                    UserName = booking.User?.Fullname ?? "Unknown",
                    CourtId = booking.CourtId,
                    CourtOwnerId = booking.Court.OwnerId,
                    CourtOwnerName = booking.Court.Owner?.Fullname ?? "Unknown",
                    CourtLocation = booking.Court.Location,
                    CourtImageUrls = booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    CourtPricePerHour = booking.Court.Priceperhour,
                    SlotId = booking.SlotId,
                    SlotStartTime = booking.Slot.Starttime,
                    SlotEndTime = booking.Slot.Endtime,
                    Bookingdate = booking.Bookingdate,
                    Status = booking.Status,
                    Amount = booking.Amount,
                    Createat = booking.Createat,
                    VoucherCode = userVoucher?.Voucher?.Code,
                    DiscountPercent = userVoucher?.Voucher?.Discountpercent,
                    DiscountedAmount = userVoucher != null && duration > 0
                        ? (decimal)duration * booking.Court.Priceperhour * (userVoucher.Voucher.Discountpercent / 100)
                        : null
                });
            }

            var paginationMetadata = new
            {
                TotalCount = totalCount,
                PageSize = filterParams.PageSize,
                CurrentPage = filterParams.Page,
                TotalPages = (int)Math.Ceiling((double)totalCount / filterParams.PageSize)
            };

            return Ok(new
            {
                StatusCode = 200,
                Status = "Success",
                Message = "Bookings retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }


        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking ID." });
            }

            var booking = await _context.Courtbookings
                .Include(b => b.User)
                .Include(b => b.Slot)
                .Include(b => b.Court)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Booking not found." });
            }

            var userInfo = UserHelper.GetUserInfo(User);
            if (userInfo.UserId == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var isAdmin = userInfo.Role == UserRole.Admin;
            var isOwner = booking.UserId == userInfo.UserId;
            var isCourtOwner = booking.Court.OwnerId == userInfo.UserId;

            if (!isAdmin && !isOwner && !(isCourtOwner))
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this booking." });
            }

            var userVoucher = await _context.Uservouchers
                .Include(uv => uv.Voucher)
                .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.Usedat == booking.Createat);
            var duration = (booking.Slot.Endtime - booking.Slot.Starttime).TotalHours;

            var responseData = new CourtBookingResponseDto
            {
                BookingId = booking.BookingId,
                UserId = booking.UserId,
                UserName = booking.User?.Fullname ?? "Unknown",
                CourtId = booking.CourtId,
                CourtOwnerId = booking.Court.OwnerId,
                CourtOwnerName = booking.Court.Owner?.Fullname ?? "Unknown",
                CourtLocation = booking.Court.Location,
                CourtImageUrls = booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                CourtPricePerHour = booking.Court.Priceperhour,
                SlotId = booking.SlotId,
                SlotStartTime = booking.Slot.Starttime,
                SlotEndTime = booking.Slot.Endtime,
                Bookingdate = booking.Bookingdate,
                Status = booking.Status,
                Amount = booking.Amount,
                Createat = booking.Createat,
                VoucherCode = userVoucher?.Voucher?.Code,
                DiscountPercent = userVoucher?.Voucher?.Discountpercent,
                DiscountedAmount = userVoucher != null && duration > 0
                    ? (decimal)duration * booking.Court.Priceperhour * (userVoucher.Voucher.Discountpercent / 100)
                    : null
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Booking retrieved successfully.", Data = responseData });
        }


        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyBookings([FromQuery] CourtBookingFilterParams filterParams)
        {
            if (!string.IsNullOrEmpty(filterParams.Status) && !_validStatuses.Contains(filterParams.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Confirmed, Cancelled." });
            }

            if (!string.IsNullOrEmpty(filterParams.SortBy) && !_validSortFields.Contains(filterParams.SortBy, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = $"Invalid sort field. Allowed values are: {string.Join(", ", _validSortFields)}." });
            }

            if (!string.IsNullOrEmpty(filterParams.SortOrder) && filterParams.SortOrder.ToLower() != "asc" && filterParams.SortOrder.ToLower() != "desc")
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid sort order. Allowed values are: asc, desc." });
            }

            if (filterParams.Page < 1)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Page number must be greater than 0." });
            }

            if (filterParams.PageSize < 1 || filterParams.PageSize > 100)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Page size must be between 1 and 100." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var query = _context.Courtbookings
                .Include(b => b.User)
                .Include(b => b.Slot)
                .Include(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Where(b => b.UserId == userId)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(filterParams.Status))
            {
                query = query.Where(b => b.Status == filterParams.Status);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(b => (b.User.Fullname != null && b.User.Fullname.ToLower().Contains(searchLower)) ||
                                         (b.Court.Location != null && b.Court.Location.ToLower().Contains(searchLower)));
            }

            if (filterParams.MinAmount.HasValue)
            {
                query = query.Where(b => b.Amount >= filterParams.MinAmount.Value);
            }

            if (filterParams.MaxAmount.HasValue)
            {
                query = query.Where(b => b.Amount <= filterParams.MaxAmount.Value);
            }

            if (filterParams.StartBookingDate.HasValue)
            {
                query = query.Where(b => b.Bookingdate >= filterParams.StartBookingDate.Value);
            }

            if (filterParams.EndBookingDate.HasValue)
            {
                query = query.Where(b => b.Bookingdate <= filterParams.EndBookingDate.Value);
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "bookingdate";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "amount" => isDescending ? query.OrderByDescending(b => b.Amount) : query.OrderBy(b => b.Amount),
                "createat" => isDescending ? query.OrderByDescending(b => b.Createat) : query.OrderBy(b => b.Createat),
                "username" => isDescending ? query.OrderByDescending(b => b.User.Fullname) : query.OrderBy(b => b.User.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(b => b.Court.Location) : query.OrderBy(b => b.Court.Location),
                _ => isDescending ? query.OrderByDescending(b => b.Bookingdate) : query.OrderBy(b => b.Bookingdate)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var bookings = await query.ToListAsync();

            var responseData = new List<CourtBookingResponseDto>();

            foreach (var booking in bookings)
            {
                var userVoucher = await _context.Uservouchers
                    .Include(uv => uv.Voucher)
                    .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.Usedat == booking.Createat);

                var duration = (booking.Slot.Endtime - booking.Slot.Starttime).TotalHours;

                responseData.Add(new CourtBookingResponseDto
                {
                    BookingId = booking.BookingId,
                    UserId = booking.UserId,
                    UserName = booking.User?.Fullname ?? "Unknown",
                    CourtId = booking.CourtId,
                    CourtOwnerId = booking.Court.OwnerId,
                    CourtOwnerName = booking.Court.Owner?.Fullname ?? "Unknown",
                    CourtLocation = booking.Court.Location,
                    CourtImageUrls = booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    CourtPricePerHour = booking.Court.Priceperhour,
                    SlotId = booking.SlotId,
                    SlotStartTime = booking.Slot.Starttime,
                    SlotEndTime = booking.Slot.Endtime,
                    Bookingdate = booking.Bookingdate,
                    Status = booking.Status,
                    Amount = booking.Amount,
                    Createat = booking.Createat,
                    VoucherCode = userVoucher?.Voucher?.Code,
                    DiscountPercent = userVoucher?.Voucher?.Discountpercent,
                    DiscountedAmount = userVoucher != null && duration > 0
                        ? (decimal)duration * booking.Court.Priceperhour * (userVoucher.Voucher.Discountpercent / 100)
                        : null
                });
            }

            var paginationMetadata = new
            {
                TotalCount = totalCount,
                PageSize = filterParams.PageSize,
                CurrentPage = filterParams.Page,
                TotalPages = (int)Math.Ceiling((double)totalCount / filterParams.PageSize)
            };

            return Ok(new
            {
                StatusCode = 200,
                Status = "Success",
                Message = "Your bookings retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromBody] CourtBookingUpdateDto dto)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value.Errors.Any())
                    .ToDictionary(k => k.Key, v => v.Value.Errors.Select(e => e.ErrorMessage).ToArray());
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid input data.", Errors = errors });
            }

            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking ID." });
            }

            var booking = await _context.Courtbookings
                .Include(b => b.User)
                .Include(b => b.Slot)
                .Include(b => b.Court)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Booking not found." });
            }

            var userInfo = UserHelper.GetUserInfo(User);
            if (userInfo.UserId == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var isAdmin = userInfo.Role == UserRole.Admin;
            var isOwner = booking.UserId == userInfo.UserId;
            var isCourtOwner = booking.Court.OwnerId == userInfo.UserId;

            if (!isAdmin && !isOwner && !(isCourtOwner))
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this booking." });
            }

            if (!string.IsNullOrEmpty(dto.Status) && !_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Confirmed, Cancelled." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Status))
                {
                    booking.Status = dto.Status.Trim();
                }

                await _context.SaveChangesAsync();

                var userVoucher = await _context.Uservouchers
                    .Include(uv => uv.Voucher)
                    .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.Usedat == booking.Createat);
                var duration = (booking.Slot.Endtime - booking.Slot.Starttime).TotalHours;

                var responseData = new CourtBookingResponseDto
                {
                    BookingId = booking.BookingId,
                    UserId = booking.UserId,
                    UserName = booking.User?.Fullname ?? "Unknown",
                    CourtId = booking.CourtId,
                    CourtOwnerId = booking.Court.OwnerId,
                    CourtOwnerName = booking.Court.Owner?.Fullname ?? "Unknown",
                    CourtLocation = booking.Court.Location,
                    CourtImageUrls = booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    CourtPricePerHour = booking.Court.Priceperhour,
                    SlotId = booking.SlotId,
                    SlotStartTime = booking.Slot.Starttime,
                    SlotEndTime = booking.Slot.Endtime,
                    Bookingdate = booking.Bookingdate,
                    Status = booking.Status,
                    Amount = booking.Amount,
                    Createat = booking.Createat,
                    VoucherCode = userVoucher?.Voucher?.Code,
                    DiscountPercent = userVoucher?.Voucher?.Discountpercent,
                    DiscountedAmount = userVoucher != null && duration > 0
                        ? (decimal)duration * booking.Court.Priceperhour * (userVoucher.Voucher.Discountpercent / 100)
                        : null
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Booking updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the booking." });
            }
        }


        [HttpGet("{id}/payment-status")]
        [Authorize]
        public async Task<ActionResult> CheckPaymentStatus(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking ID." });
            }

            var booking = await _context.Courtbookings
                .Include(b => b.Slot)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Booking not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == booking.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to check this booking's payment status." });
            }

            try
            {
                var paymentLinkInformation = await _payOSService.GetPaymentLinkInformation(booking.BookingId);

                if (paymentLinkInformation.status == "PAID")
                {
                    if (paymentLinkInformation.amountPaid != (int)booking.Amount || paymentLinkInformation.amountRemaining != 0)
                    {
                        return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Payment amount mismatch." });
                    }

                    booking.Status = "Confirmed";
                    await _context.SaveChangesAsync();

                    return Ok(new { StatusCode = 200, Status = "Success", Message = "Payment confirmed. Booking status updated to Confirmed." });
                }
                else if (paymentLinkInformation.status == "CANCELLED")
                {
                    booking.Status = "Cancelled";
                    var slot = await _context.Courtslots.FindAsync(booking.SlotId);
                    if (slot != null)
                    {
                        slot.Isavailable = true;
                    }
                    var userVoucher = await _context.Uservouchers
                        .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.Usedat == booking.Createat);
                    if (userVoucher != null)
                    {
                        var voucher = await _context.Vouchers.FindAsync(userVoucher.VoucherId);
                        if (voucher != null)
                        {
                            voucher.Usage--;
                            userVoucher.Usedat = null;
                        }
                    }
                    await _context.SaveChangesAsync();

                    return Ok(new { StatusCode = 200, Status = "CANCELLED", Message = "Payment cancelled. Booking status updated to Cancelled." });
                }
                else
                {
                    return Ok(new { StatusCode = 200, Status = "Fail", Message = $"Payment status: {paymentLinkInformation.status}" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while checking payment status: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking ID." });
            }

            var booking = await _context.Courtbookings
                .Include(b => b.Slot)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Booking not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == booking.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this booking." });
            }

            try
            {
                var slot = await _context.Courtslots.FindAsync(booking.SlotId);
                if (slot != null)
                {
                    slot.Isavailable = true;
                }

                var userVoucher = await _context.Uservouchers
                    .FirstOrDefaultAsync(uv => uv.UserId == booking.UserId && uv.Usedat == booking.Createat);
                if (userVoucher != null)
                {
                    var voucher = await _context.Vouchers.FindAsync(userVoucher.VoucherId);
                    if (voucher != null)
                    {
                        voucher.Usage--;
                        userVoucher.Usedat = null;
                    }
                }

                _context.Courtbookings.Remove(booking);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Booking deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the booking." });
            }
        }
    }
}