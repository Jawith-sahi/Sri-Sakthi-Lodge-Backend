using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class EnquiriesController : ControllerBase
    {
        private readonly EnquiryService _enquiryService;

        public EnquiriesController(EnquiryService enquiryService)
        {
            _enquiryService = enquiryService;
        }

        [HttpPost("enquiries")]
        public async Task<IActionResult> SubmitEnquiry([FromBody] SubmitEnquiryRequest req)
        {
            var id = await _enquiryService.SubmitAsync(req);
            return id != null ? Ok(new { id, message = "Enquiry submitted successfully." }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/enquiries")]
        public async Task<IActionResult> GetAdminEnquiries(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null) =>
            Ok(await _enquiryService.GetAllAdminAsync(page, pageSize, status, search));

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/enquiries/{id}")]
        public async Task<IActionResult> GetAdminEnquiry(int id)
        {
            var item = await _enquiryService.GetByIdAdminAsync(id);
            return item != null ? Ok(item) : NotFound();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPatch("admin/enquiries/{id}")]
        public async Task<IActionResult> UpdateEnquiry(int id, [FromBody] UpdateEnquiryRequest req)
        {
            var username = UserContext.GetUsername(User);
            return await _enquiryService.UpdateAsync(id, req, username) ? NoContent() : BadRequest();
        }
    }
}

