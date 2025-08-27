namespace GetSportAPI.Models.Enum
{
    public static class UserRole
    {
        public const string Admin = "Admin";         
        public const string Staff = "Staff";        
        public const string Customer = "Customer";  
    }

    public static class UserStatus
    {
        public const string Active = "Active";
        public const string Inactive = "Inactive";
        public const string Pending = "Pending";
        public const string Banned = "Banned";
    }

    public static class BlogStatus
    {
        public const string Draft = "Draft";          
        public const string Published = "Published";
        public const string Banned = "Banned";
        public const string Deleted = "Deleted"; 
    }

    public static class CourtStatus
    {
        public const string Pending = "Pending";
        public const string Approved = "Approved";
        public const string Rejected = "Rejected";
        public const string Deleted = "Deleted";
    }

    public enum SlotDuration
    {
        ThirtyMinutes = 30,
        FortyFiveMinutes = 45,
        SixtyMinutes = 60,
        NinetyMinutes = 90,
        OneHundredTwentyMinutes = 120
    }

    public static class HostImageUrl
    {
        public const string Local = "https://localhost:7260/api/images/view/";
        public const string Production = "https://demo.com/images/";
    }
}
