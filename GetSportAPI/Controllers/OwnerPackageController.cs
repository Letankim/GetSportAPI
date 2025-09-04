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
using Net.payOS;
using Net.payOS.Types;
using System.ComponentModel.DataAnnotations;
using GetSportAPI.Utils;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class OwnerPackageController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly PayOSService _payOSService;
        private readonly string[] _validStatuses = { "Pending", "Active", "Expired", "Cancelled" };

        public OwnerPackageController(GetSportContext context, PayOSService payOSService)
        {
            _context = context;
            _payOSService = payOSService;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromBody] OwnerPackageCreateDto dto)
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

            var owner = await _context.Accounts.FindAsync(dto.OwnerId);
            if (owner == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Owner not found." });
            }

            if (dto.Startdate > dto.Enddate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
            }

            try
            {
                var ownerPackage = new Ownerpackage
                {
                    OwnerId = dto.OwnerId,
                    Packagename = dto.Packagename.Trim(),
                    Duration = dto.Duration,
                    Startdate = dto.Startdate,
                    Enddate = dto.Enddate,
                    Price = dto.Price,
                    Status = "Pending",
                    Createat = DateTime.UtcNow,
                    Priority = dto.Priority
                };

                _context.Ownerpackages.Add(ownerPackage);
                await _context.SaveChangesAsync();

                List<ItemData> items = new List<ItemData>
                {
                    new ItemData($"Owner Package: {dto.Packagename}", 1, (int)dto.Price)
                };

                string cancelUrl = $"https://example.com/ownerpackage/cancel?ownerPackageId={ownerPackage.OwnerpackageId}";
                string successUrl = $"https://example.com/ownerpackage/success?ownerPackageId={ownerPackage.OwnerpackageId}";

                var paymentResult = await _payOSService.CreatePaymentLink(
                    ownerPackage.OwnerpackageId,
                    ownerPackage.Price,
                    $"Payment for Owner Package: {dto.Packagename}",
                    items,
                    cancelUrl,
                    successUrl
                );

                var responseData = new OwnerPackageResponseDto
                {
                    OwnerpackageId = ownerPackage.OwnerpackageId,
                    OwnerId = ownerPackage.OwnerId,
                    OwnerName = owner.Fullname ?? "Unknown",
                    Packagename = ownerPackage.Packagename,
                    Duration = ownerPackage.Duration,
                    Startdate = ownerPackage.Startdate,
                    Enddate = ownerPackage.Enddate,
                    Price = ownerPackage.Price,
                    Status = ownerPackage.Status,
                    Createat = ownerPackage.Createat,
                    Priority = ownerPackage.Priority
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Owner package created successfully. Please complete payment.", Data = responseData, PaymentLink = paymentResult.checkoutUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the owner package: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] string? status = null)
        {
            if (!string.IsNullOrEmpty(status) && !_validStatuses.Contains(status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Active, Expired, Cancelled." });
            }

            var query = _context.Ownerpackages
                .Include(op => op.Owner)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(op => op.Status == status);
            }

            var ownerPackages = await query.ToListAsync();

            var responseData = ownerPackages.Select(op => new OwnerPackageResponseDto
            {
                OwnerpackageId = op.OwnerpackageId,
                OwnerId = op.OwnerId,
                OwnerName = op.Owner?.Fullname ?? "Unknown",
                Packagename = op.Packagename,
                Duration = op.Duration,
                Startdate = op.Startdate,
                Enddate = op.Enddate,
                Price = op.Price,
                Status = op.Status,
                Createat = op.Createat,
                Priority = op.Priority
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Owner packages retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var ownerPackage = await _context.Ownerpackages
                .Include(op => op.Owner)
                .FirstOrDefaultAsync(op => op.OwnerpackageId == id);

            if (ownerPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Owner package not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == ownerPackage.OwnerId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this owner package." });
            }

            var responseData = new OwnerPackageResponseDto
            {
                OwnerpackageId = ownerPackage.OwnerpackageId,
                OwnerId = ownerPackage.OwnerId,
                OwnerName = ownerPackage.Owner?.Fullname ?? "Unknown",
                Packagename = ownerPackage.Packagename,
                Duration = ownerPackage.Duration,
                Startdate = ownerPackage.Startdate,
                Enddate = ownerPackage.Enddate,
                Price = ownerPackage.Price,
                Status = ownerPackage.Status,
                Createat = ownerPackage.Createat,
                Priority = ownerPackage.Priority
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Owner package retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult> GetMyOwnerPackages()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int ownerId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var ownerPackages = await _context.Ownerpackages
                .Include(op => op.Owner)
                .Where(op => op.OwnerId == ownerId)
                .ToListAsync();

            var responseData = ownerPackages.Select(op => new OwnerPackageResponseDto
            {
                OwnerpackageId = op.OwnerpackageId,
                OwnerId = op.OwnerId,
                OwnerName = op.Owner?.Fullname ?? "Unknown",
                Packagename = op.Packagename,
                Duration = op.Duration,
                Startdate = op.Startdate,
                Enddate = op.Enddate,
                Price = op.Price,
                Status = op.Status,
                Createat = op.Createat,
                Priority = op.Priority
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your owner packages retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Update(int id, [FromBody] OwnerPackageUpdateDto dto)
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

            var ownerPackage = await _context.Ownerpackages
                .Include(op => op.Owner)
                .FirstOrDefaultAsync(op => op.OwnerpackageId == id);

            if (ownerPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Owner package not found." });
            }

            if (!string.IsNullOrEmpty(dto.Status) && !_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Active, Expired, Cancelled." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Packagename)) ownerPackage.Packagename = dto.Packagename.Trim();
                if (dto.Duration.HasValue) ownerPackage.Duration = dto.Duration.Value;
                if (dto.Startdate.HasValue) ownerPackage.Startdate = dto.Startdate.Value;
                if (dto.Enddate.HasValue) ownerPackage.Enddate = dto.Enddate.Value;
                if (dto.Price.HasValue) ownerPackage.Price = dto.Price.Value;
                if (!string.IsNullOrEmpty(dto.Status)) ownerPackage.Status = dto.Status.Trim();
                if (dto.Priority.HasValue) ownerPackage.Priority = dto.Priority.Value;

                if (ownerPackage.Startdate > ownerPackage.Enddate)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
                }

                await _context.SaveChangesAsync();

                var responseData = new OwnerPackageResponseDto
                {
                    OwnerpackageId = ownerPackage.OwnerpackageId,
                    OwnerId = ownerPackage.OwnerId,
                    OwnerName = ownerPackage.Owner?.Fullname ?? "Unknown",
                    Packagename = ownerPackage.Packagename,
                    Duration = ownerPackage.Duration,
                    Startdate = ownerPackage.Startdate,
                    Enddate = ownerPackage.Enddate,
                    Price = ownerPackage.Price,
                    Status = ownerPackage.Status,
                    Createat = ownerPackage.Createat,
                    Priority = ownerPackage.Priority
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Owner package updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the owner package." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Delete(int id)
        {
            var ownerPackage = await _context.Ownerpackages.FirstOrDefaultAsync(op => op.OwnerpackageId == id);

            if (ownerPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Owner package not found." });
            }

            try
            {
                _context.Ownerpackages.Remove(ownerPackage);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Owner package deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the owner package." });
            }
        }

        [HttpGet("{id}/payment-status")]
        [Authorize]
        public async Task<ActionResult> CheckPaymentStatus(int id, [FromQuery] string status)
        {
            var ownerPackage = await _context.Ownerpackages.FirstOrDefaultAsync(op => op.OwnerpackageId == id);

            if (ownerPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Owner package not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == ownerPackage.OwnerId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to check this owner package's payment status." });
            }

            try
            {
                var paymentLinkInformation = await _payOSService.GetPaymentLinkInformation(ownerPackage.OwnerpackageId);

                if (paymentLinkInformation.status == "PAID")
                {
                    if (paymentLinkInformation.amountPaid != (int)ownerPackage.Price || paymentLinkInformation.amountRemaining != 0)
                    {
                        return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Payment amount mismatch." });
                    }

                    ownerPackage.Status = "Active";
                    await _context.SaveChangesAsync();

                    return Ok(new { StatusCode = 200, Status = "Success", Message = "Payment confirmed. Owner package status updated to Active." });
                }
                else if (paymentLinkInformation.status == "CANCELLED")
                {
                    ownerPackage.Status = "Cancelled";
                    await _context.SaveChangesAsync();

                    return Ok(new { StatusCode = 200, Status = "Success", Message = "Payment cancelled. Owner package status updated to Cancelled." });
                }
                else
                {
                    return Ok(new { StatusCode = 200, Status = "Success", Message = $"Payment status: {paymentLinkInformation.status}" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while checking payment status: {ex.Message}" });
            }
        }
    }
}
