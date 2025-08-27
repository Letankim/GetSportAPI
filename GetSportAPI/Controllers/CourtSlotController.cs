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
using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CourtSlotController : ControllerBase
    {
        private readonly GetSportContext _context;

        public CourtSlotController(GetSportContext context)
        {
            _context = context;
        }

        private (TimeSpan OpenTime, TimeSpan CloseTime) GetCourtOperatingHours(Court court)
        {
            // Use court's Startdate and Enddate time components if available, else default to 6:00 AM–10:00 PM
            var openTime = court.Startdate.HasValue ? court.Startdate.Value.TimeOfDay : new TimeSpan(6, 0, 0);
            var closeTime = court.Enddate.HasValue ? court.Enddate.Value.TimeOfDay : new TimeSpan(22, 0, 0);
            return (openTime, closeTime);
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromBody] CourtSlotCreateDto dto)
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

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == dto.CourtId && c.Status != CourtStatus.Deleted);

            if (court == null || !court.Isactive || court.Status != CourtStatus.Approved)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found, deleted, inactive, or not approved." });
            }

            if (dto.Starttime >= dto.Endtime)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start time must be before end time." });
            }

            double duration = (dto.Endtime - dto.Starttime).TotalMinutes;
            if (!Enum.IsDefined(typeof(SlotDuration), (int)duration))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot duration. Allowed durations are 30, 45, 60, 90, 120 minutes." });
            }

            // Check if slot time is within court's daily operating hours
            var (openTime, closeTime) = GetCourtOperatingHours(court);
            if (dto.Starttime.TimeOfDay < openTime || dto.Endtime.TimeOfDay > closeTime)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Slot times are outside the court's daily operating hours." });
            }

            var existingSlots = await _context.Courtslots
                .Where(s => s.CourtId == dto.CourtId && s.Starttime.Date == dto.Starttime.Date)
                .ToListAsync();

            bool isDuplicate = existingSlots.Any(s => s.Starttime == dto.Starttime && s.Endtime == dto.Endtime);
            if (isDuplicate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "A slot with the same start and end time already exists for this court on this date." });
            }

            bool hasOverlap = existingSlots.Any(s => dto.Starttime < s.Endtime && dto.Endtime > s.Starttime);
            if (hasOverlap)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Slot overlaps with an existing slot." });
            }

            try
            {
                var slot = new Courtslot
                {
                    CourtId = dto.CourtId,
                    Slotnumber = dto.Slotnumber,
                    Starttime = dto.Starttime,
                    Endtime = dto.Endtime,
                    Isavailable = dto.Isavailable
                };

                _context.Courtslots.Add(slot);
                await _context.SaveChangesAsync();

                var responseData = new CourtSlotResponseDto
                {
                    SlotId = slot.SlotId,
                    CourtId = slot.CourtId,
                    Slotnumber = slot.Slotnumber,
                    Starttime = slot.Starttime,
                    Endtime = slot.Endtime,
                    Isavailable = slot.Isavailable,
                    CourtLocation = court.Location ?? "Unknown",
                    OwnerId = court.OwnerId
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slot created successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating the court slot: {ex.Message}" });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot ID." });
            }

            var slot = await _context.Courtslots
                .Include(s => s.Court)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(s => s.SlotId == id);

            if (slot == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court slot not found." });
            }

            bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
            var court = slot.Court;
            if (!slot.Isavailable)
            {
                if (!isAuthenticated)
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this unavailable slot." });
                }
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim, out int accountId) || (court.OwnerId != accountId && User.FindFirstValue(ClaimTypes.Role) != UserRole.Admin && User.FindFirstValue(ClaimTypes.Role) != UserRole.Staff))
                {
                    return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this unavailable slot." });
                }
            }

            var responseData = new CourtSlotResponseDto
            {
                SlotId = slot.SlotId,
                CourtId = slot.CourtId,
                Slotnumber = slot.Slotnumber,
                Starttime = slot.Starttime,
                Endtime = slot.Endtime,
                Isavailable = slot.Isavailable,
                CourtLocation = court.Location ?? "Unknown",
                OwnerId = court.OwnerId            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slot retrieved successfully.", Data = responseData });
        }

        [HttpGet("court/{courtId}/date/{date}")]
        public async Task<ActionResult> GetSlotsByCourtAndDate(int courtId, DateTime date)
        {
            if (courtId <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == courtId && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or deleted." });
            }

            if (court.Status != CourtStatus.Approved || !court.Isactive)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Court is not approved or active." });
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
            var isOwner = accountId.HasValue && court.OwnerId == accountId.Value;

            var startOfDay = date.Date;
            var endOfDay = startOfDay.AddDays(1).AddTicks(-1);

            var slots = await _context.Courtslots
                .Where(s => s.CourtId == courtId && s.Starttime >= startOfDay && s.Starttime <= endOfDay)
                .OrderBy(s => s.Starttime)
                .ToListAsync();

            if (!slots.Any())
            {
                var (openTime, closeTime) = GetCourtOperatingHours(court);
                var openDateTime = startOfDay.Add(openTime);
                var closeDateTime = startOfDay.Add(closeTime);

                int slotNumber = 1;
                var currentTime = openDateTime;
                while (currentTime < closeDateTime)
                {
                    var slotEndTime = currentTime.AddMinutes(60); // Fixed 60-minute slots
                    if (slotEndTime > closeDateTime) break;

                    var newSlot = new Courtslot
                    {
                        CourtId = courtId,
                        Slotnumber = slotNumber++,
                        Starttime = currentTime,
                        Endtime = slotEndTime,
                        Isavailable = true
                    };

                    _context.Courtslots.Add(newSlot);
                    currentTime = slotEndTime;
                }

                await _context.SaveChangesAsync();
                slots = await _context.Courtslots
                    .Where(s => s.CourtId == courtId && s.Starttime >= startOfDay && s.Starttime <= endOfDay)
                    .OrderBy(s => s.Starttime)
                    .ToListAsync();
            }

            var query = slots.AsQueryable();
            if (!isAuthenticated)
            {
                query = query.Where(s => s.Isavailable);
            }
            else if (!isAdminOrStaff && !isOwner)
            {
                query = query.Where(s => s.Isavailable);
            }

            var responseData = query.Select(slot => new CourtSlotResponseDto
            {
                SlotId = slot.SlotId,
                CourtId = slot.CourtId,
                Slotnumber = slot.Slotnumber,
                Starttime = slot.Starttime,
                Endtime = slot.Endtime,
                Isavailable = slot.Isavailable,
                CourtLocation = court.Location ?? "Unknown",
                OwnerId = court.OwnerId            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slots retrieved successfully.", Data = responseData });
        }

        [HttpPost("bulk")]
        [Authorize]
        public async Task<ActionResult> CreateBulkSlots([FromBody] BulkCourtSlotCreateDto dto)
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

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == dto.CourtId);

            if (court == null || court.Status == CourtStatus.Deleted || !court.Isactive)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found, deleted, or inactive." });
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

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = userRole == UserRole.Admin;
            var isOwner = court.OwnerId == accountId;

            if (!isAdmin && !isOwner)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to create slots for this court." });
            }

            if (dto.StartDateTime >= dto.EndDateTime)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date-time must be before end date-time." });
            }

            if (!Enum.IsDefined(typeof(SlotDuration), dto.Duration))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot duration. Allowed durations are 30, 45, 60, 90, 120 minutes." });
            }

            var (openTime, closeTime) = GetCourtOperatingHours(court);
            var existingSlots = await _context.Courtslots
                .Where(s => s.CourtId == dto.CourtId && s.Starttime >= dto.StartDateTime && s.Starttime <= dto.EndDateTime)
                .ToListAsync();

            var newSlots = new List<Courtslot>();
            var currentTime = dto.StartDateTime;
            int slotNumber = existingSlots.Any() ? existingSlots.Max(s => s.Slotnumber) + 1 : 1;

            while (currentTime < dto.EndDateTime)
            {
                var slotEndTime = currentTime.AddMinutes(dto.Duration);
                if (slotEndTime > dto.EndDateTime) break;

                var slotDate = currentTime.Date;
                var slotTime = currentTime.TimeOfDay;

                // Skip if slot is outside daily operating hours
                if (slotTime < openTime || slotTime >= closeTime)
                {
                    currentTime = slotDate.AddDays(1).Add(openTime);
                    continue;
                }

                bool isDuplicate = existingSlots.Any(s => s.Starttime == currentTime && s.Endtime == slotEndTime);
                bool hasOverlap = existingSlots.Any(s => currentTime < s.Endtime && slotEndTime > s.Starttime);
                if (!isDuplicate && !hasOverlap)
                {
                    newSlots.Add(new Courtslot
                    {
                        CourtId = dto.CourtId,
                        Slotnumber = slotNumber++,
                        Starttime = currentTime,
                        Endtime = slotEndTime,
                        Isavailable = true
                    });
                }

                currentTime = slotEndTime;
            }

            try
            {
                _context.Courtslots.AddRange(newSlots);
                await _context.SaveChangesAsync();

                var responseData = newSlots.Select(slot => new CourtSlotResponseDto
                {
                    SlotId = slot.SlotId,
                    CourtId = slot.CourtId,
                    Slotnumber = slot.Slotnumber,
                    Starttime = slot.Starttime,
                    Endtime = slot.Endtime,
                    Isavailable = slot.Isavailable,
                    CourtLocation = court.Location ?? "Unknown",
                    OwnerId = court.OwnerId
                }).ToList();

                return Ok(new { StatusCode = 200, Status = "Success", Message = $"Created {newSlots.Count} court slots successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while creating bulk court slots: {ex.Message}" });
            }
        }

        [HttpGet("court/{courtId}")]
        public async Task<ActionResult> GetSlotsByCourt(int courtId)
        {
            if (courtId <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid court ID." });
            }

            var court = await _context.Courts
                .Include(c => c.Owner)
                .FirstOrDefaultAsync(c => c.CourtId == courtId && c.Status != CourtStatus.Deleted);

            if (court == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court not found or deleted." });
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
            var isOwner = accountId.HasValue && court.OwnerId == accountId.Value;

            var query = _context.Courtslots
                .Where(s => s.CourtId == courtId)
                .AsQueryable();

            if (!isAuthenticated)
            {
                query = query.Where(s => s.Isavailable);
            }
            else if (!isAdminOrStaff && !isOwner)
            {
                query = query.Where(s => s.Isavailable);
            }

            var slots = await query.OrderBy(s => s.Starttime).ToListAsync();

            var responseData = slots.Select(slot => new CourtSlotResponseDto
            {
                SlotId = slot.SlotId,
                CourtId = slot.CourtId,
                Slotnumber = slot.Slotnumber,
                Starttime = slot.Starttime,
                Endtime = slot.Endtime,
                Isavailable = slot.Isavailable,
                CourtLocation = court.Location ?? "Unknown",
                OwnerId = court.OwnerId
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slots retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Update(int id, [FromBody] CourtSlotUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot ID." });
            }

            var slot = await _context.Courtslots
                .Include(s => s.Court)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(s => s.SlotId == id);

            if (slot == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court slot not found." });
            }

            var court = slot.Court;
            if (court.Status == CourtStatus.Deleted || !court.Isactive || court.Status != CourtStatus.Approved)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Court is deleted, inactive, or not approved." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? accountId = null;
            if (userIdClaim != null && int.TryParse(userIdClaim, out int parsedId))
            {
                accountId = parsedId;
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = userRole == UserRole.Admin;
            var isStaff = userRole == UserRole.Staff;
            var isOwner = accountId.HasValue && court.OwnerId == accountId.Value;

            if (!isAdmin && !isStaff && !isOwner)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this slot." });
            }

            DateTime newStarttime = dto.Starttime ?? slot.Starttime;
            DateTime newEndtime = dto.Endtime ?? slot.Endtime;

            double duration = (newEndtime - newStarttime).TotalMinutes;
            if (!Enum.IsDefined(typeof(SlotDuration), (int)duration))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot duration. Allowed durations are 30, 45, 60, 90, 120 minutes." });
            }

            // Check if updated slot time is within court's daily operating hours
            var (openTime, closeTime) = GetCourtOperatingHours(court);
            if (newStarttime.TimeOfDay < openTime || newEndtime.TimeOfDay > closeTime)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Updated slot times are outside the court's daily operating hours." });
            }

            var existingSlots = await _context.Courtslots
                .Where(s => s.CourtId == slot.CourtId && s.SlotId != id && s.Starttime.Date == newStarttime.Date)
                .ToListAsync();

            bool isDuplicate = existingSlots.Any(s => s.Starttime == newStarttime && s.Endtime == newEndtime);
            if (isDuplicate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "A slot with the same start and end time already exists for this court on this date." });
            }

            bool hasOverlap = existingSlots.Any(s => newStarttime < s.Endtime && newEndtime > s.Starttime);
            if (hasOverlap)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Updated slot overlaps with an existing slot." });
            }

            try
            {
                if (dto.Slotnumber.HasValue) slot.Slotnumber = dto.Slotnumber.Value;
                slot.Starttime = newStarttime;
                slot.Endtime = newEndtime;
                if (dto.Isavailable.HasValue) slot.Isavailable = dto.Isavailable.Value;

                await _context.SaveChangesAsync();

                var responseData = new CourtSlotResponseDto
                {
                    SlotId = slot.SlotId,
                    CourtId = slot.CourtId,
                    Slotnumber = slot.Slotnumber,
                    Starttime = slot.Starttime,
                    Endtime = slot.Endtime,
                    Isavailable = slot.Isavailable,
                    CourtLocation = court.Location ?? "Unknown",
                    OwnerId = court.OwnerId
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slot updated successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while updating the court slot: {ex.Message}" });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot ID." });
            }

            var slot = await _context.Courtslots
                .Include(s => s.Court)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(s => s.SlotId == id);

            if (slot == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court slot not found." });
            }

            var court = slot.Court;
            if (court.Status == CourtStatus.Deleted || !court.Isactive || court.Status != CourtStatus.Approved)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Court is deleted, inactive, or not approved." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? accountId = null;
            if (userIdClaim != null && int.TryParse(userIdClaim, out int parsedId))
            {
                accountId = parsedId;
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = userRole == UserRole.Admin;
            var isStaff = userRole == UserRole.Staff;
            var isOwner = accountId.HasValue && court.OwnerId == accountId.Value;

            if (!isAdmin && !isStaff && !isOwner)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this slot." });
            }

            try
            {
                _context.Courtslots.Remove(slot);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slot deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while deleting the court slot: {ex.Message}" });
            }
        }

        [HttpPut("availability/{id}")]
        [Authorize]
        public async Task<ActionResult> UpdateAvailability(int id, [FromBody] bool isAvailable)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid slot ID." });
            }

            var slot = await _context.Courtslots
                .Include(s => s.Court)
                .ThenInclude(c => c.Owner)
                .FirstOrDefaultAsync(s => s.SlotId == id);

            if (slot == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Court slot not found." });
            }

            var court = slot.Court;
            if (court.Status == CourtStatus.Deleted || !court.Isactive || court.Status != CourtStatus.Approved)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Court is deleted, inactive, or not approved." });
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

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = userRole == UserRole.Admin;
            var isStaff = userRole == UserRole.Staff;
            var isOwner = court.OwnerId == accountId;

            if (!isAdmin && !isStaff && !isOwner)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this slot's availability." });
            }

            try
            {
                slot.Isavailable = isAvailable;
                await _context.SaveChangesAsync();

                var responseData = new CourtSlotResponseDto
                {
                    SlotId = slot.SlotId,
                    CourtId = slot.CourtId,
                    Slotnumber = slot.Slotnumber,
                    Starttime = slot.Starttime,
                    Endtime = slot.Endtime,
                    Isavailable = slot.Isavailable,
                    CourtLocation = court.Location ?? "Unknown",
                    OwnerId = court.OwnerId
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Court slot availability updated successfully.", Data = responseData });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = $"An error occurred while updating the court slot availability: {ex.Message}" });
            }
        }
    }
}