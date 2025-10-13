namespace GetSportAPI.DTO
{
    public class CourtResponseDto
    {
        public int CourtId { get; set; }
        public int OwnerId { get; set; }
        public string OwnerName { get; set; } = null!;
        public string? Location { get; set; }
        public List<string> Imageurls { get; set; } = new List<string>();
        public decimal Priceperhour { get; set; }
        public string? Status { get; set; }
        public bool Isactive { get; set; }
        public int Priority { get; set; }
        public DateTime? Startdate { get; set; }
        public DateTime? Enddate { get; set; }
    }
}