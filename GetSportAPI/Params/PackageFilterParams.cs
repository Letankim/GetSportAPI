namespace GetSportAPI.Params
{
    public class PackageFilterParams
    {
        public string? Search { get; set; } 
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; } 
        public int? MinDurationDays { get; set; }
        public int? MaxDurationDays { get; set; } 
        public bool? IsActive { get; set; } 
        public DateTime? StartCreateDate { get; set; }
        public DateTime? EndCreateDate { get; set; }
        public DateTime? StartUpdateDate { get; set; }
        public DateTime? EndUpdateDate { get; set; }
        public string? SortBy { get; set; } = "Createat";
        public string? SortOrder { get; set; } = "asc";
        public int Page { get; set; } = 1; 
        public int PageSize { get; set; } = 10;
    }
}