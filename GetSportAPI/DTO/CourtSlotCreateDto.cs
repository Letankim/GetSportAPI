using System.ComponentModel.DataAnnotations;

namespace GetSportAPI.DTO
{
    public class CourtSlotCreateDto
    {
        [Required]
        public int CourtId { get; set; }
        [Required]
        public int Slotnumber { get; set; }
        [Required]
        public DateTime Starttime { get; set; }
        [Required]
        public DateTime Endtime { get; set; }
        public bool Isavailable { get; set; } = true;
    }
}
