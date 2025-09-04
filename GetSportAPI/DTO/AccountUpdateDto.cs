namespace GetSportAPI.DTO
{
    public class AccountUpdateDto
    {
        public string? Fullname { get; set; }
        public string? Gender { get; set; }
        public string? Phonenumber { get; set; }
        public string? Email { get; set; }
        public DateOnly? Dateofbirth { get; set; }
        public string? Skilllevel { get; set; }
        public string? Membershiptype { get; set; }
        public string? Role { get; set; }
        public int? Totalpoint { get; set; } 
        public string? Status { get; set; } 
        public bool? Isactive { get; set; } 
    }
}
