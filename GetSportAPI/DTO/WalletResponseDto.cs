namespace GetSportAPI.DTO
{
    public class WalletResponseDto
    {
        public int WalletId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public decimal Balance { get; set; }
        public DateTime Createdat { get; set; }
        public DateTime? Updatedat { get; set; }
    }
}
