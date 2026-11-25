using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class ReviewsController : ControllerBase
    {
        private readonly ReviewService _reviewService;

        public ReviewsController(ReviewService reviewService)
        {
            _reviewService = reviewService;
        }

        [HttpGet("reviews")]
        public async Task<IActionResult> GetPublicReviews() => Ok(await _reviewService.GetAllPublicAsync());

        [HttpPost("reviews")]
        public async Task<IActionResult> SubmitReview([FromBody] SubmitReviewRequest req)
        {
            var id = await _reviewService.SubmitAsync(req);
            return id != null ? Ok(new { id, message = "Review submitted for moderation." }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/reviews")]
        public async Task<IActionResult> GetAdminReviews(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null) =>
            Ok(await _reviewService.GetAllAdminPagedAsync(page, pageSize, search, status, sort, dir));

        [Authorize(Roles = "owner,staff")]
        [HttpPatch("admin/reviews/{id}/status")]
        public async Task<IActionResult> ModerateReview(int id, [FromBody] ModerateReviewRequest req)
        {
            var username = UserContext.GetUsername(User);
            return await _reviewService.ModerateAsync(id, req, username) ? NoContent() : BadRequest();
        }
    }
}

