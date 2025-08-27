namespace GetSportAPI.DTO
{
    public class CourtSlotUpdateDto
    {
        public int? Slotnumber { get; set; }
        public DateTime? Starttime { get; set; }
        public DateTime? Endtime { get; set; }
        public bool? Isavailable { get; set; }
    }
}
