using GetSportAPI.Models.Enum;
using GetSportAPI.Models.Generated;
using GetSportAPI.Utils;
using Microsoft.AspNetCore.Authorization;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Net.payOS.Types;
using Microsoft.EntityFrameworkCore;
using GetSportAPI.DTO;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserPackageController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly PayOSService _payOSService;
        private readonly string[] _validStatuses = { "Pending", "Active", "Expired", "Cancelled" };

        public UserPackageController(GetSportContext context, PayOSService payOSService)
        {
            _context = context;
            _payOSService = payOSService;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> Create([FromBody] UserPackageCreateDto dto)
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

            var package = await _context.Packages.FindAsync(dto.PackageId);
            if (package == null || !package.Isactive)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Package not found or is inactive." });
            }

            if (dto.Startdate > dto.Enddate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
            }

            try
            {
                var userPackage = new Userpackage
                {
                    UserId = userId,
                    PackageId = dto.PackageId,
                    Startdate = dto.Startdate,
                    Enddate = dto.Enddate,
                    Isactive = true,
                    Createat = DateTime.UtcNow
                };

                _context.Userpackages.Add(userPackage);
                await _context.SaveChangesAsync();

                List<ItemData> items = new List<ItemData>
                {
                    new ItemData($"User Package: {package.Name}", 1, (int)package.Price)
                };

                string cancelUrl = $"https://example.com/userpackage/cancel?userPackageId={userPackage.UserpackageId}";
                string successUrl = $"https://example.com/userpackage/success?userPackageId={userPackage.UserpackageId}";

                var paymentResult = await _payOSService.CreatePaymentLink(
                    userPackage.UserpackageId,
                    package.Price,
                    $"Payment for User Package: {package.Name}",
                    items,
                    cancelUrl,
                    successUrl
                );

                var user = await _context.Accounts.FindAsync(userId);
                var responseData = new UserPackageResponseDto
                {
                    UserpackageId = userPackage.UserpackageId,
                    UserId = userPackage.UserId,
                    UserName = user?.Fullname ?? "Unknown",
                    PackageId = userPackage.PackageId,
                    PackageName = package.Name,
                    Startdate = userPackage.Startdate,
                    Enddate = userPackage.Enddate,
                    Isactive = userPackage.Isactive,
                    Createat = userPackage.Createat,
                    Updateat = userPackage.Updateat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "User package created successfully. Please complete payment.", Data = responseData, PaymentLink = paymentResult.checkoutUrl });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the user package: {ex.Message}" });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] bool? isActive = null)
        {
            var query = _context.Userpackages
                .Include(up => up.User)
                .Include(up => up.Package)
                .AsQueryable();

            if (isActive.HasValue)
            {
                query = query.Where(up => up.Isactive == isActive.Value);
            }

            var userPackages = await query.ToListAsync();

            var responseData = userPackages.Select(up => new UserPackageResponseDto
            {
                UserpackageId = up.UserpackageId,
                UserId = up.UserId,
                UserName = up.User?.Fullname ?? "Unknown",
                PackageId = up.PackageId,
                PackageName = up.Package?.Name ?? "Unknown",
                Startdate = up.Startdate,
                Enddate = up.Enddate,
                Isactive = up.Isactive,
                Createat = up.Createat,
                Updateat = up.Updateat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "User packages retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var userPackage = await _context.Userpackages
                .Include(up => up.User)
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserpackageId == id);

            if (userPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "User package not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == userPackage.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this user package." });
            }

            var responseData = new UserPackageResponseDto
            {
                UserpackageId = userPackage.UserpackageId,
                UserId = userPackage.UserId,
                UserName = userPackage.User?.Fullname ?? "Unknown",
                PackageId = userPackage.PackageId,
                PackageName = userPackage.Package?.Name ?? "Unknown",
                Startdate = userPackage.Startdate,
                Enddate = userPackage.Enddate,
                Isactive = userPackage.Isactive,
                Createat = userPackage.Createat,
                Updateat = userPackage.Updateat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "User package retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyUserPackages()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userPackages = await _context.Userpackages
                .Include(up => up.User)
                .Include(up => up.Package)
                .Where(up => up.UserId == userId)
                .ToListAsync();

            var responseData = userPackages.Select(up => new UserPackageResponseDto
            {
                UserpackageId = up.UserpackageId,
                UserId = up.UserId,
                UserName = up.User?.Fullname ?? "Unknown",
                PackageId = up.PackageId,
                PackageName = up.Package?.Name ?? "Unknown",
                Startdate = up.Startdate,
                Enddate = up.Enddate,
                Isactive = up.Isactive,
                Createat = up.Createat,
                Updateat = up.Updateat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your user packages retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Update(int id, [FromBody] UserPackageUpdateDto dto)
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

            var userPackage = await _context.Userpackages
                .Include(up => up.User)
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserpackageId == id);

            if (userPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "User package not found." });
            }

            if (!string.IsNullOrEmpty(dto.Status) && !_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Active, Expired, Cancelled." });
            }

            try
            {
                if (dto.Startdate.HasValue) userPackage.Startdate = dto.Startdate.Value;
                if (dto.Enddate.HasValue) userPackage.Enddate = dto.Enddate.Value;
                if (!string.IsNullOrEmpty(dto.Status))
                {
                    userPackage.Isactive = dto.Status == "Active";
                    userPackage.Updateat = DateTime.UtcNow;
                }

                if (userPackage.Startdate > userPackage.Enddate)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
                }

                await _context.SaveChangesAsync();

                var responseData = new UserPackageResponseDto
                {
                    UserpackageId = userPackage.UserpackageId,
                    UserId = userPackage.UserId,
                    UserName = userPackage.User?.Fullname ?? "Unknown",
                    PackageId = userPackage.PackageId,
                    PackageName = userPackage.Package?.Name ?? "Unknown",
                    Startdate = userPackage.Startdate,
                    Enddate = userPackage.Enddate,
                    Isactive = userPackage.Isactive,
                    Createat = userPackage.Createat,
                    Updateat = userPackage.Updateat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "User package updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the user package." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Delete(int id)
        {
            var userPackage = await _context.Userpackages.FirstOrDefaultAsync(up => up.UserpackageId == id);

            if (userPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "User package not found." });
            }

            try
            {
                _context.Userpackages.Remove(userPackage);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "User package deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the user package." });
            }
        }

        [HttpGet("{id}/payment-status")]
        [Authorize]
        public async Task<ActionResult> CheckPaymentStatus(int id, [FromQuery] string status)
        {
            var userPackage = await _context.Userpackages
                .Include(up => up.Package)
                .FirstOrDefaultAsync(up => up.UserpackageId == id);

            if (userPackage == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "User package not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == userPackage.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to check this user package's payment status." });
            }

            try
            {
                var paymentLinkInformation = await _payOSService.GetPaymentLinkInformation(userPackage.UserpackageId);

                if (paymentLinkInformation.status == "PAID")
                {
                    if (paymentLinkInformation.amountPaid != (int)userPackage.Package.Price || paymentLinkInformation.amountRemaining != 0)
                    {
                        return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Payment amount mismatch." });
                    }

                    userPackage.Isactive = true;
                    userPackage.Updateat = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return Ok(new { StatusCode = 200, Status = "Success", Message = "Payment confirmed. User package status updated to Active." });
                }
                else if (paymentLinkInformation.status == "CANCELLED")
                {
                    userPackage.Isactive = false;
                    userPackage.Updateat = DateTime.UtcNow;
                    await _context.SaveChangesAsync();

                    return Ok(new { StatusCode = 200, Status = "Success", Message = "Payment cancelled. User package status updated to Inactive." });
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
