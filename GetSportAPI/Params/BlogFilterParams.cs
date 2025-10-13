namespace GetSportAPI.Params
{
    public class BlogFilterParams
    {
        public string? Status { get; set; }
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "Createdat";
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
