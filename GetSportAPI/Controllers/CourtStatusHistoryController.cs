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
using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourtStatusHistoryController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validStatuses = { CourtStatus.Pending, CourtStatus.Approved, CourtStatus.Rejected, CourtStatus.Deleted };

        public CourtStatusHistoryController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromBody] CourtStatusHistoryCreateDto dto)
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

            if (!_validStatuses.Contains(dto.Statusofcourt))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected, Deleted." });
            }

            var court = await _context.Courts.FindAsync(dto.CourtId);
            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found." });
            }

            try
            {
                var history = new Courtstatushistory
                {
                    CourtId = dto.CourtId,
                    Statusofcourt = dto.Statusofcourt.Trim(),
                    Updateat = DateTime.UtcNow
                };

                _context.Courtstatushistories.Add(history);
                await _context.SaveChangesAsync();

                var responseData = new CourtStatusHistoryResponseDto
                {
                    StatusId = history.StatusId,
                    CourtId = history.CourtId,
                    Statusofcourt = history.Statusofcourt,
                    Updateat = history.Updateat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court status history created successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the court status history." });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] int? courtId = null)
        {
            var query = _context.Courtstatushistories.AsQueryable();

            if (courtId.HasValue)
            {
                query = query.Where(h => h.CourtId == courtId.Value);
            }

            var histories = await query
                .Include(h => h.Court)
                .ToListAsync();

            var responseData = histories.Select(history => new CourtStatusHistoryResponseDto
            {
                StatusId = history.StatusId,
                CourtId = history.CourtId,
                Statusofcourt = history.Statusofcourt,
                Updateat = history.Updateat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court status histories retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var history = await _context.Courtstatushistories
                .Include(h => h.Court)
                .FirstOrDefaultAsync(h => h.StatusId == id);

            if (history == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court status history not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == history.Court.OwnerId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this court status history." });
            }

            var responseData = new CourtStatusHistoryResponseDto
            {
                StatusId = history.StatusId,
                CourtId = history.CourtId,
                Statusofcourt = history.Statusofcourt,
                Updateat = history.Updateat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court status history retrieved successfully.", Data = responseData });
        }

        [HttpGet("court/{courtId}")]
        [Authorize]
        public async Task<ActionResult> GetByCourtId(int courtId)
        {
            var court = await _context.Courts.FindAsync(courtId);
            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == court.OwnerId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this court's status history." });
            }

            var histories = await _context.Courtstatushistories
                .Where(h => h.CourtId == courtId)
                .ToListAsync();

            var responseData = histories.Select(history => new CourtStatusHistoryResponseDto
            {
                StatusId = history.StatusId,
                CourtId = history.CourtId,
                Statusofcourt = history.Statusofcourt,
                Updateat = history.Updateat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court status histories retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Update(int id, [FromBody] CourtStatusHistoryUpdateDto dto)
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

            var history = await _context.Courtstatushistories.FirstOrDefaultAsync(h => h.StatusId == id);

            if (history == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court status history not found." });
            }

            if (!string.IsNullOrEmpty(dto.Statusofcourt) && !_validStatuses.Contains(dto.Statusofcourt))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Pending, Approved, Rejected, Deleted." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Statusofcourt))
                {
                    history.Statusofcourt = dto.Statusofcourt.Trim();
                    history.Updateat = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                var responseData = new CourtStatusHistoryResponseDto
                {
                    StatusId = history.StatusId,
                    CourtId = history.CourtId,
                    Statusofcourt = history.Statusofcourt,
                    Updateat = history.Updateat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court status history updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the court status history." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Delete(int id)
        {
            var history = await _context.Courtstatushistories.FirstOrDefaultAsync(h => h.StatusId == id);

            if (history == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court status history not found." });
            }

            try
            {
                _context.Courtstatushistories.Remove(history);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court status history deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the court status history." });
            }
        }
    }
}