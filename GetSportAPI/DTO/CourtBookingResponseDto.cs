namespace GetSportAPI.DTO
{
    public class CourtBookingResponseDto
    {
        public int BookingId { get; set; }
        public int UserId { get; set; }
        public int CourtId { get; set; }
        public int SlotId { get; set; }
        public DateTime Bookingdate { get; set; }
        public string? Status { get; set; }
        public decimal Amount { get; set; }
        public DateTime Createat { get; set; }
    }
}
