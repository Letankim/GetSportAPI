using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class VerifyAccountDto
    {
        [Required(ErrorMessage = "User ID is required.")]
        public int UserId { get; set; }

        [Required(ErrorMessage = "Verification token is required.")]
        public string Token { get; set; }
    }
}
