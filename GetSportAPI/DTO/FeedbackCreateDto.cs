namespace GetSportAPI.DTO
{
    public class FeedbackCreateDto
    {
        public int BookingId { get; set; }
        public int? Rating { get; set; }
        public string? Comment { get; set; }
    }
}
