namespace GetSportAPI.DTO
{
    public class CourtStatusHistoryResponseDto
    {
        public int StatusId { get; set; }
        public int CourtId { get; set; }
        public string Statusofcourt { get; set; } = null!;
        public DateTime Updateat { get; set; }
    }
}
