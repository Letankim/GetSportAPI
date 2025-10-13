namespace GetSportAPI.Params
{
    public class CourtFilterParams
    {
        public string? Status { get; set; }
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "Priority";
        public string? SortOrder { get; set; } = "asc";
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public DateOnly? StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
    }
}
