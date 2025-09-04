namespace GetSportAPI.DTO
{
    public class PointTransactionCreateDto
    {
        public int? BookingId { get; set; }
        public int Pointchanged { get; set; }
        public string Transactiontype { get; set; } = null!;
        public string? Description { get; set; }
    }
}
