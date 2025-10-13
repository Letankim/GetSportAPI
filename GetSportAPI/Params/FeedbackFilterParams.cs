namespace GetSportAPI.Params
{
    public class FeedbackFilterParams
    {
        public int? BookingId { get; set; } 
        public int? UserId { get; set; } 
        public int? MinRating { get; set; } 
        public int? MaxRating { get; set; }
        public DateTime? StartCreateDate { get; set; } 
        public DateTime? EndCreateDate { get; set; } 
        public string? Search { get; set; }
        public string? SortBy { get; set; } = "Createat"; 
        public string? SortOrder { get; set; } = "desc";
        public int Page { get; set; } = 1; 
        public int PageSize { get; set; } = 10; 
    }
}