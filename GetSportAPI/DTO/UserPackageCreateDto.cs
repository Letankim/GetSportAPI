using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class UserPackageCreateDto
    {
        [Required(ErrorMessage = "Package ID is required.")]
        public int PackageId { get; set; }

        [Required(ErrorMessage = "Start date is required.")]
        public DateOnly Startdate { get; set; }

        [Required(ErrorMessage = "End date is required.")]
        public DateOnly Enddate { get; set; }
    }
}
