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
using System.ComponentModel.DataAnnotations;
using GetSportAPI.DTO;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WalletController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly string[] _validTransactionTypes = { "Deposit", "Withdrawal" };

        public WalletController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Create([FromBody] WalletCreateDto dto)
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
                return BadRequest(new ApiResponse<WalletResponseDto>(400, "BadRequest", "Invalid input data.", errors: errors));
            }

            var user = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.Isactive);
            if (user == null)
            {
                return NotFound(new ApiResponse<WalletResponseDto>(404, "NotFound", "User not found or inactive."));
            }

            if (await _context.Wallets.AnyAsync(w => w.UserId == dto.UserId))
            {
                return BadRequest(new ApiResponse<WalletResponseDto>(400, "BadRequest", "User already has a wallet."));
            }

            try
            {
                var wallet = new Wallet
                {
                    UserId = dto.UserId,
                    Balance = dto.InitialBalance,
                    Createdat = DateTime.UtcNow,
                    Updatedat = DateTime.UtcNow
                };

                _context.Wallets.Add(wallet);
                if (dto.InitialBalance > 0)
                {
                    var transaction = new Wallettransaction
                    {
                        Wallet = wallet,
                        Amount = dto.InitialBalance,
                        Direction = 1, // Deposit
                        Type = "Deposit",
                        Createdat = DateTime.UtcNow,
                        Comment = "Initial deposit"
                    };
                    _context.Wallettransactions.Add(transaction);
                }

                await _context.SaveChangesAsync();

                var responseData = new WalletResponseDto
                {
                    WalletId = wallet.WalletId,
                    UserId = wallet.UserId,
                    UserName = user.Fullname ?? "Unknown",
                    Balance = wallet.Balance,
                    Createdat = wallet.Createdat,
                    Updatedat = wallet.Updatedat
                };

                return Ok(new ApiResponse<WalletResponseDto>(200, "Success", "Wallet created successfully.", null, responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<WalletResponseDto>(500, "InternalServerError", $"An error occurred while creating the wallet: {ex.Message}"));
            }
        }

        [HttpGet("{userId}")]
        public async Task<ActionResult> GetByUserId(int userId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new ApiResponse<WalletResponseDto>(401, "Unauthorized", "User not authenticated."));
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == userId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new ApiResponse<WalletResponseDto>(403, "Forbidden", "You are not authorized to view this wallet."));
            }

            var wallet = await _context.Wallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                return NotFound(new ApiResponse<WalletResponseDto>(404, "NotFound", "Wallet not found."));
            }

            try
            {
                var responseData = new WalletResponseDto
                {
                    WalletId = wallet.WalletId,
                    UserId = wallet.UserId,
                    UserName = wallet.User?.Fullname ?? "Unknown",
                    Balance = wallet.Balance,
                    Createdat = wallet.Createdat,
                    Updatedat = wallet.Updatedat
                };

                return Ok(new ApiResponse<WalletResponseDto>(200, "Success", "Wallet retrieved successfully.", null,responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<WalletResponseDto>(500, "InternalServerError", $"An error occurred while retrieving the wallet: {ex.Message}"));
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> GetAll([FromQuery] int? userId = null, [FromQuery] decimal? minBalance = null)
        {
            var query = _context.Wallets
                .Include(w => w.User)
                .AsQueryable();

            if (userId.HasValue)
            {
                query = query.Where(w => w.UserId == userId.Value);
            }

            if (minBalance.HasValue)
            {
                query = query.Where(w => w.Balance >= minBalance.Value);
            }

            try
            {
                var wallets = await query.ToListAsync();
                var responseData = wallets.Select(w => new WalletResponseDto
                {
                    WalletId = w.WalletId,
                    UserId = w.UserId,
                    UserName = w.User?.Fullname ?? "Unknown",
                    Balance = w.Balance,
                    Createdat = w.Createdat,
                    Updatedat = w.Updatedat
                }).ToList();

                return Ok(new ApiResponse<List<WalletResponseDto>>(200, "Success", "Wallets retrieved successfully.",null, responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<WalletResponseDto>>(500, "InternalServerError", $"An error occurred while retrieving wallets: {ex.Message}"));
            }
        }

        [HttpPost("{userId}/add-funds")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Customer}")]
        public async Task<ActionResult> AddFunds(int userId, [FromBody] WalletAddFundsDto dto)
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
                return BadRequest(new ApiResponse<WalletResponseDto>(400, "BadRequest", "Invalid input data.", errors: errors));
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new ApiResponse<WalletResponseDto>(401, "Unauthorized", "User not authenticated."));
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = userRole == UserRole.Admin;
            var isOwner = currentUserId == userId;

            if (!isOwner && !isAdmin)
            {
                return StatusCode(403, new ApiResponse<WalletResponseDto>(403, "Forbidden", "You are not authorized to add funds to this wallet."));
            }

            var wallet = await _context.Wallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                return NotFound(new ApiResponse<WalletResponseDto>(404, "NotFound", "Wallet not found."));
            }

            try
            {
                wallet.Balance += dto.Amount;
                wallet.Updatedat = DateTime.UtcNow;

                var transaction = new Wallettransaction
                {
                    WalletId = wallet.WalletId,
                    Amount = dto.Amount,
                    Direction = 1, 
                    Type = "Deposit",
                    Relatedid = dto.Relatedid,
                    Createdat = DateTime.UtcNow,
                    Bankinfo = dto.Bankinfo?.Trim(),
                    Comment = dto.Comment?.Trim() ?? "Deposit"
                };

                _context.Wallettransactions.Add(transaction);
                await _context.SaveChangesAsync();

                var responseData = new WalletResponseDto
                {
                    WalletId = wallet.WalletId,
                    UserId = wallet.UserId,
                    UserName = wallet.User?.Fullname ?? "Unknown",
                    Balance = wallet.Balance,
                    Createdat = wallet.Createdat,
                    Updatedat = wallet.Updatedat
                };

                return Ok(new ApiResponse<WalletResponseDto>(200, "Success", "Funds added successfully.", null, responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<WalletResponseDto>(500, "InternalServerError", $"An error occurred while adding funds: {ex.Message}"));
            }
        }

        [HttpPost("{userId}/withdraw-funds")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Customer}")]
        public async Task<ActionResult> WithdrawFunds(int userId, [FromBody] WalletWithdrawFundsDto dto)
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
                return BadRequest(new ApiResponse<WalletResponseDto>(400, "BadRequest", "Invalid input data.", errors: errors));
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new ApiResponse<WalletResponseDto>(401, "Unauthorized", "User not authenticated."));
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdmin = userRole == UserRole.Admin;
            var isOwner = currentUserId == userId;

            if (!isOwner && !isAdmin)
            {
                return StatusCode(403, new ApiResponse<WalletResponseDto>(403, "Forbidden", "You are not authorized to withdraw funds from this wallet."));
            }

            var wallet = await _context.Wallets
                .Include(w => w.User)
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                return NotFound(new ApiResponse<WalletResponseDto>(404, "NotFound", "Wallet not found."));
            }

            if (wallet.Balance < dto.Amount)
            {
                return BadRequest(new ApiResponse<WalletResponseDto>(400, "BadRequest", "Insufficient balance."));
            }

            try
            {
                wallet.Balance -= dto.Amount;
                wallet.Updatedat = DateTime.UtcNow;

                var transaction = new Wallettransaction
                {
                    WalletId = wallet.WalletId,
                    Amount = dto.Amount,
                    Direction = -1,
                    Type = "Withdrawal",
                    Relatedid = dto.Relatedid,
                    Createdat = DateTime.UtcNow,
                    Bankinfo = dto.Bankinfo?.Trim(),
                    Comment = dto.Comment?.Trim() ?? "Withdrawal"
                };

                _context.Wallettransactions.Add(transaction);
                await _context.SaveChangesAsync();

                var responseData = new WalletResponseDto
                {
                    WalletId = wallet.WalletId,
                    UserId = wallet.UserId,
                    UserName = wallet.User?.Fullname ?? "Unknown",
                    Balance = wallet.Balance,
                    Createdat = wallet.Createdat,
                    Updatedat = wallet.Updatedat
                };

                return Ok(new ApiResponse<WalletResponseDto>(200, "Success", "Funds withdrawn successfully.",null ,responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<WalletResponseDto>(500, "InternalServerError", $"An error occurred while withdrawing funds: {ex.Message}"));
            }
        }

        [HttpGet("{userId}/transactions")]
        public async Task<ActionResult> GetTransactions(int userId)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new ApiResponse<List<WalletTransactionResponseDto>>(401, "Unauthorized", "User not authenticated."));
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == userId;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new ApiResponse<List<WalletTransactionResponseDto>>(403, "Forbidden", "You are not authorized to view this wallet's transactions."));
            }

            var wallet = await _context.Wallets.FirstOrDefaultAsync(w => w.UserId == userId);
            if (wallet == null)
            {
                return NotFound(new ApiResponse<List<WalletTransactionResponseDto>>(404, "NotFound", "Wallet not found."));
            }

            try
            {
                var transactions = await _context.Wallettransactions
                    .Where(t => t.WalletId == wallet.WalletId)
                    .ToListAsync();

                var responseData = transactions.Select(t => new WalletTransactionResponseDto
                {
                    TransactionId = t.TransactionId,
                    WalletId = t.WalletId,
                    Amount = t.Amount,
                    Direction = t.Direction,
                    Type = t.Type,
                    Relatedid = t.Relatedid,
                    Createdat = t.Createdat,
                    Bankinfo = t.Bankinfo,
                    Comment = t.Comment
                }).ToList();

                return Ok(new ApiResponse<List<WalletTransactionResponseDto>>(200, "Success", "Transactions retrieved successfully.", null, responseData));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<WalletTransactionResponseDto>>(500, "InternalServerError", $"An error occurred while retrieving transactions: {ex.Message}"));
            }
        }
    }
}