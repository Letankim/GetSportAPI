using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class RegisterDto
    {
        [Required(ErrorMessage = "Full name is required.")]
        [StringLength(100, MinimumLength = 2, ErrorMessage = "Full name must be between 2 and 100 characters.")]
        public string Fullname { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
        public string Password { get; set; } = null!;

        [Required(ErrorMessage = "Email is required.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        [StringLength(100, ErrorMessage = "Email must not exceed 100 characters.")]
        public string Email { get; set; } = null!;

        [StringLength(10, ErrorMessage = "Gender must not exceed 10 characters.")]
        public string? Gender { get; set; }

        [StringLength(20, ErrorMessage = "Phone number must not exceed 20 characters.")]
        [RegularExpression(@"^\+?[1-9]\d{1,14}$", ErrorMessage = "Invalid phone number format.")]
        public string? Phonenumber { get; set; }

        public DateOnly? Dateofbirth { get; set; }

        [StringLength(50, ErrorMessage = "Skill level must not exceed 50 characters.")]
        public string? Skilllevel { get; set; }

        [StringLength(50, ErrorMessage = "Membership type must not exceed 50 characters.")]
        public string? Membershiptype { get; set; }

        [Required(ErrorMessage = "Role is required.")]
        [RegularExpression("^(Admin|Staff|Customer)$", ErrorMessage = "Role must be 'Admin', 'Staff', or 'Customer'.")]
        public string Role { get; set; } = null!;
    }
}
