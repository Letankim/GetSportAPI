namespace GetSportAPI.DTO
{
    public class PointTransactionResponseDto
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public int? BookingId { get; set; }
        public int Pointchanged { get; set; }
        public string Transactiontype { get; set; } = null!;
        public string? Description { get; set; }
        public DateTime Createat { get; set; }
    }
}
