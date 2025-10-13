namespace GetSportAPI.Params
{
    public class CourtBookingFilterParams
    {
        public string? Status { get; set; } 
        public string? Search { get; set; } 
        public decimal? MinAmount { get; set; } 
        public decimal? MaxAmount { get; set; }
        public DateTime? StartBookingDate { get; set; } 
        public DateTime? EndBookingDate { get; set; } 
        public string? SortBy { get; set; } = "Bookingdate"; 
        public string? SortOrder { get; set; } = "asc"; 
        public int Page { get; set; } = 1; 
        public int PageSize { get; set; } = 10; 
    }
}