using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class GalleryController : ControllerBase
    {
        private readonly GalleryService _galleryService;

        public GalleryController(GalleryService galleryService)
        {
            _galleryService = galleryService;
        }

        [HttpGet("gallery")]
        public async Task<IActionResult> GetPublicGallery() => Ok(await _galleryService.GetAllPublicAsync());

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/gallery")]
        public async Task<IActionResult> GetAdminGallery(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? category = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null) =>
            Ok(await _galleryService.GetAllAdminPagedAsync(page, pageSize, search, status, category, sort, dir));

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/gallery")]
        public async Task<IActionResult> CreateGallery([FromBody] CreateGalleryRequest req)
        {
            var userId = UserContext.GetUserId(User);
            var id = await _galleryService.CreateAsync(req, userId);
            return id != null ? Ok(new { id }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPut("admin/gallery/{id}")]
        public async Task<IActionResult> UpdateGallery(int id, [FromBody] UpdateGalleryRequest req)
        {
            var userId = UserContext.GetUserId(User);
            return await _galleryService.UpdateAsync(id, req, userId) ? NoContent() : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpDelete("admin/gallery/{id}")]
        public async Task<IActionResult> DeleteGallery(int id) =>
            await _galleryService.DeleteAsync(id) ? NoContent() : BadRequest();

        // General image upload endpoint
        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/uploads")]
        public async Task<IActionResult> UploadImage(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest("File is empty.");

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/{fileName}";
            return Ok(new { url });
        }
    }
}
