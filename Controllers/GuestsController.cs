using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class GuestsController : ControllerBase
    {
        private readonly GuestService _guestService;

        public GuestsController(GuestService guestService)
        {
            _guestService = guestService;
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/guests")]
        public async Task<IActionResult> GetGuests([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null) =>
            Ok(await _guestService.GetAllAsync(page, pageSize, search));

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/guests/{id}")]
        public async Task<IActionResult> GetGuest(int id)
        {
            var g = await _guestService.GetByIdAsync(id);
            return g != null ? Ok(g) : NotFound();
        }
    }
}

