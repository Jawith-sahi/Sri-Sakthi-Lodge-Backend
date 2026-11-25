using Microsoft.IdentityModel.Tokens;
using SriSakthiLodge_API.Model;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SriSakthiLodge_API.Services
{
    public class JwtTokenService
    {
        private readonly IConfiguration _config;

        public JwtTokenService(IConfiguration config)
        {
            _config = config;
        }

        public (AuthResponse auth, RefreshTokenInfo refresh) GenerateTokenPair(
            int userId, string username, string name, string role, bool isPersistent)
        {
            var expiryMinutes = int.Parse(_config["Jwt:AccessTokenExpiryMinutes"] ?? "15");
            var slidingDays = int.Parse(_config["Jwt:RefreshTokenSlidingDays"] ?? "7");
            var absoluteDays = int.Parse(_config["Jwt:RefreshTokenAbsoluteDays"] ?? "30");

            var expiresAt = DateTime.UtcNow.AddMinutes(expiryMinutes);

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:SigningKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username),
                new Claim(ClaimTypes.Role, role),
                new Claim("name", name),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: _config["Jwt:Issuer"],
                audience: _config["Jwt:Audience"],
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds
            );

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

            // Refresh token — opaque cryptographically random string, stored hashed in DB
            var rawRefreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            var slidingExpiry = DateTime.UtcNow.AddDays(slidingDays);
            var absoluteExpiry = DateTime.UtcNow.AddDays(absoluteDays);

            return (
                new AuthResponse
                {
                    UserId = userId,
                    Username = username,
                    Name = name,
                    Role = role,
                    AccessToken = accessToken,
                    ExpiresAt = expiresAt
                },
                new RefreshTokenInfo
                {
                    Token = rawRefreshToken,
                    ExpiresAt = slidingExpiry,
                    AbsoluteExpiryAt = absoluteExpiry
                }
            );
        }

        public string HashToken(string rawToken)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
            return Convert.ToHexString(bytes).ToLower();
        }
    }
}

