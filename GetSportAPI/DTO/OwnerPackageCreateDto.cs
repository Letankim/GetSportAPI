using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class OwnerPackageCreateDto
    {
        [Required(ErrorMessage = "Owner ID is required.")]
        public int OwnerId { get; set; }

        [Required(ErrorMessage = "Package name is required.")]
        [StringLength(100, ErrorMessage = "Package name cannot exceed 100 characters.")]
        public string Packagename { get; set; } = null!;

        [Required(ErrorMessage = "Duration is required.")]
        [Range(1, int.MaxValue, ErrorMessage = "Duration must be greater than 0.")]
        public int Duration { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        public DateOnly Startdate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        public DateOnly Enddate { get; set; }

        [Required(ErrorMessage = "Price is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Priority is required.")]
        [Range(0, int.MaxValue, ErrorMessage = "Priority must be non-negative.")]
        public int Priority { get; set; }
    }
}
