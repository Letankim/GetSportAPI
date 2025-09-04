using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class VoucherCreateDto
    {
        [Required(ErrorMessage = "Code is required.")]
        [StringLength(50, ErrorMessage = "Code cannot exceed 50 characters.")]
        public string Code { get; set; } = null!;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "Discount percent is required.")]
        [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100.")]
        public decimal Discountpercent { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        public DateTime Startdate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        public DateTime Enddate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be greater than 0 if specified.")]
        public int? Usagelimit { get; set; }

        public bool Isactive { get; set; }
    }
}
