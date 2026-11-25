using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class ContentController : ControllerBase
    {
        private readonly ContentService _contentService;

        public ContentController(ContentService contentService)
        {
            _contentService = contentService;
        }

        [HttpGet("content")]
        public async Task<IActionResult> GetPublicContent() => Content(await _contentService.GetPublicAsync(), "application/json");

        [Authorize(Roles = "owner")]
        [HttpGet("admin/content")]
        public async Task<IActionResult> GetAdminContent() => Content(await _contentService.GetAdminAsync(), "application/json");

        [Authorize(Roles = "owner")]
        [HttpPut("admin/content")]
        public async Task<IActionResult> UpdateContent([FromBody] System.Text.Json.JsonElement body)
        {
            var username = UserContext.GetUsername(User);
            string contentKey = "main";
            string valueJson = "";

            if (body.ValueKind == System.Text.Json.JsonValueKind.Object)
            {
                if (body.TryGetProperty("contentKey", out var ck) && ck.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    contentKey = ck.GetString() ?? "main";
                }
                if (body.TryGetProperty("valueJson", out var vj) && vj.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    valueJson = vj.GetString() ?? "";
                }
                else
                {
                    valueJson = body.GetRawText();
                }
            }
            else
            {
                valueJson = body.GetRawText();
            }

            var req = new UpdateContentRequest { ContentKey = string.IsNullOrWhiteSpace(contentKey) ? "main" : contentKey, ValueJson = valueJson };
            return await _contentService.UpdateAsync(req, username) ? NoContent() : BadRequest();
        }

        [Authorize(Roles = "owner")]
        [HttpPost("admin/content/publish")]
        public async Task<IActionResult> PublishContent()
        {
            var username = UserContext.GetUsername(User);
            return await _contentService.PublishAsync(username) ? NoContent() : BadRequest();
        }
    }
}

