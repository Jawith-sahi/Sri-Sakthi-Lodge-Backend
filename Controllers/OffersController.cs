using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class OffersController : ControllerBase
    {
        private readonly OfferService _offerService;

        public OffersController(OfferService offerService)
        {
            _offerService = offerService;
        }

        [HttpGet("offers")]
        public async Task<IActionResult> GetPublicOffers() => Ok(await _offerService.GetAllPublicAsync());

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/offers")]
        public async Task<IActionResult> GetAdminOffers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null) =>
            Ok(await _offerService.GetAllAdminPagedAsync(page, pageSize, search, status, sort, dir));

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/offers")]
        public async Task<IActionResult> CreateOffer([FromBody] CreateOfferRequest req)
        {
            var userId = UserContext.GetUserId(User);
            var id = await _offerService.CreateAsync(req, userId);
            return id != null ? Ok(new { id }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPut("admin/offers/{id}")]
        public async Task<IActionResult> UpdateOffer(int id, [FromBody] UpdateOfferRequest req)
        {
            var userId = UserContext.GetUserId(User);
            return await _offerService.UpdateAsync(id, req, userId) ? NoContent() : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpDelete("admin/offers/{id}")]
        public async Task<IActionResult> DeleteOffer(int id) =>
            await _offerService.DeleteAsync(id) ? NoContent() : BadRequest();
    }
}

