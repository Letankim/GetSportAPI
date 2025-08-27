using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using GetSportAPI.Models.Generated;
using GetSportAPI.Models.Enum;
using BCrypt.Net;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using GetSportAPI.DTO;
using System.Linq;

namespace GetSportAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly GetSportContext _context;
        private readonly IConfiguration _configuration;
        private readonly string[] _validRoles = { UserRole.Admin, UserRole.Staff, UserRole.Customer };

        public AuthController(GetSportContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<ActionResult> Register([FromBody] RegisterDto dto)
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
                return BadRequest(new ApiResponse<AuthResponseDto>(
                    statusCode: 400,
                    status: "BadRequest",
                    message: "Invalid input data.",
                    errors: errors
                ));
            }

            string email = dto.Email.Trim();
            string fullname = dto.Fullname.Trim();
            string role = dto.Role.Trim();

            if (!_validRoles.Contains(role))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>(
                    statusCode: 400,
                    status: "BadRequest",
                    message: "Invalid role. Allowed values are: Admin, Staff, Customer."
                ));
            }

            if (await _context.Accounts.AnyAsync(a => a.Email == email && a.Isactive))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>(
                    statusCode: 400,
                    status: "BadRequest",
                    message: "Email already exists."
                ));
            }

            if (dto.Dateofbirth.HasValue && dto.Dateofbirth.Value > DateOnly.FromDateTime(DateTime.UtcNow))
            {
                return BadRequest(new ApiResponse<AuthResponseDto>(
                    statusCode: 400,
                    status: "BadRequest",
                    message: "Date of birth cannot be in the future."
                ));
            }

            try
            {
                var account = new Account
                {
                    Fullname = fullname,
                    Password = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                    Email = email,
                    Gender = dto.Gender?.Trim(),
                    Phonenumber = dto.Phonenumber?.Trim(),
                    Dateofbirth = dto.Dateofbirth,
                    Skilllevel = dto.Skilllevel?.Trim(),
                    Membershiptype = dto.Membershiptype?.Trim(),
                    Role = role,
                    Totalpoint = 0,
                    Createat = DateTime.UtcNow,
                    Isactive = true,
                    Status = UserStatus.Active 
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                var token = GenerateJwtToken(account);

                var responseData = new AuthResponseDto
                {
                    Token = token,
                    Fullname = account.Fullname,
                    Email = account.Email,
                    Role = account.Role
                };

                return Ok(new ApiResponse<AuthResponseDto>(
                    statusCode: 200,
                    status: "Success",
                    message: "Registration successful.",
                    data: responseData
                ));
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<AuthResponseDto>(
                    statusCode: 500,
                    status: "InternalServerError",
                    message: "An error occurred while registering the user."
                ));
            }
        }

        [HttpPost("login")]
        public async Task<ActionResult> Login([FromBody] LoginDto dto)
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
                return BadRequest(new ApiResponse<AuthResponseDto>(
                    statusCode: 400,
                    status: "BadRequest",
                    message: "Invalid input data.",
                    errors: errors
                ));
            }

            var account = await _context.Accounts
                .Where(a => a.Email == dto.Email && a.Isactive)
                .FirstOrDefaultAsync();
            if (account == null || !BCrypt.Net.BCrypt.Verify(dto.Password, account.Password))
            {
                return Unauthorized(new ApiResponse<AuthResponseDto>(
                    statusCode: 401,
                    status: "Unauthorized",
                    message: "Invalid email or password."
                ));
            }

            if (account.Status == UserStatus.Banned)
            {
                return Unauthorized(new ApiResponse<AuthResponseDto>(
                    statusCode: 403,
                    status: "Forbidden",
                    message: "Account is banned."
                ));
            }

            try
            {
                var token = GenerateJwtToken(account);

                var responseData = new AuthResponseDto
                {
                    Token = token,
                    Fullname = account.Fullname,
                    Email = account.Email,
                    Role = account.Role
                };

                return Ok(new ApiResponse<AuthResponseDto>(
                    statusCode: 200,
                    status: "Success",
                    message: "Login successful.",
                    data: responseData
                ));
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<AuthResponseDto>(
                    statusCode: 500,
                    status: "InternalServerError",
                    message: "An error occurred while processing the login."
                ));
            }
        }

        private string GenerateJwtToken(Account account)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
        new Claim(ClaimTypes.Name, account.Fullname ?? string.Empty),
        new Claim(ClaimTypes.Role, account.Role ?? "Customer"),
        new Claim(ClaimTypes.Email, account.Email ?? string.Empty)
    };

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),  
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}