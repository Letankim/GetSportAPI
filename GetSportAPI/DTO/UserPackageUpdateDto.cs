using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class UserPackageUpdateDto
    {
        public DateOnly? Startdate { get; set; }
        public DateOnly? Enddate { get; set; }
        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string? Status { get; set; }
    }
}
