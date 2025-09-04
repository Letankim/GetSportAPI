namespace GetSportAPI.DTO
{
    public class PlaymatePostResponseDto
    {
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public int? CourtbookingId { get; set; }
        public string Title { get; set; } = null!;
        public string? Content { get; set; }
        public int Neededplayers { get; set; }
        public int CurrentPlayers { get; set; }
        public string? Skilllevel { get; set; }
        public string Status { get; set; } = null!;
        public DateTime Createdat { get; set; }
    }
}
