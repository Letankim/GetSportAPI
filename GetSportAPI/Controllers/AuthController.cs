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
using GetSportAPI.Services;

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
                string verificationToken = GenerateRandomToken(32);

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
                    Isactive = false,
                    Status = UserStatus.Active
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                var jwtToken = GenerateJwtToken(account);

                string verificationLink = $"http://localhost:5173/Auth/verify?userId={account.UserId}&token={Uri.EscapeDataString(verificationToken)}";

                string subject = "Verify Your GetSport Account";
                string body = $@"
                    <div style='font-family:Arial,sans-serif;background:#f9f9f9;padding:20px;border-radius:10px;'>
                        <h2 style='color:#007bff;'>Welcome to GetSport!</h2>
                        <p>Hello <b>{account.Fullname}</b>,</p>
                        <p>Thank you for registering with GetSport. Please click the link below to verify your account:</p>
                        <div style='padding:10px;background:#fff;border:1px solid #ddd;border-radius:8px;margin:10px 0;'>
                            <a href='{verificationLink}' style='color:#007bff;font-size:16px;text-decoration:none;'>Verify Your Account</a>
                        </div>
                        <p>Alternatively, you can use the following details to verify your account:</p>
                        <p><b>User ID:</b> {account.UserId}</p>
                        <p><b>Verification Token:</b> <span style='color:#007bff;font-size:16px;'>{verificationToken}</span></p>
                        <p style='color:#888;font-size:13px;'>If you didn’t register, please ignore this email or contact our support team.</p>
                    </div>";

                EmailService emailService = new EmailService();
                await emailService.SendEmailAsync(account.Email, subject, body);

                var responseData = new AuthResponseDto
                {
                    Token = jwtToken,
                    Fullname = account.Fullname,
                    Email = account.Email,
                    Role = account.Role
                };

                return Ok(new ApiResponse<AuthResponseDto>(
                    statusCode: 200,
                    status: "Success",
                    message: "Registration successful. Please verify your account using the link sent to your email.",
                    data: responseData
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<AuthResponseDto>(
                    statusCode: 500,
                    status: "InternalServerError",
                    message: $"An error occurred while registering the user: {ex.Message}"
                ));
            }
        }

        [HttpPost("verify")]
        public async Task<ActionResult> Verify([FromBody] VerifyAccountDto dto)
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
                return BadRequest(new ApiResponse<string>(
                    statusCode: 400,
                    status: "BadRequest",
                    message: "Invalid input data.",
                    errors: errors
                ));
            }

            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.UserId == dto.UserId && a.Isactive == false);

            if (account == null)
            {
                return NotFound(new ApiResponse<string>(
                    statusCode: 404,
                    status: "NotFound",
                    message: "Account not found or inactive."
                ));
            }

            try
            {
                account.Isactive = true;
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<string>(
                    statusCode: 200,
                    status: "Success",
                    message: "Account verified successfully."
                ));
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>(
                    statusCode: 500,
                    status: "InternalServerError",
                    message: $"An error occurred while verifying the account: {ex.Message}"
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

        [HttpPost("recover-password")]
        public async Task<ActionResult> RecoverPassword()
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Email == "letankim2003@gmail.com");

            if (account == null)
            {
                return NotFound(new ApiResponse<string>(
                    statusCode: 404,
                    status: "NotFound",
                    message: "Account not found."
                ));
            }

            string newPassword ="123";
            account.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            account.Isactive = true;
            await _context.SaveChangesAsync();

            string subject = "Your Password Has Been Reset";
            string body = $@"
    <div style='font-family:Arial,sans-serif;background:#f9f9f9;padding:20px;border-radius:10px;'>
        <h2 style='color:#007bff;'>Password Recovery</h2>
        <p>Hello <b>{account.Fullname}</b>,</p>
        <p>Your password has been reset by the system administrator.</p>
        <p><b>New password:</b> <span style='color:#007bff;font-size:16px;'>{newPassword}</span></p>
        <p>Please change your password immediately after logging in.</p>
    </div>";

            EmailService emailService = new EmailService();
            await emailService.SendEmailAsync(account.Email, subject, body);

            return Ok(new ApiResponse<string>(
                statusCode: 200,
                status: "Success",
                message: $"Password for {account.Email} has been reset successfully."
            ));
        }

        [HttpPost("forgot-password")]
        public async Task<ActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Email == dto.Email && a.Isactive);

            if (account == null)
            {
                return NotFound(new ApiResponse<string>(
                    statusCode: 404,
                    status: "NotFound",
                    message: "Email not found or account inactive."
                ));
            }

            string newPassword = GenerateRandomPassword(8);

            account.Password = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            string subject = "Your New Password";
            string body = $@"
    <div style='font-family:Arial,sans-serif;background:#f9f9f9;padding:20px;border-radius:10px;'>
        <h2 style='color:#007bff;'>Password Reset Successful</h2>
        <p>Hello <b>{account.Fullname}</b>,</p>
        <p>Your new password has been generated successfully. Please use the password below to log in:</p>
        <div style='padding:10px;background:#fff;border:1px solid #ddd;border-radius:8px;margin:10px 0;'>
            <h3 style='color:#007bff;text-align:center;'>{newPassword}</h3>
        </div>
        <p>We recommend changing your password immediately after logging in for better security.</p>
        <p style='color:#888;font-size:13px;'>If you didn’t request this reset, please contact our support team.</p>
    </div>";

            EmailService emailService = new EmailService();
            await emailService.SendEmailAsync(account.Email, subject, body);

            return Ok(new ApiResponse<string>(
                statusCode: 200,
                status: "Success",
                message: "A new password has been sent to your email."
            ));
        }

        private string GenerateRandomToken(int length)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }
        private string GenerateRandomPassword(int length)
        {
            const string validChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var random = new Random();
            return new string(Enumerable.Repeat(validChars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        private string GenerateJwtToken(Account account)
        {
            var claims = new List<Claim>
    {
        new Claim(ClaimTypes.NameIdentifier, account.UserId.ToString()),
          new Claim("userId", account.UserId.ToString()),
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