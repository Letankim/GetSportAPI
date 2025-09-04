using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class PackageUpdateDto
    {
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters.")]
        public string? Name { get; set; }

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
        public string? Description { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "Price must be greater than 0.")]
        public decimal? Price { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Duration must be greater than 0.")]
        public int? Durationdays { get; set; }

        public bool? Isactive { get; set; }
    }
}
