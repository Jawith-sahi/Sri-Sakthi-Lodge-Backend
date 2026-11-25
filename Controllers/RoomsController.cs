using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Exceptions;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class RoomsController : ControllerBase
    {
        private readonly RoomService _roomService;

        public RoomsController(RoomService roomService)
        {
            _roomService = roomService;
        }

        // Public Endpoints
        [HttpGet("rooms")]
        public async Task<IActionResult> GetRooms() => Ok(await _roomService.GetAllPublicAsync());

        [HttpGet("rooms/{slug}")]
        public async Task<IActionResult> GetRoomBySlug(string slug)
        {
            var room = await _roomService.GetBySlugAsync(slug);
            return room != null ? Ok(room) : NotFound();
        }

        [HttpGet("rooms/availability")]
        public async Task<IActionResult> CheckAvailability([FromQuery] RoomAvailabilityRequest req) =>
            Ok(await _roomService.CheckAvailabilityAsync(req));

        // Room Types Endpoints
        [HttpGet("room-types")]
        public async Task<IActionResult> GetRoomTypes([FromQuery] bool? activeOnly = true) =>
            Ok(await _roomService.GetRoomTypesAsync(activeOnly ?? true));

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/room-types")]
        public async Task<IActionResult> GetAdminRoomTypes([FromQuery] bool? activeOnly = false) =>
            Ok(await _roomService.GetRoomTypesAsync(activeOnly ?? false));

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/room-types")]
        public async Task<IActionResult> CreateRoomType([FromBody] CreateRoomTypeRequest req)
        {
            var roomType = await _roomService.CreateRoomTypeAsync(req);
            return roomType != null ? Ok(roomType) : BadRequest(new { message = "Failed to create room type." });
        }

        // Admin Endpoints
        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/rooms")]
        public async Task<IActionResult> GetAdminRooms(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null) =>
            Ok(await _roomService.GetAllAdminPagedAsync(page, pageSize, search, status, sort, dir));

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/rooms/{id}")]
        public async Task<IActionResult> GetAdminRoom(int id)
        {
            var room = await _roomService.GetByIdAdminAsync(id);
            return room != null ? Ok(room) : NotFound();
        }

        [Authorize(Roles = "owner")]
        [HttpPost("admin/rooms")]
        public async Task<IActionResult> CreateRoom([FromBody] CreateRoomRequest req)
        {
            var userId = UserContext.GetUserId(User);
            var id = await _roomService.CreateAsync(req, userId);
            return id != null ? Created($"/api/v1/admin/rooms/{id}", new { id }) : BadRequest();
        }

        [Authorize(Roles = "owner")]
        [HttpPut("admin/rooms/{id}")]
        public async Task<IActionResult> UpdateRoom(int id, [FromBody] UpdateRoomRequest req)
        {
            var userId = UserContext.GetUserId(User);
            return await _roomService.UpdateAsync(id, req, userId) ? NoContent() : BadRequest();
        }

        [Authorize(Roles = "owner")]
        [HttpPatch("admin/rooms/{id}/active")]
        public async Task<IActionResult> SetActive(int id, [FromBody] SetActiveRequest req)
        {
            var userId = UserContext.GetUserId(User);
            return await _roomService.SetActiveAsync(id, req.IsActive, userId) ? NoContent() : BadRequest();
        }

        [Authorize(Roles = "owner")]
        [HttpDelete("admin/rooms/{id}")]
        public async Task<IActionResult> DeleteRoom(int id)
        {
            var userId = UserContext.GetUserId(User);
            return await _roomService.DeleteAsync(id, userId) ? NoContent() : BadRequest();
        }

        // Room Images
        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/rooms/{id}/images")]
        public async Task<IActionResult> UploadImage(int id, IFormFile file, [FromForm] string? altEn, [FromForm] string? altTa)
        {
            if (file == null || file.Length == 0) throw new BadRequestException("File is empty.");

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "Uploads");
            if (!Directory.Exists(uploadsDir)) Directory.CreateDirectory(uploadsDir);

            var fileName = $"{Guid.NewGuid()}_{Path.GetFileName(file.FileName)}";
            var filePath = Path.Combine(uploadsDir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            try
            {
                var url = $"/uploads/{fileName}";
                var imageId = await _roomService.AddImageAsync(id, url, altEn, altTa);
                if (imageId == null)
                {
                    if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                    throw new BadRequestException("Failed to save room image record.");
                }
                return Ok(new { id = imageId, url });
            }
            catch
            {
                if (System.IO.File.Exists(filePath)) System.IO.File.Delete(filePath);
                throw;
            }
        }

        [Authorize(Roles = "owner,staff")]
        [HttpDelete("admin/rooms/{id}/images/{imageId}")]
        public async Task<IActionResult> DeleteImage(int id, int imageId) =>
            await _roomService.DeleteImageAsync(imageId) ? NoContent() : BadRequest();

        [Authorize(Roles = "owner,staff")]
        [HttpPut("admin/rooms/{id}/images/order")]
        public async Task<IActionResult> ReorderImages(int id, [FromBody] ReorderImagesRequest req) =>
            await _roomService.ReorderImagesAsync(id, req.Items) ? NoContent() : BadRequest();

        // Blocked Dates
        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/rooms/{id}/blocked-dates")]
        public async Task<IActionResult> GetBlockedDates(int id) => Ok(await _roomService.GetBlockedDatesAsync(id));

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/rooms/{id}/blocked-dates")]
        public async Task<IActionResult> AddBlockedDate(int id, [FromBody] BlockDateRequest req)
        {
            var blockId = await _roomService.AddBlockedDateAsync(id, req);
            return blockId != null ? Ok(new { id = blockId }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpDelete("admin/rooms/{id}/blocked-dates/{blockId}")]
        public async Task<IActionResult> RemoveBlockedDate(int id, int blockId) =>
            await _roomService.RemoveBlockedDateAsync(blockId) ? NoContent() : BadRequest();
    }

    public class SetActiveRequest
    {
        public bool IsActive { get; set; }
    }
}
