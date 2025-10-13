using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = null!;
        public string Fullname { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Role { get; set; } = null!;
    }

    public class ForgotPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; }
    }

    public class ResetPasswordDto
    {
        [Required, EmailAddress]
        public string Email { get; set; }

        [Required]
        public string Token { get; set; }

        [Required, MinLength(6)]
        public string NewPassword { get; set; }
    }

}
