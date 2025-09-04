using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class OwnerPackageUpdateDto
    {
        [StringLength(100, ErrorMessage = "Package name cannot exceed 100 characters.")]
        public string? Packagename { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Duration must be greater than 0.")]
        public int? Duration { get; set; }

        public DateOnly? Startdate { get; set; }

        public DateOnly? Enddate { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal? Price { get; set; }

        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string? Status { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Priority must be non-negative.")]
        public int? Priority { get; set; }
    }
}
