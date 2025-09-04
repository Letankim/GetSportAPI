namespace GetSportAPI.DTO
{
    public class FeedbackResponseDto
    {
        public int FeedbackId { get; set; }
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
        public DateTime Createat { get; set; }
    }
}
