using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GetSportAPI.Models.Generated;
using GetSportAPI.DTO;
using GetSportAPI.Models.Enum;
using System.Security.Claims;
using Microsoft.AspNetCore.Hosting;
using System.ComponentModel.DataAnnotations;
using GetSportAPI.Utils;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourtController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly string[] _validStatuses = { CourtStatus.Pending, CourtStatus.Approved, CourtStatus.Rejected, CourtStatus.Deleted };

        public CourtController(GetSportContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromForm] CourtCreateDto dto)
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
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int ownerId;
            if (!int.TryParse(userIdClaim, out ownerId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            if (dto.Startdate.HasValue && dto.Enddate.HasValue && dto.Startdate > dto.Enddate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
            }

            try
            {
                ImageService imageService = new ImageService(_environment);
                string? imageUrl = await imageService.SaveImageAsync(dto.Image);

                var court = new Court
                {
                    OwnerId = ownerId,
                    Location = dto.Location?.Trim(),
                    Imageurl = imageUrl,
                    Priceperhour = dto.Priceperhour,
                    Status = CourtStatus.Pending,
                    Isactive = true,
                    Priority = dto.Priority,
                    Startdate = dto.Startdate,
                    Enddate = dto.Enddate
                };

                _context.Courts.Add(court);
                await _context.SaveChangesAsync();

                var history = new Courtstatushistory
                {
                    CourtId = court.CourtId,
                    Statusofcourt = court.Status,
                    Updateat = DateTime.UtcNow
                };
                _context.Courtstatushistories.Add(history);
                await _context.SaveChangesAsync();

                var owner = await _context.Accounts.FindAsync(ownerId);
                var responseData = new CourtResponseDto
                {
                    CourtId = court.CourtId,
                    OwnerId = court.OwnerId,
                    OwnerName = owner?.Fullname ?? "Unknown",
                    Location = court.Location,
                    Imageurl = court.Imageurl,
                    Priceperhour = court.Priceperhour,
                    Status = court.Status,
                    Isactive = court.Isactive,
                    Priority = court.Priority,
                    Startdate = court.Startdate,
                    Enddate = court.Enddate
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court created successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the court." });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            if (court.Status == CourtStatus.Pending)
            {
                if (!isAuthenticated)
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this pending court." });
                }
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId) || (court.OwnerId != accountId && User.FindFirstValue(ClaimTypes.Role) != UserRole.Admin && User.FindFirstValue(ClaimTypes.Role) != UserRole.Staff))
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this pending court." });
                }
            }

            var responseData = new CourtResponseDto
            {
                CourtId = court.CourtId,
                OwnerId = court.OwnerId,
                OwnerName = court.Owner?.Fullname ?? "Unknown",
                Location = court.Location,
                Imageurl = court.Imageurl,
                Priceperhour = court.Priceperhour,
                Status = court.Status,
                Isactive = court.Isactive,
                Priority = court.Priority,
                Startdate = court.Startdate,
                Enddate = court.Enddate
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court retrieved successfully.", Data = responseData });
        }

        [HttpGet]
        public async Task<ActionResult> GetAll([FromQuery] string? status = null)
        {
            if (!string.IsNullOrEmpty(status) && !_validStatuses.Contains(status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            int? accountId = null;
            string? userRole = null;
            if (isAuthenticated)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim, out int parsedId))
                {
                    accountId = parsedId;
                }
                userRole = User.FindFirstValue(ClaimTypes.Role);
            }

            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;

            var query = _context.Courts
                .Include(c => c.Owner)
                .Where(c => c.Status != CourtStatus.Deleted)
                .AsQueryable();

            if (!isAuthenticated)
            {
                query = query.Where(c => c.Status == CourtStatus.Approved && c.Isactive);
            }
            else
            {
                if (!isAdminOrStaff && accountId.HasValue)
                {
                    query = query.Where(c => c.Status == CourtStatus.Approved || c.Status == CourtStatus.Rejected || (c.Status == CourtStatus.Pending && c.OwnerId == accountId));
                }

                if (!string.IsNullOrEmpty(status))
                {
                    query = query.Where(c => c.Status == status);
                }
            }

            var courts = await query.ToListAsync();

            var responseData = courts.Select(court => new CourtResponseDto
            {
                CourtId = court.CourtId,
                OwnerId = court.OwnerId,
                OwnerName = court.Owner?.Fullname ?? "Unknown",
                Location = court.Location,
                Imageurl = court.Imageurl,
                Priceperhour = court.Priceperhour,
                Status = court.Status,
                Isactive = court.Isactive,
                Priority = court.Priority,
                Startdate = court.Startdate,
                Enddate = court.Enddate
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Courts retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = UserRole.Customer)]
        public async Task<ActionResult> GetMyCourts()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int ownerId;
            if (!int.TryParse(userIdClaim, out ownerId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            var courts = await _context.Courts
                .Include(c => c.Owner)
                .Where(c => c.OwnerId == ownerId && c.Status != CourtStatus.Deleted)
                .ToListAsync();

            var responseData = courts.Select(court => new CourtResponseDto
            {
                CourtId = court.CourtId,
                OwnerId = court.OwnerId,
                OwnerName = court.Owner?.Fullname ?? "Unknown",
                Location = court.Location,
                Imageurl = court.Imageurl,
                Priceperhour = court.Priceperhour,
                Status = court.Status,
                Isactive = court.Isactive,
                Priority = court.Priority,
                Startdate = court.Startdate,
                Enddate = court.Enddate
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "My courts retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromForm] CourtUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isOwner = court.OwnerId == accountId;
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this court." });
            }

            try
            {
                string oldStatus = court.Status ?? string.Empty;

                if (!string.IsNullOrEmpty(dto.Location)) court.Location = dto.Location.Trim();
                if (dto.Priceperhour.HasValue) court.Priceperhour = dto.Priceperhour.Value;
                if (dto.Priority.HasValue) court.Priority = dto.Priority.Value;
                if (dto.Startdate.HasValue) court.Startdate = dto.Startdate;
                if (dto.Enddate.HasValue) court.Enddate = dto.Enddate;
                if (dto.Image != null)
                {
                    ImageService imageService = new ImageService(_environment);
                    string? newImageUrl = await imageService.SaveImageAsync(dto.Image);
                    if (newImageUrl != null) court.Imageurl = newImageUrl;
                }
                if (!string.IsNullOrEmpty(dto.Status) && isAdminOrStaff)
                {
                    if (!_validStatuses.Contains(dto.Status) || dto.Status == CourtStatus.Deleted)
                    {
                        return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected." });
                    }
                    court.Status = dto.Status.Trim();
                }

                if (court.Startdate.HasValue && court.Enddate.HasValue && court.Startdate > court.Enddate)
                {
                    return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
                }

                if (court.Status != oldStatus)
                {
                    var history = new Courtstatushistory
                    {
                        CourtId = court.CourtId,
                        Statusofcourt = court.Status,
                        Updateat = DateTime.UtcNow
                    };
                    _context.Courtstatushistories.Add(history);
                }

                await _context.SaveChangesAsync();

                var responseData = new CourtResponseDto
                {
                    CourtId = court.CourtId,
                    OwnerId = court.OwnerId,
                    OwnerName = court.Owner?.Fullname ?? "Unknown",
                    Location = court.Location,
                    Imageurl = court.Imageurl,
                    Priceperhour = court.Priceperhour,
                    Status = court.Status,
                    Isactive = court.Isactive,
                    Priority = court.Priority,
                    Startdate = court.Startdate,
                    Enddate = court.Enddate
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court updated successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the court." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            int accountId;
            if (!int.TryParse(userIdClaim, out accountId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "Invalid user ID." });
            }

            var court = await _context.Courts
                .FirstOrDefaultAsync(c => c.CourtId == id && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or has been deleted." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isOwner = court.OwnerId == accountId;
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this court." });
            }

            try
            {
                string oldStatus = court.Status ?? string.Empty;
                court.Status = CourtStatus.Deleted;
                court.Isactive = false;

                var history = new Courtstatushistory
                {
                    CourtId = court.CourtId,
                    Statusofcourt = court.Status,
                    Updateat = DateTime.UtcNow
                };
                _context.Courtstatushistories.Add(history);

                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court marked as deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the court." });
            }
        }
    }
}