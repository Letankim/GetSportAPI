using Microsoft.Extensions.Hosting;

namespace GetSportAPI.Models.Enum
{
    public static class UserRole
    {
        public const string Admin = "Admin";
        public const string Staff = "Owner";
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
        public const string Local = "https://api.getsport.3docorp.vn/api/images/view/";
        public const string Production = "https://demo.com/images/";
    }

    public static class HostBookingUrl
    {
        private const string Local = "http://localhost:5173/booking/callback/";
        private const string Production = "https://demo.com/booking/callback/";

        public static string GetBaseUrl(HostEnvironment env)
        {
            return env switch
            {
                HostEnvironment.Local => Local,
                HostEnvironment.Production => Production,
                _ => throw new ArgumentOutOfRangeException(nameof(env), env, null)
            };
        }

        public static string GetCancelUrl(HostEnvironment env, int bookingId)
        {
            return $"{GetBaseUrl(env)}cancel?bookingId={bookingId}";
        }

        public static string GetSuccessUrl(HostEnvironment env, int bookingId)
        {
            return $"{GetBaseUrl(env)}success?bookingId={bookingId}";
        }

        public enum HostEnvironment
        {
            Local,
            Production
        }
    }
}
