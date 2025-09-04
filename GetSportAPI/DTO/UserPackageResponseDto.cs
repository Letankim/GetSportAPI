namespace GetSportAPI.DTO
{
    public class UserPackageResponseDto
    {
        public int UserpackageId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public int PackageId { get; set; }
        public string PackageName { get; set; } = null!;
        public DateOnly Startdate { get; set; }
        public DateOnly Enddate { get; set; }
        public bool Isactive { get; set; }
        public DateTime Createat { get; set; }
        public DateTime? Updateat { get; set; }
    }
}
