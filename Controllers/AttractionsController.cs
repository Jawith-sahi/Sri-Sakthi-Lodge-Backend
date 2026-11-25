using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class AttractionsController : ControllerBase
    {
        private readonly AttractionService _attractionService;

        public AttractionsController(AttractionService attractionService)
        {
            _attractionService = attractionService;
        }

        [HttpGet("attractions")]
        public async Task<IActionResult> GetPublicAttractions() => Ok(await _attractionService.GetAllPublicAsync());

        [HttpGet("attractions/{slug}")]
        public async Task<IActionResult> GetAttractionBySlug(string slug)
        {
            var item = await _attractionService.GetBySlugAsync(slug);
            return item != null ? Ok(item) : NotFound();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/attractions")]
        public async Task<IActionResult> GetAdminAttractions(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null) =>
            Ok(await _attractionService.GetAllAdminPagedAsync(page, pageSize, search, status, sort, dir));

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/attractions")]
        public async Task<IActionResult> CreateAttraction([FromBody] CreateAttractionRequest req)
        {
            var userId = UserContext.GetUserId(User);
            var id = await _attractionService.CreateAsync(req, userId);
            return id != null ? Ok(new { id }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPut("admin/attractions/{id}")]
        public async Task<IActionResult> UpdateAttraction(int id, [FromBody] UpdateAttractionRequest req)
        {
            var userId = UserContext.GetUserId(User);
            return await _attractionService.UpdateAsync(id, req, userId) ? NoContent() : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpDelete("admin/attractions/{id}")]
        public async Task<IActionResult> DeleteAttraction(int id) =>
            await _attractionService.DeleteAsync(id) ? NoContent() : BadRequest();
    }
}

