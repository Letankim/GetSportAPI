using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class BulkCourtSlotCreateDto
    {
        [Required] public int CourtId { get; set; }
        [Required] public DateTime StartDateTime { get; set; }
        [Required] public DateTime EndDateTime { get; set; }
        [Required] public int Duration { get; set; }
    }
}
