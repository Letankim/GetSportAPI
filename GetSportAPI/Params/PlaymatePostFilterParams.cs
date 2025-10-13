namespace GetSportAPI.Params
{
    public class PlaymatePostFilterParams
    {
        public int? CourtbookingId { get; set; } 
        public int? UserId { get; set; } 
        public string? Status { get; set; } 
        public string? Skilllevel { get; set; } 
        public int? MinNeededPlayers { get; set; } 
        public int? MaxNeededPlayers { get; set; }
        public DateTime? StartCreateDate { get; set; } 
        public DateTime? EndCreateDate { get; set; } 
        public string? Search { get; set; } 
        public string? SortBy { get; set; } = "Createdat"; 
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1; 
        public int PageSize { get; set; } = 10; 
    }
}