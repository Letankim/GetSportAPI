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

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FeedbackController : ControllerBase
    {
        private readonly GetSportContext _context;

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

            var booking = await _context.Courtbookings.FirstOrDefaultAsync(b => b.BookingId == dto.BookingId && b.UserId == userId);
            if (booking == null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid booking or not owned by user." });
            }

            try
            {
                var feedback = new Feedback
                {
                    BookingId = dto.BookingId,
                    UserId = userId,
                    Rating = dto.Rating,
                    Comment = dto.Comment,
                    Createat = DateTime.UtcNow
                };

                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();

                var responseData = new FeedbackResponseDto
                {
                    FeedbackId = feedback.FeedbackId,
                    BookingId = feedback.BookingId,
                    UserId = feedback.UserId,
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    Createat = feedback.Createat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback created successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the feedback." });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll()
        {
            var feedbacks = await _context.Feedbacks.ToListAsync();

            var responseData = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.FeedbackId,
                BookingId = f.BookingId,
                UserId = f.UserId,
                Rating = f.Rating,
                Comment = f.Comment,
                Createat = f.Createat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedbacks retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
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
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this feedback." });
            }

            var responseData = new FeedbackResponseDto
            {
                FeedbackId = feedback.FeedbackId,
                BookingId = feedback.BookingId,
                UserId = feedback.UserId,
                Rating = feedback.Rating,
                Comment = feedback.Comment,
                Createat = feedback.Createat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult> GetMyFeedbacks()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var feedbacks = await _context.Feedbacks.Where(f => f.UserId == userId).ToListAsync();

            var responseData = feedbacks.Select(f => new FeedbackResponseDto
            {
                FeedbackId = f.FeedbackId,
                BookingId = f.BookingId,
                UserId = f.UserId,
                Rating = f.Rating,
                Comment = f.Comment,
                Createat = f.Createat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your feedbacks retrieved successfully.", Data = responseData });
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
                    UserId = feedback.UserId,
                    Rating = feedback.Rating,
                    Comment = feedback.Comment,
                    Createat = feedback.Createat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Feedback updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the feedback." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<ActionResult> Delete(int id)
        {
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
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the feedback." });
            }
        }
    }
}