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
    public class VoucherController : ControllerBase
    {
        private readonly GetSportContext _context;

        public VoucherController(GetSportContext context)
        {
            _context = context;
        }

        [HttpPost]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Create([FromBody] VoucherCreateDto dto)
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

            if (dto.Startdate > dto.Enddate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
            }

            if (await _context.Vouchers.AnyAsync(v => v.Code == dto.Code))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Voucher code already exists." });
            }

            try
            {
                var voucher = new Voucher
                {
                    Code = dto.Code.Trim(),
                    Description = dto.Description?.Trim(),
                    Discountpercent = dto.Discountpercent,
                    Startdate = dto.Startdate,
                    Enddate = dto.Enddate,
                    Usagelimit = dto.Usagelimit,
                    Usage = 0,
                    Isactive = dto.Isactive
                };

                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();

                var responseData = new VoucherResponseDto
                {
                    VoucherId = voucher.VoucherId,
                    Code = voucher.Code,
                    Description = voucher.Description,
                    Discountpercent = voucher.Discountpercent,
                    Startdate = voucher.Startdate,
                    Enddate = voucher.Enddate,
                    Usagelimit = voucher.Usagelimit,
                    Usage = voucher.Usage,
                    Isactive = voucher.Isactive
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher created successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while creating the voucher." });
            }
        }

        [HttpPost("assign")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> AssignVoucher([FromBody] AssignVoucherDto dto)
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

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == dto.VoucherId);
            if (voucher == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Voucher not found." });
            }

            var user = await _context.Accounts.FirstOrDefaultAsync(a => a.UserId == dto.UserId);
            if (user == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "User not found." });
            }

            if (await _context.Uservouchers.AnyAsync(uv => uv.VoucherId == dto.VoucherId && uv.UserId == dto.UserId && uv.Usedat == null))
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Voucher already assigned to this user and not used." });
            }

            try
            {
                var userVoucher = new Uservoucher
                {
                    UserId = dto.UserId,
                    VoucherId = dto.VoucherId,
                    Assignedat = DateTime.UtcNow
                };

                _context.Uservouchers.Add(userVoucher);
                await _context.SaveChangesAsync();

                var responseData = new UserVoucherResponseDto
                {
                    UservoucherId = userVoucher.UservoucherId,
                    UserId = userVoucher.UserId,
                    UserName = user.Fullname ?? "Unknown",
                    VoucherId = userVoucher.VoucherId,
                    VoucherCode = voucher.Code,
                    Discountpercent = voucher.Discountpercent,
                    Usedat = userVoucher.Usedat,
                    Assignedat = userVoucher.Assignedat
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher assigned successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while assigning the voucher." });
            }
        }

        public class AssignVoucherDto
        {
            [Required(ErrorMessage = "User ID is required.")]
            public int UserId { get; set; }

            [Required(ErrorMessage = "Voucher ID is required.")]
            public int VoucherId { get; set; }
        }

        [HttpGet]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> GetAll()
        {
            var vouchers = await _context.Vouchers.ToListAsync();

            var responseData = vouchers.Select(voucher => new VoucherResponseDto
            {
                VoucherId = voucher.VoucherId,
                Code = voucher.Code,
                Description = voucher.Description,
                Discountpercent = voucher.Discountpercent,
                Startdate = voucher.Startdate,
                Enddate = voucher.Enddate,
                Usagelimit = voucher.Usagelimit,
                Usage = voucher.Usage,
                Isactive = voucher.Isactive
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Vouchers retrieved successfully.", Data = responseData });
        }

        [HttpGet("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> GetById(int id)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == id);

            if (voucher == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Voucher not found." });
            }

            var responseData = new VoucherResponseDto
            {
                VoucherId = voucher.VoucherId,
                Code = voucher.Code,
                Description = voucher.Description,
                Discountpercent = voucher.Discountpercent,
                Startdate = voucher.Startdate,
                Enddate = voucher.Enddate,
                Usagelimit = voucher.Usagelimit,
                Usage = voucher.Usage,
                Isactive = voucher.Isactive
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher retrieved successfully.", Data = responseData });
        }

        [HttpGet("check")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> CheckVoucher([FromQuery] VoucherCheckDto dto)
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

            var voucher = await _context.Vouchers
                .Include(v => v.Uservouchers)
                .FirstOrDefaultAsync(v => v.Code == dto.Code && v.Isactive && v.Startdate <= DateTime.UtcNow && v.Enddate >= DateTime.UtcNow);

            if (voucher == null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Invalid or expired voucher code." });
            }

            if (voucher.Usagelimit.HasValue && voucher.Usage >= voucher.Usagelimit.Value)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Voucher usage limit reached." });
            }

            var userVoucher = voucher.Uservouchers.FirstOrDefault(uv => uv.UserId == userId && uv.Usedat == null);
            if (userVoucher == null)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Voucher not assigned to this user or already used." });
            }

            var responseData = new VoucherResponseDto
            {
                VoucherId = voucher.VoucherId,
                Code = voucher.Code,
                Description = voucher.Description,
                Discountpercent = voucher.Discountpercent,
                Startdate = voucher.Startdate,
                Enddate = voucher.Enddate,
                Usagelimit = voucher.Usagelimit,
                Usage = voucher.Usage,
                Isactive = voucher.Isactive
            };

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher is valid.", Data = responseData });
        }

        [HttpGet("my")]
        [Authorize(Roles = $"{UserRole.Customer}")]
        public async Task<ActionResult> GetMyVouchers()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim, out int userId))
            {
                return Unauthorized(new { StatusCode = 401, Status = "Unauthorized", Message = "User not authenticated." });
            }

            var userVouchers = await _context.Uservouchers
                .Include(uv => uv.Voucher)
                .Include(uv => uv.User)
                .Where(uv => uv.UserId == userId)
                .ToListAsync();

            var responseData = userVouchers.Select(uv => new UserVoucherResponseDto
            {
                UservoucherId = uv.UservoucherId,
                UserId = uv.UserId,
                UserName = uv.User?.Fullname ?? "Unknown",
                VoucherId = uv.VoucherId,
                VoucherCode = uv.Voucher.Code,
                Discountpercent = uv.Voucher.Discountpercent,
                Usedat = uv.Usedat,
                Assignedat = uv.Assignedat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Your voucher history retrieved successfully.", Data = responseData });
        }

        [HttpGet("history")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> GetAllVoucherHistory()
        {
            var userVouchers = await _context.Uservouchers
                .Include(uv => uv.Voucher)
                .Include(uv => uv.User)
                .ToListAsync();

            var responseData = userVouchers.Select(uv => new UserVoucherResponseDto
            {
                UservoucherId = uv.UservoucherId,
                UserId = uv.UserId,
                UserName = uv.User?.Fullname ?? "Unknown",
                VoucherId = uv.VoucherId,
                VoucherCode = uv.Voucher.Code,
                Discountpercent = uv.Voucher.Discountpercent,
                Usedat = uv.Usedat,
                Assignedat = uv.Assignedat
            }).ToList();

            return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher usage history retrieved successfully.", Data = responseData });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Update(int id, [FromBody] VoucherUpdateDto dto)
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

            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == id);

            if (voucher == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Voucher not found." });
            }

            if (dto.Startdate.HasValue && dto.Enddate.HasValue && dto.Startdate > dto.Enddate)
            {
                return BadRequest(new { StatusCode = 400, Status = "BadRequest", Message = "Start date cannot be after end date." });
            }

            try
            {
                if (!string.IsNullOrEmpty(dto.Description)) voucher.Description = dto.Description.Trim();
                if (dto.Discountpercent.HasValue) voucher.Discountpercent = dto.Discountpercent.Value;
                if (dto.Startdate.HasValue) voucher.Startdate = dto.Startdate.Value;
                if (dto.Enddate.HasValue) voucher.Enddate = dto.Enddate.Value;
                if (dto.Usagelimit.HasValue) voucher.Usagelimit = dto.Usagelimit;
                if (dto.Isactive.HasValue) voucher.Isactive = dto.Isactive.Value;

                await _context.SaveChangesAsync();

                var responseData = new VoucherResponseDto
                {
                    VoucherId = voucher.VoucherId,
                    Code = voucher.Code,
                    Description = voucher.Description,
                    Discountpercent = voucher.Discountpercent,
                    Startdate = voucher.Startdate,
                    Enddate = voucher.Enddate,
                    Usagelimit = voucher.Usagelimit,
                    Usage = voucher.Usage,
                    Isactive = voucher.Isactive
                };

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher updated successfully.", Data = responseData });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while updating the voucher." });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = $"{UserRole.Admin}")]
        public async Task<ActionResult> Delete(int id)
        {
            var voucher = await _context.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == id);

            if (voucher == null)
            {
                return NotFound(new { StatusCode = 404, Status = "NotFound", Message = "Voucher not found." });
            }

            try
            {
                _context.Vouchers.Remove(voucher);
                await _context.SaveChangesAsync();

                return Ok(new { StatusCode = 200, Status = "Success", Message = "Voucher deleted successfully." });
            }
            catch (Exception)
            {
                return StatusCode(500, new { StatusCode = 500, Status = "InternalServerError", Message = "An error occurred while deleting the voucher." });
            }
        }
    }
}