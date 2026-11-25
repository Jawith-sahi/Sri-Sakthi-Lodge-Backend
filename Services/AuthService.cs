using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class AuthService
    {
        private readonly DbConnection _dbFactory;
        private readonly IPasswordHasher<object> _passwordHasher;
        private readonly JwtTokenService _jwt;
        private readonly ILogger<AuthService> _logger;

        public enum LoginFailureReason { UserNotFound, InvalidPassword, InactiveUser }
        public record LoginResult(AuthResponse? Auth, RefreshTokenInfo? Refresh, LoginFailureReason? Failure);

        public AuthService(DbConnection dbFactory, IPasswordHasher<object> passwordHasher, JwtTokenService jwt, ILogger<AuthService> logger)
        {
            _dbFactory = dbFactory;
            _passwordHasher = passwordHasher;
            _jwt = jwt;
            _logger = logger;
        }

        // ── Login ─────────────────────────────────────────────────────────────
        public async Task<LoginResult> LoginAsync(LoginRequest request, string ip)
        {
            _logger.LogInformation("LoginAsync called Username={Username}", request.Username);

            AdminUser? user = null;

            using (var conn = (MySqlConnection)_dbFactory.CreateConnection())
            {
                await conn.OpenAsync();
                using var cmd = new MySqlCommand("sp_Auth_Login", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("p_Username", request.Username);

                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    user = new AdminUser
                    {
                        Id = r.GetInt32("Id"),
                        Username = r.GetString("Username"),
                        Name = r.GetString("Name"),
                        PasswordHash = r.GetString("PasswordHash"),
                        Role = r.GetString("Role"),
                        IsActive = r.GetBoolean("IsActive")
                    };
                }
            }

            if (user == null)
            {
                _logger.LogWarning("LoginAsync: user not found Username={Username}", request.Username);
                return new LoginResult(null, null, LoginFailureReason.UserNotFound);
            }

            if (!user.IsActive)
            {
                _logger.LogWarning("LoginAsync: inactive user Username={Username}", request.Username);
                return new LoginResult(null, null, LoginFailureReason.InactiveUser);
            }

            var verifyResult = _passwordHasher.VerifyHashedPassword(new object(), user.PasswordHash, request.Password);
            if (verifyResult == PasswordVerificationResult.Failed)
            {
                _logger.LogWarning("LoginAsync: invalid password Username={Username}", request.Username);
                return new LoginResult(null, null, LoginFailureReason.InvalidPassword);
            }

            var (auth, refresh) = _jwt.GenerateTokenPair(user.Id, user.Username, user.Name, user.Role, request.RememberMe);

            await StoreRefreshTokenAsync(user.Id, refresh, ip);

            _logger.LogInformation("LoginAsync SUCCESS Username={Username} Role={Role}", user.Username, user.Role);
            return new LoginResult(auth, refresh, null);
        }

        // ── Refresh ───────────────────────────────────────────────────────────
        public async Task<(AuthResponse auth, RefreshTokenInfo refresh)?> RefreshAsync(string rawRefreshToken, string ip)
        {
            var tokenHash = _jwt.HashToken(rawRefreshToken);

            RefreshTokenRecord? record = null;
            AdminUser? user = null;

            using (var conn = (MySqlConnection)_dbFactory.CreateConnection())
            {
                await conn.OpenAsync();
                using var cmd = new MySqlCommand("sp_Auth_ValidateRefreshToken", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("p_TokenHash", tokenHash);

                using var r = await cmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    record = new RefreshTokenRecord
                    {
                        Id = r.GetInt32("Id"),
                        UserId = r.GetInt32("UserId"),
                        IsRevoked = r.GetBoolean("IsRevoked"),
                        SlidingExpiry = r.GetDateTime("SlidingExpiry"),
                        AbsoluteExpiry = r.GetDateTime("AbsoluteExpiry")
                    };
                }

                if (!r.IsClosed) await r.CloseAsync();

                if (record == null || record.IsRevoked || record.SlidingExpiry < DateTime.UtcNow || record.AbsoluteExpiry < DateTime.UtcNow)
                {
                    _logger.LogWarning("Refresh failed: token invalid/expired/revoked");
                    return null;
                }

                using var cmd2 = new MySqlCommand("sp_Staff_GetById", conn) { CommandType = CommandType.StoredProcedure };
                cmd2.Parameters.AddWithValue("p_Id", record.UserId);
                using var r2 = await cmd2.ExecuteReaderAsync();
                if (await r2.ReadAsync())
                {
                    user = new AdminUser
                    {
                        Id = r2.GetInt32("Id"),
                        Username = r2.GetString("Username"),
                        Name = r2.GetString("Name"),
                        Role = r2.GetString("Role"),
                        IsActive = r2.GetBoolean("IsActive")
                    };
                }
            }

            if (user == null || !user.IsActive) return null;

            var (newAuth, newRefresh) = _jwt.GenerateTokenPair(user.Id, user.Username, user.Name, user.Role, true);

            await RotateRefreshTokenAsync(record!.Id, tokenHash, _jwt.HashToken(newRefresh.Token), newRefresh, ip);

            _logger.LogInformation("Token refreshed for UserId={UserId}", user.Id);
            return (newAuth, newRefresh);
        }

        // ── Logout ────────────────────────────────────────────────────────────
        public async Task LogoutAsync(string rawRefreshToken, string ip)
        {
            var tokenHash = _jwt.HashToken(rawRefreshToken);
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Auth_RevokeToken", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_TokenHash", tokenHash);
            cmd.Parameters.AddWithValue("p_Reason", "logout");
            await cmd.ExecuteNonQueryAsync();
            _logger.LogInformation("Token revoked on logout");
        }

        // ── Change Password ───────────────────────────────────────────────────
        public async Task<bool> ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            string currentHash = "";
            using (var conn = (MySqlConnection)_dbFactory.CreateConnection())
            {
                await conn.OpenAsync();
                using var cmd = new MySqlCommand("sp_Auth_GetPasswordHash", conn) { CommandType = CommandType.StoredProcedure };
                cmd.Parameters.AddWithValue("p_UserId", userId);
                var result = await cmd.ExecuteScalarAsync();
                currentHash = result?.ToString() ?? "";
            }

            var verify = _passwordHasher.VerifyHashedPassword(new object(), currentHash, request.CurrentPassword);
            if (verify == PasswordVerificationResult.Failed) return false;

            var newHash = _passwordHasher.HashPassword(new object(), request.NewPassword);

            using var conn2 = (MySqlConnection)_dbFactory.CreateConnection();
            await conn2.OpenAsync();
            using var cmd2 = new MySqlCommand("sp_Auth_ChangePassword", conn2) { CommandType = CommandType.StoredProcedure };
            cmd2.Parameters.AddWithValue("p_UserId", userId);
            cmd2.Parameters.AddWithValue("p_NewPasswordHash", newHash);
            await cmd2.ExecuteNonQueryAsync();
            return true;
        }

        // ── GetMe ─────────────────────────────────────────────────────────────
        public async Task<AdminUser?> GetByIdAsync(int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Staff_GetById", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", userId);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new AdminUser
                {
                    Id = r.GetInt32("Id"),
                    Username = r.GetString("Username"),
                    Name = r.GetString("Name"),
                    Role = r.GetString("Role"),
                    IsActive = r.GetBoolean("IsActive")
                };
            }
            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private async Task StoreRefreshTokenAsync(int userId, RefreshTokenInfo refresh, string ip)
        {
            var hash = _jwt.HashToken(refresh.Token);
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Auth_CreateRefreshToken", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_UserId", userId);
            cmd.Parameters.AddWithValue("p_TokenHash", hash);
            cmd.Parameters.AddWithValue("p_IpAddress", ip);
            cmd.Parameters.AddWithValue("p_SlidingExpiry", refresh.ExpiresAt);
            cmd.Parameters.AddWithValue("p_AbsoluteExpiry", refresh.AbsoluteExpiryAt);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task RotateRefreshTokenAsync(int oldId, string oldHash, string newHash, RefreshTokenInfo newRefresh, string ip)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Auth_RotateRefreshToken", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_OldId", oldId);
            cmd.Parameters.AddWithValue("p_OldHash", oldHash);
            cmd.Parameters.AddWithValue("p_NewHash", newHash);
            cmd.Parameters.AddWithValue("p_NewSlidingExpiry", newRefresh.ExpiresAt);
            cmd.Parameters.AddWithValue("p_NewAbsoluteExpiry", newRefresh.AbsoluteExpiryAt);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

