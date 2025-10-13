namespace GetSportAPI.Params
{
    public class PlaymateJoinFilterParams
    {
        public int? PostId { get; set; } 
        public int? UserId { get; set; } 
        public DateTime? StartJoinedDate { get; set; } 
        public DateTime? EndJoinedDate { get; set; }
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "Joinedat"; 
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }
}