using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class VoucherCheckDto
    {
        [Required(ErrorMessage = "Voucher code is required.")]
        [StringLength(50, ErrorMessage = "Voucher code cannot exceed 50 characters.")]
        public string Code { get; set; } = null!;
    }
}
