namespace GetSportAPI.DTO
{
    public class PackageResponseDto
    {
        public int PackageId { get; set; }
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal Price { get; set; }
        public int Durationdays { get; set; }
        public bool Isactive { get; set; }
        public DateTime Createat { get; set; }
        public DateTime? Updateat { get; set; }
    }
}
