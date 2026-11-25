using System.Security.Claims;

namespace SriSakthiLodge_API.Helpers
{
    /// <summary>
    /// Utility to extract user context from JWT claims in controllers.
    /// </summary>
    public static class UserContext
    {
        public static int GetUserId(ClaimsPrincipal user)
        {
            var claim = user.FindFirst(ClaimTypes.NameIdentifier) ?? user.FindFirst("sub");
            if (claim == null || !int.TryParse(claim.Value, out int userId))
                throw new UnauthorizedAccessException("Invalid or missing user ID in token.");
            return userId;
        }

        public static string GetRole(ClaimsPrincipal user)
        {
            var claim = user.FindFirst(ClaimTypes.Role);
            if (claim == null || string.IsNullOrWhiteSpace(claim.Value))
                throw new UnauthorizedAccessException("Invalid or missing role in token.");
            return claim.Value;
        }

        public static string GetUsername(ClaimsPrincipal user)
        {
            var claim = user.FindFirst(ClaimTypes.Name);
            return claim?.Value ?? "unknown";
        }

        public static (int userId, string role, string username) GetUserContext(ClaimsPrincipal user)
        {
            return (GetUserId(user), GetRole(user), GetUsername(user));
        }
    }
}

