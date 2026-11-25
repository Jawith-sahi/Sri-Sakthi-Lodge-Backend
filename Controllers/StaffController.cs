using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1/admin/staff")]
    public class StaffController : ControllerBase
    {
        private readonly StaffService _staffService;

        public StaffController(StaffService staffService)
        {
            _staffService = staffService;
        }

        [Authorize(Roles = "owner")]
        [HttpGet]
        public async Task<IActionResult> GetStaff() => Ok(await _staffService.GetAllAsync());

        [Authorize(Roles = "owner")]
        [HttpPost]
        public async Task<IActionResult> CreateStaff([FromBody] CreateStaffRequest req)
        {
            var userId = UserContext.GetUserId(User);
            var id = await _staffService.CreateAsync(req, userId);
            return id != null ? Ok(new { id }) : BadRequest();
        }

        [Authorize(Roles = "owner")]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateStaff(int id, [FromBody] UpdateStaffRequest req)
        {
            var userId = UserContext.GetUserId(User);
            return await _staffService.UpdateAsync(id, req, userId) ? NoContent() : BadRequest();
        }
    }
}

