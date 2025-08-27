namespace GetSportAPI.DTO
{
    public class CourtSlotResponseDto
    {
        public int SlotId { get; set; }
        public int CourtId { get; set; }
        public int Slotnumber { get; set; }
        public DateTime Starttime { get; set; }
        public DateTime Endtime { get; set; }
        public bool Isavailable { get; set; }
        public string CourtLocation { get; set; } = null!;
        public int OwnerId { get; set; }
    }
}
