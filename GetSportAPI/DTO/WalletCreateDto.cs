using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class WalletCreateDto
    {
        [Required(ErrorMessage = "User ID is required.")]
        public int UserId { get; set; }

        [Range(0, double.MaxValue, ErrorMessage = "Initial balance cannot be negative.")]
        public decimal InitialBalance { get; set; } = 0;
    }
}
