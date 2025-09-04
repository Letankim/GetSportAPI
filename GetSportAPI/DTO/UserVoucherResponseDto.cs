namespace GetSportAPI.DTO
{
    public class UserVoucherResponseDto
    {
        public int UservoucherId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public int VoucherId { get; set; }
        public string VoucherCode { get; set; } = null!;
        public decimal Discountpercent { get; set; }
        public DateTime? Usedat { get; set; }
        public DateTime Assignedat { get; set; }
    }
}
