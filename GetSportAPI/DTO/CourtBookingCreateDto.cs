namespace GetSportAPI.DTO
{
    public class CourtBookingCreateDto
    {
        public int CourtId { get; set; }
        public int SlotId { get; set; }
        public DateTime Bookingdate { get; set; }
        public string? Status { get; set; }
        public decimal Amount { get; set; }
    }
}
