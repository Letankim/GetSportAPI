namespace GetSportAPI.DTO
{
    public class PlaymateJoinResponseDto
    {
        public int JoinId { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public DateTime Joinedat { get; set; }
    }
}
