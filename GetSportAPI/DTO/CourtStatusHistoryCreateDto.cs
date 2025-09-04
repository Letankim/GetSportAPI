using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class CourtStatusHistoryCreateDto
    {
        [Required(ErrorMessage = "Court ID is required.")]
        public int CourtId { get; set; }

        [Required(ErrorMessage = "Status is required.")]
        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string Statusofcourt { get; set; } = null!;
    }

}
