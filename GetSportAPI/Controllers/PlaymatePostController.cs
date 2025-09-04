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
    public class PlaymatePostController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validStatuses = { "Open", "Closed", "Cancelled" };

        public PlaymatePostController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> Create([FromBody] PlaymatePostCreateDto dto)
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

            if (!_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Open, Closed, Cancelled." });
            }

            var booking = await _context.Courtbookings
                .Include(b => b.Slot)
                .FirstOrDefaultAsync(b => b.BookingId == dto.CourtbookingId && b.UserId == userId && b.Status == "Confirmed");

            if (booking == null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid or unauthorized booking." });
            }

            // Check if the slot is still valid (at least 1 hour before start time)
            var slotStartTime = booking.Slot.Starttime;
            var currentTime = DateTime.UtcNow;
            if (slotStartTime <= currentTime.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Cannot create post for a booking that is expired or starts within 1 hour." });
            }

            try
            {
                var post = new Playmatepost
                {
                    UserId = userId,
                    CourtbookingId = dto.CourtbookingId,
                    Title = dto.Title.Trim(),
                    Content = dto.Content?.Trim(),
                    Neededplayers = dto.Neededplayers,
                    Skilllevel = dto.Skilllevel?.Trim(),
                    Status = dto.Status.Trim(),
                    Createdat = DateTime.UtcNow
                };

                _context.Playmateposts.Add(post);
                await _context.SaveChangesAsync();

                var user = await _context.Accounts.FindAsync(userId);
                var responseData = new PlaymatePostResponseDto
                {
                    PostId = post.PostId,
                    UserId = post.UserId,
                    UserName = user?.Fullname ?? "Unknown",
                    CourtbookingId = post.CourtbookingId,
                    Title = post.Title,
                    Content = post.Content,
                    Neededplayers = post.Neededplayers,
                    CurrentPlayers = 0, // No joins yet
                    Skilllevel = post.Skilllevel,
                    Status = post.Status,
                    Createdat = post.Createdat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post created successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the playmate post." });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] string? status = null)
        {
            if (!string.IsNullOrEmpty(status) && !_validStatuses.Contains(status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Open, Closed, Cancelled." });
            }

            var currentTime = DateTime.UtcNow;
            var query = _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Where(p => p.Courtbooking != null && p.Courtbooking.Status == "Confirmed" && p.Courtbooking.Slot.Starttime > currentTime.AddHours(1));

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(p => p.Status == status);
            }

            var posts = await query.ToListAsync();

            var responseData = posts.Select(post => new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins.Count,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate posts retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var post = await _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            if (post.Courtbooking == null || post.Courtbooking.Status != "Confirmed" || post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this playmate post." });
            }

            var responseData = new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins.Count,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyPosts()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var currentTime = DateTime.UtcNow;
            var posts = await _context.Playmateposts
                .Include(p => p.User)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Where(p => p.UserId == userId && p.Courtbooking != null && p.Courtbooking.Status == "Confirmed" && p.Courtbooking.Slot.Starttime > currentTime.AddHours(1))
                .ToListAsync();

            var responseData = posts.Select(post => new PlaymatePostResponseDto
            {
                PostId = post.PostId,
                UserId = post.UserId,
                UserName = post.User?.Fullname ?? "Unknown",
                CourtbookingId = post.CourtbookingId,
                Title = post.Title,
                Content = post.Content,
                Neededplayers = post.Neededplayers,
                CurrentPlayers = post.Playmatejoins.Count,
                Skilllevel = post.Skilllevel,
                Status = post.Status,
                Createdat = post.Createdat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your playmate posts retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromBody] PlaymatePostUpdateDto dto)
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

            var post = await _context.Playmateposts
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            if (post.Courtbooking == null || post.Courtbooking.Status != "Confirmed" || post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this playmate post." });
            }

            if (!string.IsNullOrEmpty(dto.Status) && !_validStatuses.Contains(dto.Status))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid status. Allowed values are: Open, Closed, Cancelled." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Title)) post.Title = dto.Title.Trim();
                if (!string.IsNullOrEmpty(dto.Content)) post.Content = dto.Content?.Trim();
                if (dto.Neededplayers.HasValue) post.Neededplayers = dto.Neededplayers.Value;
                if (!string.IsNullOrEmpty(dto.Skilllevel)) post.Skilllevel = dto.Skilllevel?.Trim();
                if (!string.IsNullOrEmpty(dto.Status)) post.Status = dto.Status.Trim();

                await _context.SaveChangesAsync();

                var user = await _context.Accounts.FindAsync(post.UserId);
                var responseData = new PlaymatePostResponseDto
                {
                    PostId = post.PostId,
                    UserId = post.UserId,
                    UserName = user?.Fullname ?? "Unknown",
                    CourtbookingId = post.CourtbookingId,
                    Title = post.Title,
                    Content = post.Content,
                    Neededplayers = post.Neededplayers,
                    CurrentPlayers = post.Playmatejoins.Count,
                    Skilllevel = post.Skilllevel,
                    Status = post.Status,
                    Createdat = post.Createdat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the playmate post." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            var post = await _context.Playmateposts.FirstOrDefaultAsync(p => p.PostId == id);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this playmate post." });
            }

            try
            {
                _context.Playmateposts.Remove(post);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate post deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the playmate post." });
            }
        }
    }
}