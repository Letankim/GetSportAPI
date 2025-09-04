namespace GetSportAPI.DTO
{
    public class AccountResponseDto
    {
        public int UserId { get; set; }
        public string Role { get; set; } = null!;
        public string Fullname { get; set; } = null!;
        public string? Gender { get; set; }
        public string? Phonenumber { get; set; }
        public string? Email { get; set; }
        public DateOnly? Dateofbirth { get; set; }
        public string? Skilllevel { get; set; }
        public string? Membershiptype { get; set; }
        public int Totalpoint { get; set; }
        public DateTime Createat { get; set; }
        public bool Isactive { get; set; }
        public string? Status { get; set; }
        public decimal WalletBalance { get; set; }
    }
}
