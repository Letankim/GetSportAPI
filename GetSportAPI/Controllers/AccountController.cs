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
    public class AccountController : ControllerBase
    {
        private readonly GetSportContext _context;

        public AccountController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Create([FromBody] AccountCreateDto dto)
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

            try
            {
                var account = new Account
                {
                    Role = dto.Role?.Trim() ?? UserRole.Customer,
                    Fullname = dto.Fullname.Trim(),
                    Password = BCrypt.Net.BCrypt.HashPassword(dto.Password), 
                    Gender = dto.Gender?.Trim(),
                    Phonenumber = dto.Phonenumber?.Trim(),
                    Email = dto.Email?.Trim(),
                    Dateofbirth = dto.Dateofbirth,
                    Skilllevel = dto.Skilllevel?.Trim(),
                    Membershiptype = dto.Membershiptype?.Trim(),
                    Totalpoint = dto.Totalpoint ?? 0,
                    Createat = DateTime.UtcNow,
                    Isactive = dto.Isactive ?? true,
                    Status = dto.Status?.Trim() ?? "Active"
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                // Create a wallet for the new account
                var wallet = new Wallet
                {
                    UserId = account.UserId,
                    Balance = 0,
                    Createdat = DateTime.UtcNow
                };
                _context.Wallets.Add(wallet);
                await _context.SaveChangesAsync();

                var responseData = new AccountResponseDto
                {
                    UserId = account.UserId,
                    Role = account.Role,
                    Fullname = account.Fullname,
                    Gender = account.Gender,
                    Phonenumber = account.Phonenumber,
                    Email = account.Email,
                    Dateofbirth = account.Dateofbirth,
                    Skilllevel = account.Skilllevel,
                    Membershiptype = account.Membershiptype,
                    Totalpoint = account.Totalpoint,
                    Createat = account.Createat,
                    Isactive = account.Isactive,
                    Status = account.Status,
                    WalletBalance = wallet.Balance
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Account created successfully.", Data = responseData });
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = ex.Message });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the account." });
            }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> GetAll([FromQuery] string? status = null, [FromQuery] bool? isActive = null)
        {
            var query = _context.Accounts
                .Include(a => a.Wallet)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(a => a.Status == status);
            }

            if (isActive.HasValue)
            {
                query = query.Where(a => a.Isactive == isActive.Value);
            }

            var accounts = await query.ToListAsync();

            var responseData = accounts.Select(account => new AccountResponseDto
            {
                UserId = account.UserId,
                Role = account.Role,
                Fullname = account.Fullname,
                Gender = account.Gender,
                Phonenumber = account.Phonenumber,
                Email = account.Email,
                Dateofbirth = account.Dateofbirth,
                Skilllevel = account.Skilllevel,
                Membershiptype = account.Membershiptype,
                Totalpoint = account.Totalpoint,
                Createat = account.Createat,
                Isactive = account.Isactive,
                Status = account.Status,
                WalletBalance = account.Wallet?.Balance ?? 0
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Accounts retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize]
        public async Task<ActionResult> GetById(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid account ID." });
            }

            var account = await _context.Accounts
                .Include(a => a.Wallet)
                .FirstOrDefaultAsync(a => a.UserId == id);

            if (account == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Account not found." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == id;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this account." });
            }

            var responseData = new AccountResponseDto
            {
                UserId = account.UserId,
                Role = account.Role,
                Fullname = account.Fullname,
                Gender = account.Gender,
                Phonenumber = account.Phonenumber,
                Email = account.Email,
                Dateofbirth = account.Dateofbirth,
                Skilllevel = account.Skilllevel,
                Membershiptype = account.Membershiptype,
                Totalpoint = account.Totalpoint,
                Createat = account.Createat,
                Isactive = account.Isactive,
                Status = account.Status,
                WalletBalance = account.Wallet?.Balance ?? 0
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Account retrieved successfully.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize]
        public async Task<ActionResult> GetMyAccount()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var account = await _context.Accounts
                .Include(a => a.Wallet)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (account == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Account not found." });
            }

            var responseData = new AccountResponseDto
            {
                UserId = account.UserId,
                Role = account.Role,
                Fullname = account.Fullname,
                Gender = account.Gender,
                Phonenumber = account.Phonenumber,
                Email = account.Email,
                Dateofbirth = account.Dateofbirth,
                Skilllevel = account.Skilllevel,
                Membershiptype = account.Membershiptype,
                Totalpoint = account.Totalpoint,
                Createat = account.Createat,
                Isactive = account.Isactive,
                Status = account.Status,
                WalletBalance = account.Wallet?.Balance ?? 0
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your account retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<ActionResult> Update(int id, [FromBody] AccountUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid account ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var account = await _context.Accounts
                .Include(a => a.Wallet)
                .FirstOrDefaultAsync(a => a.UserId == id);

            if (account == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Account not found." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == id;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to update this account." });
            }

            try
            {
                // Fields updatable by owner or admin/staff
                if (!string.IsNullOrEmpty(dto.Fullname)) account.Fullname = dto.Fullname.Trim();
                if (!string.IsNullOrEmpty(dto.Gender)) account.Gender = dto.Gender.Trim();
                if (!string.IsNullOrEmpty(dto.Phonenumber)) account.Phonenumber = dto.Phonenumber.Trim();
                if (!string.IsNullOrEmpty(dto.Email)) account.Email = dto.Email.Trim();
                if (dto.Dateofbirth.HasValue) account.Dateofbirth = dto.Dateofbirth;
                if (!string.IsNullOrEmpty(dto.Skilllevel)) account.Skilllevel = dto.Skilllevel.Trim();
                if (!string.IsNullOrEmpty(dto.Membershiptype)) account.Membershiptype = dto.Membershiptype.Trim();

                // Fields only updatable by admin/staff
                if (isAdminOrStaff)
                {
                    if (!string.IsNullOrEmpty(dto.Role)) account.Role = dto.Role.Trim();
                    if (dto.Totalpoint.HasValue) account.Totalpoint = dto.Totalpoint.Value;
                    if (!string.IsNullOrEmpty(dto.Status)) account.Status = dto.Status.Trim();
                    if (dto.Isactive.HasValue) account.Isactive = dto.Isactive.Value;
                }

                await _context.SaveChangesAsync();

                var responseData = new AccountResponseDto
                {
                    UserId = account.UserId,
                    Role = account.Role,
                    Fullname = account.Fullname,
                    Gender = account.Gender,
                    Phonenumber = account.Phonenumber,
                    Email = account.Email,
                    Dateofbirth = account.Dateofbirth,
                    Skilllevel = account.Skilllevel,
                    Membershiptype = account.Membershiptype,
                    Totalpoint = account.Totalpoint,
                    Createat = account.Createat,
                    Isactive = account.Isactive,
                    Status = account.Status,
                    WalletBalance = account.Wallet?.Balance ?? 0
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Account updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the account." });
            }
        }

        [HttpPut("my")]
        [Authorize]
        public async Task<ActionResult> UpdateMyAccount([FromBody] AccountUpdateDto dto)
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

            var account = await _context.Accounts
                .Include(a => a.Wallet)
                .FirstOrDefaultAsync(a => a.UserId == userId);

            if (account == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Account not found." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Fullname)) account.Fullname = dto.Fullname.Trim();
                if (!string.IsNullOrEmpty(dto.Gender)) account.Gender = dto.Gender.Trim();
                if (!string.IsNullOrEmpty(dto.Phonenumber)) account.Phonenumber = dto.Phonenumber.Trim();
                if (!string.IsNullOrEmpty(dto.Email)) account.Email = dto.Email.Trim();
                if (dto.Dateofbirth.HasValue) account.Dateofbirth = dto.Dateofbirth;
                if (!string.IsNullOrEmpty(dto.Skilllevel)) account.Skilllevel = dto.Skilllevel.Trim();
                if (!string.IsNullOrEmpty(dto.Membershiptype)) account.Membershiptype = dto.Membershiptype.Trim();

                await _context.SaveChangesAsync();

                var responseData = new AccountResponseDto
                {
                    UserId = account.UserId,
                    Role = account.Role,
                    Fullname = account.Fullname,
                    Gender = account.Gender,
                    Phonenumber = account.Phonenumber,
                    Email = account.Email,
                    Dateofbirth = account.Dateofbirth,
                    Skilllevel = account.Skilllevel,
                    Membershiptype = account.Membershiptype,
                    Totalpoint = account.Totalpoint,
                    Createat = account.Createat,
                    Isactive = account.Isactive,
                    Status = account.Status,
                    WalletBalance = account.Wallet?.Balance ?? 0
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Your account updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating your account." });
            }
        }

        [HttpGet("{id}/wallet")]
        [Authorize]
        public async Task<ActionResult> GetWallet(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid account ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == id;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view this wallet." });
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == id);

            if (wallet == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Wallet not found." });
            }

            var responseData = new WalletResponseDto
            {
                WalletId = wallet.WalletId,
                UserId = wallet.UserId,
                Balance = wallet.Balance,
                Createdat = wallet.Createdat,
                Updatedat = wallet.Updatedat
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Wallet retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}/wallet")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> UpdateWallet(int id, [FromBody] WalletUpdateDto dto)
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
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid account ID." });
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == id);

            if (wallet == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Wallet not found." });
            }

            try
            {
                if (dto.Balance.HasValue)
                {
                    var transaction = new Wallettransaction
                    {
                        WalletId = wallet.WalletId,
                        Amount = dto.Balance.Value - wallet.Balance,
                        Direction = dto.Balance.Value > wallet.Balance ? 1 : -1,
                        Type = "AdminAdjustment",
                        Createdat = DateTime.UtcNow,
                        Comment = "Balance adjusted by admin/staff"
                    };
                    _context.Wallettransactions.Add(transaction);

                    wallet.Balance = dto.Balance.Value;
                    wallet.Updatedat = DateTime.UtcNow;
                }

                await _context.SaveChangesAsync();

                var responseData = new WalletResponseDto
                {
                    WalletId = wallet.WalletId,
                    UserId = wallet.UserId,
                    Balance = wallet.Balance,
                    Createdat = wallet.Createdat,
                    Updatedat = wallet.Updatedat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Wallet updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the wallet." });
            }
        }

        [HttpGet("{id}/transactions")]
        [Authorize]
        public async Task<ActionResult> GetTransactions(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid account ID." });
            }

            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int currentUserId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userRole = User.FindFirstValue(ClaimTypes.Role);
            var isAdminOrStaff = userRole == UserRole.Admin || userRole == UserRole.Staff;
            var isOwner = currentUserId == id;

            if (!isOwner && !isAdminOrStaff)
            {
                return StatusCode(403, new { StatusCode = 403, Status = "Forbidden", Message = "You are not authorized to view these transactions." });
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == id);

            if (wallet == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Wallet not found." });
            }

            var transactions = await _context.Wallettransactions
                .Where(t => t.WalletId == wallet.WalletId)
                .OrderByDescending(t => t.Createdat)
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

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Transactions retrieved successfully.", Data = responseData });
        }

        [HttpGet("my/transactions")]
        [Authorize]
        public async Task<ActionResult> GetMyTransactions()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var wallet = await _context.Wallets
                .FirstOrDefaultAsync(w => w.UserId == userId);

            if (wallet == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Wallet not found." });
            }

            var transactions = await _context.Wallettransactions
                .Where(t => t.WalletId == wallet.WalletId)
                .OrderByDescending(t => t.Createdat)
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

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your transactions retrieved successfully.", Data = responseData });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin},{UserRole.Staff}")]
        public async Task<ActionResult> Delete(int id)
        {
            if (id <= 0)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid account ID." });
            }

            var account = await _context.Accounts.FindAsync(id);

            if (account == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Account not found." });
            }

            try
            {
                account.Isactive = false;
                account.Status = "Deleted";

                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Account marked as deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the account." });
            }
        }
    }
}