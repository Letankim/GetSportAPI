using System;
using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class AccountCreateDto
    {
        [Required(ErrorMessage = "Fullname is required.")]
        [StringLength(100, ErrorMessage = "Fullname cannot exceed 100 characters.")]
        public string Fullname { get; set; } = null!;

        [Required(ErrorMessage = "Password is required.")]
        [StringLength(100, MinimumLength = 6, ErrorMessage = "Password must be between 6 and 100 characters.")]
        public string Password { get; set; } = null!;

        [StringLength(50, ErrorMessage = "Role cannot exceed 50 characters.")]
        public string? Role { get; set; }

        [StringLength(10, ErrorMessage = "Gender cannot exceed 10 characters.")]
        public string? Gender { get; set; }

        [StringLength(15, ErrorMessage = "Phone number cannot exceed 15 characters.")]
        [RegularExpression(@"^\+?\d{10,15}$", ErrorMessage = "Invalid phone number format.")]
        public string? Phonenumber { get; set; }

        [StringLength(100, ErrorMessage = "Email cannot exceed 100 characters.")]
        [EmailAddress(ErrorMessage = "Invalid email format.")]
        public string? Email { get; set; }

        public DateOnly? Dateofbirth { get; set; }

        [StringLength(50, ErrorMessage = "Skill level cannot exceed 50 characters.")]
        public string? Skilllevel { get; set; }

        [StringLength(50, ErrorMessage = "Membership type cannot exceed 50 characters.")]
        public string? Membershiptype { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Total points cannot be negative.")]
        public int? Totalpoint { get; set; }

        public bool? Isactive { get; set; }

        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string? Status { get; set; }
    }
}