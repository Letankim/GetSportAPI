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
    public class PointTransactionController : ControllerBase
    {
        private readonly GetSportContext _context;

        public PointTransactionController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromBody] PointTransactionCreateDto dto)
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

            // For admin/staff to create transactions, assume userId from query or something, but for simplicity, require userId in DTO if needed
            // But model has UserId, so add to DTO if necessary. For now, assume it's for a specific user.

            return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Endpoint not fully implemented for creation." }); // Placeholder, as creation might be system-generated
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll()
        {
            var transactions = await _context.Pointtransactions.ToListAsync();

            var responseData = transactions.Select(t => new PointTransactionResponseDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                BookingId = t.BookingId,
                Pointchanged = t.Pointchanged,
                Transactiontype = t.Transactiontype,
                Description = t.Description,
                Createat = t.Createat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Point transactions retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            var transaction = await _context.Pointtransactions.FirstOrDefaultAsync(t => t.TransactionId == id);

            if (transaction == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Point transaction not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == transaction.UserId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this point transaction." });
            }

            var responseData = new PointTransactionResponseDto
            {
                TransactionId = transaction.TransactionId,
                UserId = transaction.UserId,
                BookingId = transaction.BookingId,
                Pointchanged = transaction.Pointchanged,
                Transactiontype = transaction.Transactiontype,
                Description = transaction.Description,
                Createat = transaction.Createat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Point transaction retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult> GetMyTransactions()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var transactions = await _context.Pointtransactions.Where(t => t.UserId == userId).ToListAsync();

            var responseData = transactions.Select(t => new PointTransactionResponseDto
            {
                TransactionId = t.TransactionId,
                UserId = t.UserId,
                BookingId = t.BookingId,
                Pointchanged = t.Pointchanged,
                Transactiontype = t.Transactiontype,
                Description = t.Description,
                Createat = t.Createat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your point transactions retrieved successfully.", Data = responseData });
        }

    }
}