namespace GetSportAPI.Params
{
    public class CourtSlotFilterParams
    {
        public int? CourtId { get; set; } 
        public bool? IsAvailable { get; set; } 
        public DateTime? StartDateTime { get; set; }
        public DateTime? EndDateTime { get; set; } 
        public string? Search { get; set; } 
        public string? SortBy { get; set; } = "Starttime";
        public string? SortOrder { get; set; } = "asc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10; 
    }
}