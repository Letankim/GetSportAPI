using System.Security.Claims;
using GetSportAPI.DTO;

namespace GetSportAPI.Helpers
{
    public static class UserHelper
    {
        public static UserInfoDto? GetUserInfo(ClaimsPrincipal user)
        {
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
                return null;

            var idClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(idClaim) || !int.TryParse(idClaim, out int userId))
                return null;

            return new UserInfoDto
            {
                UserId = userId,
                Email = user.FindFirstValue(ClaimTypes.Email),
                Role = user.FindFirstValue(ClaimTypes.Role)
            };
        }

        public static bool IsInRole(ClaimsPrincipal user, string role)
        {
            var roleClaim = user.FindFirstValue(ClaimTypes.Role);
            return !string.IsNullOrEmpty(roleClaim) &&
                   roleClaim.Equals(role, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsAuthenticated(ClaimsPrincipal user)
        {
            return user?.Identity?.IsAuthenticated ?? false;
        }
    }
}
