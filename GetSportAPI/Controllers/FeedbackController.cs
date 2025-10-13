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
using GetSportAPI.Params;
using System.ComponentModel.DataAnnotations;
using GetSportAPI.Helpers;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validSortFields = { "Createat", "Rating", "UserName", "CourtLocation", "Bookingdate" };

        public FeedbackController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize]
        public async Task<ActionResult> Create([FromBody] FeedbackCreateDto dto)
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

            var booking = await _context.Courtbookings
                .Include(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.BookingId == dto.BookingId && b.UserId == userId && b.Status == "Done");

            if (booking == null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking, not owned by user, or not confirmed." });
            }

            var existingFeedback = await _context.Feedbacks
                .FirstOrDefaultAsync(f => f.BookingId == dto.BookingId && f.UserId == userId);
            if (existingFeedback != null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Feedback already exists for this booking." });
            }

            try
            {
                var feedback = new Feedback
                {
                    BookingId = dto.BookingId,
                    UserId = userId,
                    Rating = dto.Rating,
                    Comment = dto.Comment?.Trim(),
                    Createat = DateTime.UtcNow
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                var responseData = new FeedbackResponseDto
                {
                    FeedbackId = feedback.FeedbackId,
                    BookingId = feedback.BookingId,
                    Bookingdate = booking.Bookingdate,
                    CourtId = booking.CourtId,
                    CourtName = booking.Court.Owner.Fullname ?? "Unknown",
                    CourtLocation = booking.Court.Location ?? "Unknown",
                    CourtImageUrls = booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    UserId = feedback.UserId,
                    UserName = booking.User?.Fullname ?? "Unknown",
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    Createat = feedback.Createat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback created successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the feedback: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] FeedbackFilterParams filterParams)
        {
            var userInfo = UserHelper.GetUserInfo(User);

            if (userInfo.UserId == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
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

            if (filterParams.MinRating.HasValue && (filterParams.MinRating < 1 || filterParams.MinRating > 5))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum rating must be between 1 and 5." });
            }

            if (filterParams.MaxRating.HasValue && (filterParams.MaxRating < 1 || filterParams.MaxRating > 5))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Maximum rating must be between 1 and 5." });
            }

            if (filterParams.MinRating.HasValue && filterParams.MaxRating.HasValue && filterParams.MinRating > filterParams.MaxRating)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum rating cannot be greater than maximum rating." });
            }

            var query = _context.Feedbacks
                .Include(f => f.Booking)
                    .ThenInclude(b => b.Court)
                        .ThenInclude(c => c.Owner)
                .Include(f => f.User)
                .AsQueryable();

            if (userInfo.Role == UserRole.Staff)
            {
                query = query.Where(f => f.Booking.Court.OwnerId == userInfo.UserId);
            }

            if (filterParams.BookingId.HasValue)
            {
                query = query.Where(f => f.BookingId == filterParams.BookingId.Value);
            }

            if (filterParams.UserId.HasValue)
            {
                query = query.Where(f => f.UserId == filterParams.UserId.Value);
            }

            if (filterParams.MinRating.HasValue)
            {
                query = query.Where(f => f.Rating >= filterParams.MinRating.Value);
            }

            if (filterParams.MaxRating.HasValue)
            {
                query = query.Where(f => f.Rating <= filterParams.MaxRating.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(f => f.Createat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(f => f.Createat <= filterParams.EndCreateDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(f =>
                    (f.User.Fullname != null && f.User.Fullname.ToLower().Contains(searchLower)) ||
                    (f.Booking.Court.Location != null && f.Booking.Court.Location.ToLower().Contains(searchLower)));
            }

            var totalCount = await query.CountAsync();

            var sortBy = filterParams.SortBy?.ToLower() ?? "createat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "rating" => isDescending ? query.OrderByDescending(f => f.Rating) : query.OrderBy(f => f.Rating),
                "username" => isDescending ? query.OrderByDescending(f => f.User.Fullname) : query.OrderBy(f => f.User.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(f => f.Booking.Court.Location) : query.OrderBy(f => f.Booking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(f => f.Booking.Bookingdate) : query.OrderBy(f => f.Booking.Bookingdate),
                _ => isDescending ? query.OrderByDescending(f => f.Createat) : query.OrderBy(f => f.Createat)
            };

            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var feedbacks = await query.ToListAsync();

            var responseData = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.FeedbackId,
                BookingId = f.BookingId,
                Bookingdate = f.Booking.Bookingdate,
                CourtId = f.Booking.CourtId,
                CourtName = f.Booking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = f.Booking.Court.Location ?? "Unknown",
                CourtImageUrls = f.Booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                UserId = f.UserId,
                UserName = f.User?.Fullname ?? "Unknown",
                Rating = f.Rating,
                Comment = f.Comment,
                Createat = f.Createat
            }).ToList();

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
                Message = "Feedbacks retrieved successfully.",
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid feedback ID." });
            }

            var feedback = await _context.Feedbacks
                .Include(f => f.Booking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Feedback not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == feedback.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this feedback." });
            }

            var responseData = new FeedbackResponseDto
            {
                FeedbackId = feedback.FeedbackId,
                BookingId = feedback.BookingId,
                Bookingdate = feedback.Booking.Bookingdate,
                CourtId = feedback.Booking.CourtId,
                CourtName = feedback.Booking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = feedback.Booking.Court.Location ?? "Unknown",
                CourtImageUrls = feedback.Booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                UserId = feedback.UserId,
                UserName = feedback.User?.Fullname ?? "Unknown",
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                Createat = feedback.Createat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult> GetMyFeedbacks([FromQuery] FeedbackFilterParams filterParams)
        {
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

            if (filterParams.MinRating.HasValue && (filterParams.MinRating < 1 || filterParams.MinRating > 5))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum rating must be between 1 and 5." });
            }

            if (filterParams.MaxRating.HasValue && (filterParams.MaxRating < 1 || filterParams.MaxRating > 5))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Maximum rating must be between 1 and 5." });
            }

            if (filterParams.MinRating.HasValue && filterParams.MaxRating.HasValue && filterParams.MinRating > filterParams.MaxRating)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum rating cannot be greater than maximum rating." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var query = _context.Feedbacks
                .Include(f => f.Booking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(f => f.User)
                .Where(f => f.UserId == userId)
                .AsQueryable();

            // Apply filters
            if (filterParams.BookingId.HasValue)
            {
                query = query.Where(f => f.BookingId == filterParams.BookingId.Value);
            }

            if (filterParams.MinRating.HasValue)
            {
                query = query.Where(f => f.Rating >= filterParams.MinRating.Value);
            }

            if (filterParams.MaxRating.HasValue)
            {
                query = query.Where(f => f.Rating <= filterParams.MaxRating.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(f => f.Createat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(f => f.Createat <= filterParams.EndCreateDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(f => (f.User.Fullname != null && f.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (f.Booking.Court.Location != null && f.Booking.Court.Location.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "createat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "rating" => isDescending ? query.OrderByDescending(f => f.Rating) : query.OrderBy(f => f.Rating),
                "username" => isDescending ? query.OrderByDescending(f => f.User.Fullname) : query.OrderBy(f => f.User.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(f => f.Booking.Court.Location) : query.OrderBy(f => f.Booking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(f => f.Booking.Bookingdate) : query.OrderBy(f => f.Booking.Bookingdate),
                _ => isDescending ? query.OrderByDescending(f => f.Createat) : query.OrderBy(f => f.Createat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var feedbacks = await query.ToListAsync();

            var responseData = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.FeedbackId,
                BookingId = f.BookingId,
                Bookingdate = f.Booking.Bookingdate,
                CourtId = f.Booking.CourtId,
                CourtName = f.Booking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = f.Booking.Court.Location ?? "Unknown",
                CourtImageUrls = f.Booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                UserId = f.UserId,
                UserName = f.User?.Fullname ?? "Unknown",
                Rating = f.Rating,
                Comment = f.Comment,
                Createat = f.Createat
            }).ToList();

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
                Message = "Your feedbacks retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpGet("court/{courtId}")]
        [Authorize]
        public async Task<ActionResult> GetByCourt(int courtId, [FromQuery] FeedbackFilterParams filterParams)
        {
            if (courtId <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
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

            if (filterParams.MinRating.HasValue && (filterParams.MinRating < 1 || filterParams.MinRating > 5))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum rating must be between 1 and 5." });
            }

            if (filterParams.MaxRating.HasValue && (filterParams.MaxRating < 1 || filterParams.MaxRating > 5))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Maximum rating must be between 1 and 5." });
            }

            if (filterParams.MinRating.HasValue && filterParams.MaxRating.HasValue && filterParams.MinRating > filterParams.MaxRating)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Minimum rating cannot be greater than maximum rating." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == courtId && c.Status != CourtStatus.Deleted && c.Isactive && c.Status == CourtStatus.Approved);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found, deleted, inactive, or not approved." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var query = _context.Feedbacks
                .Include(f => f.Booking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(f => f.User)
                .Where(f => f.Booking.CourtId == courtId)
                .AsQueryable();

            // Apply filters
            if (filterParams.BookingId.HasValue)
            {
                query = query.Where(f => f.BookingId == filterParams.BookingId.Value);
            }

            if (filterParams.UserId.HasValue)
            {
                query = query.Where(f => f.UserId == filterParams.UserId.Value);
            }

            if (filterParams.MinRating.HasValue)
            {
                query = query.Where(f => f.Rating >= filterParams.MinRating.Value);
            }

            if (filterParams.MaxRating.HasValue)
            {
                query = query.Where(f => f.Rating <= filterParams.MaxRating.Value);
            }

            if (filterParams.StartCreateDate.HasValue)
            {
                query = query.Where(f => f.Createat >= filterParams.StartCreateDate.Value);
            }

            if (filterParams.EndCreateDate.HasValue)
            {
                query = query.Where(f => f.Createat <= filterParams.EndCreateDate.Value);
            }

            if (!string.IsNullOrEmpty(filterParams.Search))
            {
                var searchLower = filterParams.Search.ToLower();
                query = query.Where(f => (f.User.Fullname != null && f.User.Fullname.ToLower().Contains(searchLower)) ||
                                        (f.Booking.Court.Location != null && f.Booking.Court.Location.ToLower().Contains(searchLower)));
            }

            // Get total count for pagination
            var totalCount = await query.CountAsync();

            // Apply sorting
            var sortBy = filterParams.SortBy?.ToLower() ?? "createat";
            var isDescending = filterParams.SortOrder?.ToLower() == "desc";

            query = sortBy switch
            {
                "rating" => isDescending ? query.OrderByDescending(f => f.Rating) : query.OrderBy(f => f.Rating),
                "username" => isDescending ? query.OrderByDescending(f => f.User.Fullname) : query.OrderBy(f => f.User.Fullname),
                "courtlocation" => isDescending ? query.OrderByDescending(f => f.Booking.Court.Location) : query.OrderBy(f => f.Booking.Court.Location),
                "bookingdate" => isDescending ? query.OrderByDescending(f => f.Booking.Bookingdate) : query.OrderBy(f => f.Booking.Bookingdate),
                _ => isDescending ? query.OrderByDescending(f => f.Createat) : query.OrderBy(f => f.Createat)
            };

            // Apply pagination
            query = query
                .Skip((filterParams.Page - 1) * filterParams.PageSize)
                .Take(filterParams.PageSize);

            var feedbacks = await query.ToListAsync();

            var responseData = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.FeedbackId,
                BookingId = f.BookingId,
                Bookingdate = f.Booking.Bookingdate,
                CourtId = f.Booking.CourtId,
                CourtName = f.Booking.Court.Owner.Fullname ?? "Unknown",
                CourtLocation = f.Booking.Court.Location ?? "Unknown",
                CourtImageUrls = f.Booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                UserId = f.UserId,
                UserName = f.User?.Fullname ?? "Unknown",
                Rating = f.Rating,
                Comment = f.Comment,
                Createat = f.Createat
            }).ToList();

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
                Message = "Feedbacks for the court retrieved successfully.",
                Pagination = paginationMetadata,
                Data = responseData
            });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromBody] FeedbackUpdateDto dto)
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

            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid feedback ID." });
            }

            var feedback = await _context.Feedbacks
                .Include(f => f.Booking)
                .ThenInclude(b => b.Court)
                .ThenInclude(c => c.Owner)
                .Include(f => f.User)
                .FirstOrDefaultAsync(f => f.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Feedback not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == feedback.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this feedback." });
            }

            try
            {
                if (dto.Rating.HasValue) feedback.Rating = dto.Rating.Value;
                if (!string.IsNullOrEmpty(dto.Comment)) feedback.Comment = dto.Comment.Trim();

                await _context.SaveChangesAsync();

                var responseData = new FeedbackResponseDto
                {
                    FeedbackId = feedback.FeedbackId,
                    BookingId = feedback.BookingId,
                    Bookingdate = feedback.Booking.Bookingdate,
                    CourtId = feedback.Booking.CourtId,
                    CourtName = feedback.Booking.Court.Owner.Fullname ?? "Unknown",
                    CourtLocation = feedback.Booking.Court.Location ?? "Unknown",
                    CourtImageUrls = feedback.Booking.Court.Imageurl?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>(),
                    UserId = feedback.UserId,
                    UserName = feedback.User?.Fullname ?? "Unknown",
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    Createat = feedback.Createat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback updated successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while updating the feedback: {ex.Message}" });
            }
        }

        [HttpGet("court/{courtId}/average-rating")]
        public async Task<ActionResult> GetAverageRatingByCourt(int courtId)
        {
            if (courtId <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var court = await _context.Courts.FirstOrDefaultAsync(c => c.CourtId == courtId);
            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found." });
            }

            var feedbacksQuery = _context.Feedbacks
                .Include(f => f.Booking)
                .Where(f => f.Booking.CourtId == courtId);

            var totalFeedbacks = await feedbacksQuery.CountAsync();
            if (totalFeedbacks == 0)
            {
                return Ok(new
                {
                    StatusCode = 200,
                    Status = "Success",
                    Message = "No feedbacks available for this court.",
                    Data = new { AverageRating = 0.0, TotalFeedbacks = 0 }
                });
            }

            var averageRating = await feedbacksQuery.AverageAsync(f => (double)f.Rating);

            return Ok(new
            {
                StatusCode = 200,
                Status = "Success",
                Message = "Average rating retrieved successfully.",
                Data = new { AverageRating = Math.Round(averageRating, 2), TotalFeedbacks = totalFeedbacks }
            });
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid feedback ID." });
            }

            var feedback = await _context.Feedbacks.FirstOrDefaultAsync(f => f.FeedbackId == id);

            if (feedback == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Feedback not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == feedback.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this feedback." });
            }

            try
            {
                _context.Feedbacks.Remove(feedback);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while deleting the feedback: {ex.Message}" });
            }
        }
    }
}