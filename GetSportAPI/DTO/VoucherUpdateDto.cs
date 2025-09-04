using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class VoucherUpdateDto
    {
        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Range(0, 100, ErrorMessage = "Discount percent must be between 0 and 100.")]
        public decimal? Discountpercent { get; set; }

        public DateTime? Startdate { get; set; }

        public DateTime? Enddate { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Usage limit must be greater than 0 if specified.")]
        public int? Usagelimit { get; set; }

        public bool? Isactive { get; set; }
    }
}
