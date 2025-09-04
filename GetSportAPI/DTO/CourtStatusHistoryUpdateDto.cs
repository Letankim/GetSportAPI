using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class CourtStatusHistoryUpdateDto
    {
        [StringLength(50, ErrorMessage = "Status cannot exceed 50 characters.")]
        public string? Statusofcourt { get; set; }
    }
}
