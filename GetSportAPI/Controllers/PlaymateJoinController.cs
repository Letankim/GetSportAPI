using GetSportAPI.DTO;
using GetSportAPI.Models.Enum;
using GetSportAPI.Models.Generated;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlaymateJoinController : ControllerBase
    {
        private readonly GetSportContext _context;

        public PlaymateJoinController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> Create([FromBody] PlaymateJoinCreateDto dto)
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

            var post = await _context.Playmateposts
                .Include(p => p.Playmatejoins)
                .Include(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .FirstOrDefaultAsync(p => p.PostId == dto.PostId);

            if (post == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate post not found." });
            }

            if (post.Courtbooking == null || post.Courtbooking.Status != "Confirmed" || post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is associated with an invalid or expired booking." });
            }

            if (post.Status != "Open")
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post is not open for joining." });
            }

            var currentJoins = post.Playmatejoins.Count;
            if (currentJoins >= post.Neededplayers)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate post has reached its maximum number of players." });
            }

            if (post.UserId == userId)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "You cannot join your own playmate post." });
            }

            if (post.Playmatejoins.Any(j => j.UserId == userId))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "You have already joined this playmate post." });
            }

            try
            {
                var join = new Playmatejoin
                {
                    PostId = dto.PostId,
                    UserId = userId,
                    Joinedat = DateTime.UtcNow
                };

                _context.Playmatejoins.Add(join);
                await _context.SaveChangesAsync();

                // Check if the post is now full and update status to Closed if necessary
                if (post.Playmatejoins.Count + 1 >= post.Neededplayers)
                {
                    post.Status = "Closed";
                    await _context.SaveChangesAsync();
                }

                var user = await _context.Accounts.FindAsync(userId);
                var responseData = new PlaymateJoinResponseDto
                {
                    JoinId = join.JoinId,
                    PostId = join.PostId,
                    UserId = join.UserId,
                    UserName = user?.Fullname ?? "Unknown",
                    Joinedat = join.Joinedat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Successfully joined the playmate post.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while joining the playmate post." });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] int? postId = null)
        {
            var query = _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Where(j => j.Post.Courtbooking != null && j.Post.Courtbooking.Status == "Confirmed" && j.Post.Courtbooking.Slot.Starttime > DateTime.UtcNow.AddHours(1));

            if (postId.HasValue)
            {
                query = query.Where(j => j.PostId == postId.Value);
            }

            var joins = await query.ToListAsync();

            var responseData = joins.Select(join => new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate joins retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var join = await _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .FirstOrDefaultAsync(j => j.JoinId == id);

            if (join == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate join not found." });
            }

            if (join.Post.Courtbooking == null || join.Post.Courtbooking.Status != "Confirmed" || join.Post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate join is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == join.UserId || currentUserId == join.Post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this playmate join." });
            }

            var responseData = new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate join retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyJoins()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var joins = await _context.Playmatejoins
                .Include(j => j.User)
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .Where(j => j.UserId == userId && j.Post.Courtbooking != null && j.Post.Courtbooking.Status == "Confirmed" && j.Post.Courtbooking.Slot.Starttime > DateTime.UtcNow.AddHours(1))
                .ToListAsync();

            var responseData = joins.Select(join => new PlaymateJoinResponseDto
            {
                JoinId = join.JoinId,
                PostId = join.PostId,
                UserId = join.UserId,
                UserName = join.User?.Fullname ?? "Unknown",
                Joinedat = join.Joinedat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your playmate joins retrieved successfully.", Data = responseData });
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
            var join = await _context.Playmatejoins
                .Include(j => j.Post)
                .ThenInclude(p => p.Courtbooking)
                .ThenInclude(b => b.Slot)
                .FirstOrDefaultAsync(j => j.JoinId == id);

            if (join == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Playmate join not found." });
            }

            if (join.Post.Courtbooking == null || join.Post.Courtbooking.Status != "Confirmed" || join.Post.Courtbooking.Slot.Starttime <= DateTime.UtcNow.AddHours(1))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Playmate join is associated with an invalid or expired booking." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == join.UserId || currentUserId == join.Post.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to delete this playmate join." });
            }

            try
            {
                _context.Playmatejoins.Remove(join);

                if (join.Post.Status == "Closed" && join.Post.Playmatejoins.Count <= join.Post.Neededplayers)
                {
                    join.Post.Status = "Open";
                }

                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Playmate join deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the playmate join." });
            }
        }
    }}
