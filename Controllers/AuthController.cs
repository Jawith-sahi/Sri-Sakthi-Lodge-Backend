using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1/auth")]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;
        private readonly ILogger<AuthController> _logger;

        public AuthController(AuthService authService, ILogger<AuthController> logger)
        {
            _authService = authService;
            _logger = logger;
        }

        private string GetIp() => HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        private CookieOptions GetCookieOptions(bool isPersistent, DateTime? expiresAt = null)
        {
            var isHttps = Request.IsHttps;
            var opts = new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/"
            };
            if (isPersistent && expiresAt.HasValue)
            {
                opts.Expires = expiresAt.Value;
            }
            return opts;
        }

        private void SetAccessCookie(string token, DateTime expiresAt, bool isPersistent)
        {
            Response.Cookies.Append("AuthToken", token, GetCookieOptions(isPersistent, expiresAt));
        }

        private void SetRefreshCookie(string token, DateTime expiresAt, bool isPersistent)
        {
            Response.Cookies.Append("RefreshToken", token, GetCookieOptions(isPersistent, expiresAt));
        }

        private void ClearCookies()
        {
            var opts = GetCookieOptions(false);
            opts.Expires = DateTime.UtcNow.AddDays(-1);
            Response.Cookies.Delete("AuthToken", opts);
            Response.Cookies.Delete("RefreshToken", opts);
        }

        // POST /api/v1/auth/login
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            _logger.LogInformation("LOGIN ATTEMPT Username={Username}", req.Username);

            var result = await _authService.LoginAsync(req, GetIp());

            if (result.Failure.HasValue)
            {
                _logger.LogWarning("LOGIN FAILED Username={Username} Reason={Reason}", req.Username, result.Failure);
                return Unauthorized(new { message = "Incorrect username or password." });
            }

            var auth = result.Auth!;
            var refresh = result.Refresh!;

            SetAccessCookie(auth.AccessToken, auth.ExpiresAt, req.RememberMe);
            SetRefreshCookie(refresh.Token, refresh.ExpiresAt, req.RememberMe);

            _logger.LogInformation("LOGIN SUCCESS Username={Username} Role={Role}", auth.Username, auth.Role);

            return Ok(new
            {
                id = auth.UserId,
                name = auth.Name,
                username = auth.Username,
                role = auth.Role,
                token = auth.AccessToken,
                accessToken = auth.AccessToken,
                expiresAt = auth.ExpiresAt
            });
        }

        // POST /api/v1/auth/logout
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            if (Request.Cookies.TryGetValue("RefreshToken", out var refreshToken))
                await _authService.LogoutAsync(refreshToken, GetIp());

            ClearCookies();
            _logger.LogInformation("LOGOUT SUCCESS");
            return Ok(new { message = "Logged out." });
        }

        // GET /api/v1/auth/me
        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var userId = UserContext.GetUserId(User);
            var user = await _authService.GetByIdAsync(userId);
            if (user == null) return NotFound(new { message = "User not found." });

            return Ok(new
            {
                id = user.Id,
                name = user.Name,
                username = user.Username,
                role = user.Role
            });
        }

        // POST /api/v1/auth/refresh
        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            if (!Request.Cookies.TryGetValue("RefreshToken", out var refreshToken))
            {
                _logger.LogWarning("REFRESH FAILED: missing cookie");
                return Unauthorized(new { message = "Missing refresh token." });
            }

            var result = await _authService.RefreshAsync(refreshToken, GetIp());
            if (result == null)
            {
                ClearCookies();
                return Unauthorized(new { message = "Session expired. Please sign in again." });
            }

            var (auth, newRefresh) = result.Value;
            bool isPersistent = newRefresh.AbsoluteExpiryAt > DateTime.UtcNow.AddDays(1);

            SetAccessCookie(auth.AccessToken, auth.ExpiresAt, isPersistent);
            SetRefreshCookie(newRefresh.Token, newRefresh.ExpiresAt, isPersistent);

            _logger.LogInformation("TOKEN REFRESH SUCCESS");
            return Ok(new { message = "Refreshed.", accessToken = auth.AccessToken, token = auth.AccessToken, expiresAt = auth.ExpiresAt });
        }

        // POST /api/v1/auth/change-password
        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest req)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var userId = UserContext.GetUserId(User);
            var success = await _authService.ChangePasswordAsync(userId, req);

            if (!success)
                return BadRequest(new { message = "Current password is incorrect." });

            return Ok(new { message = "Password changed successfully." });
        }
    }
}

