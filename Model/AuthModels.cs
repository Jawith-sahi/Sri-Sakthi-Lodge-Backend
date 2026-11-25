namespace SriSakthiLodge_API.Model
{
    // ---- Requests ----
    public class LoginRequest
    {
        public string Username { get; set; } = "";
        public string Password { get; set; } = "";
        public bool RememberMe { get; set; } = false;
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; } = "";
        public string NewPassword { get; set; } = "";
    }

    // ---- Responses ----
    public class AuthResponse
    {
        public int UserId { get; set; }
        public string Username { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public string AccessToken { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
    }

    public class RefreshTokenInfo
    {
        public string Token { get; set; } = "";
        public DateTime ExpiresAt { get; set; }
        public DateTime AbsoluteExpiryAt { get; set; }
    }

    public class AdminUser
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Name { get; set; } = "";
        public string PasswordHash { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
    }

    public class RefreshTokenRecord
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string TokenHash { get; set; } = "";
        public bool IsRevoked { get; set; }
        public DateTime SlidingExpiry { get; set; }
        public DateTime AbsoluteExpiry { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

