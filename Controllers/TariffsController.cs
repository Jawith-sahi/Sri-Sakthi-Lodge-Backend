using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class TariffsController : ControllerBase
    {
        private readonly TariffService _tariffService;

        public TariffsController(TariffService tariffService)
        {
            _tariffService = tariffService;
        }

        [Authorize(Roles = "owner")]
        [HttpGet("admin/tariffs")]
        public async Task<IActionResult> GetTariffs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null) =>
            Ok(await _tariffService.GetAllPagedAsync(page, pageSize, search, sort, dir));

        [Authorize(Roles = "owner")]
        [HttpPost("admin/tariffs")]
        public async Task<IActionResult> CreateTariff([FromBody] CreateTariffRequest req)
        {
            var id = await _tariffService.CreateAsync(req);
            return id != null ? Ok(new { id }) : BadRequest();
        }

        [Authorize(Roles = "owner")]
        [HttpPut("admin/tariffs/{id}")]
        public async Task<IActionResult> UpdateTariff(int id, [FromBody] UpdateTariffRequest req) =>
            await _tariffService.UpdateAsync(id, req) ? NoContent() : BadRequest();

        [Authorize(Roles = "owner")]
        [HttpDelete("admin/tariffs/{id}")]
        public async Task<IActionResult> DeleteTariff(int id) =>
            await _tariffService.DeleteAsync(id) ? NoContent() : BadRequest();
    }
}

