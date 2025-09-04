using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class WalletWithdrawFundsDto
    {
        [Required(ErrorMessage = "Amount is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be positive.")]
        public decimal Amount { get; set; }

        [StringLength(200, ErrorMessage = "Comment cannot exceed 200 characters.")]
        public string? Comment { get; set; }

        [StringLength(100, ErrorMessage = "Bank info cannot exceed 100 characters.")]
        public string? Bankinfo { get; set; }

        public int? Relatedid { get; set; }
    }
}
