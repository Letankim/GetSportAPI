using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class CourtCreateDto
    {
        [Required(ErrorMessage = "Location is required.")]
        [StringLength(255, MinimumLength = 5, ErrorMessage = "Location must be between 5 and 255 characters.")]
        public string Location { get; set; } = null!;

        [Required(ErrorMessage = "Price per hour is required.")]
        [Range(0.01, double.MaxValue, ErrorMessage = "Price per hour must be greater than 0.")]
        public decimal Priceperhour { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Priority must be non-negative.")]
        public int Priority { get; set; } = 0;

        public DateTime? Startdate { get; set; }

        public DateTime? Enddate { get; set; }

        public IFormFile? Image { get; set; }
    }
}
