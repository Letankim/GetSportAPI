namespace GetSportAPI.DTO
{
    public class FeedbackResponseDto
    {
        public int FeedbackId { get; set; }
        public int BookingId { get; set; }
        public DateTime Bookingdate { get; set; }
        public int CourtId { get; set; }
        public string CourtName { get; set; } = null!;
        public string CourtLocation { get; set; } = null!;
        public List<string> CourtImageUrls { get; set; } = new List<string>();
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime Createat { get; set; }
    }
}
