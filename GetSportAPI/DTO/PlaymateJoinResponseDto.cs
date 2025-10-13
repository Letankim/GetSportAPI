namespace GetSportAPI.DTO
{
    public class PlaymateJoinResponseDto
    {
        public int JoinId { get; set; }
        public int PostId { get; set; }
        public int CourtbookingId { get; set; }
        public int CourtId { get; set; }
        public string CourtName { get; set; } = null!;
        public string CourtLocation { get; set; } = null!;
        public List<string> CourtImageUrls { get; set; } = new List<string>();
        public DateTime Bookingdate { get; set; }
        public DateTime SlotStarttime { get; set; }
        public DateTime SlotEndtime { get; set; }
        public string PostTitle { get; set; } = null!;
        public string? PostSkilllevel { get; set; }
        public string PostStatus { get; set; } = null!;
        public int Neededplayers { get; set; }
        public int CurrentPlayers { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public DateTime Joinedat { get; set; }
    }
}
