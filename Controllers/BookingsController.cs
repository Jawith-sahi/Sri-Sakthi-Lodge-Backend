using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1")]
    public class BookingsController : ControllerBase
    {
        private readonly BookingService _bookingService;

        public BookingsController(BookingService bookingService)
        {
            _bookingService = bookingService;
        }

        [HttpPost("bookings")]
        public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest req, [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey)
        {
            req.IdempotencyKey = idempotencyKey;
            var booking = await _bookingService.CreateAsync(req);
            return booking != null ? Ok(booking) : BadRequest(new { message = "Booking creation failed or room not available." });
        }

        [HttpGet("bookings/public/{reference}")]
        public async Task<IActionResult> GetPublicBooking(string reference, [FromQuery] string? token)
        {
            var booking = await _bookingService.GetByReferencePublicAsync(reference, token);
            return booking != null ? Ok(booking) : NotFound();
        }

        [HttpPost("bookings/lookup")]
        public async Task<IActionResult> LookupBooking([FromBody] LookupBookingRequest req)
        {
            var booking = await _bookingService.LookupAsync(req);
            return booking != null ? Ok(booking) : NotFound(new { message = "Booking not found with provided reference and phone." });
        }

        [HttpPost("bookings/{reference}/cancel")]
        public async Task<IActionResult> CancelPublicBooking(string reference, [FromBody] CancelBookingRequest req)
        {
            return await _bookingService.CancelPublicAsync(reference, req) ? Ok(new { message = "Booking cancelled successfully." }) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/bookings")]
        public async Task<IActionResult> GetAdminBookings(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? status = null,
            [FromQuery] string? search = null,
            [FromQuery] string? paymentStatus = null,
            [FromQuery] string? from = null,
            [FromQuery] string? checkInFrom = null,
            [FromQuery] string? to = null,
            [FromQuery] string? checkInTo = null,
            [FromQuery] string? sort = null,
            [FromQuery] string? dir = null)
        {
            var effectiveFrom = !string.IsNullOrWhiteSpace(from) ? from : checkInFrom;
            var effectiveTo = !string.IsNullOrWhiteSpace(to) ? to : checkInTo;
            return Ok(await _bookingService.GetAllAdminAsync(page, pageSize, status, search, paymentStatus, effectiveFrom, effectiveTo, sort, dir));
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("admin/bookings/{id}")]
        public async Task<IActionResult> GetAdminBooking(int id)
        {
            var b = await _bookingService.GetByIdAdminAsync(id);
            return b != null ? Ok(b) : NotFound();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/bookings")]
        public async Task<IActionResult> CreateAdminBooking([FromBody] CreateBookingRequest req)
        {
            var (userId, _, username) = UserContext.GetUserContext(User);
            var booking = await _bookingService.CreateAdminAsync(req, userId, username);
            return booking != null ? Ok(booking) : BadRequest();
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPatch("admin/bookings/{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(int id, [FromBody] UpdateBookingStatusRequest req)
        {
            var username = UserContext.GetUsername(User);
            var success = await _bookingService.UpdateStatusAsync(id, req, username);
            if (!success) return BadRequest(new { message = "Failed to update booking status." });
            var updated = await _bookingService.GetByIdAdminAsync(id);
            return Ok(updated);
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPatch("admin/bookings/{id}/payment-status")]
        public async Task<IActionResult> UpdateBookingPaymentStatus(int id, [FromBody] UpdateBookingPaymentStatusRequest req)
        {
            var username = UserContext.GetUsername(User);
            var success = await _bookingService.UpdatePaymentStatusAsync(id, req, username);
            if (!success) return BadRequest(new { message = "Failed to update booking payment status." });
            var updated = await _bookingService.GetByIdAdminAsync(id);
            return Ok(updated);
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPost("admin/bookings/{id}/cancel")]
        public async Task<IActionResult> CancelAdminBooking(int id, [FromBody] CancelBookingRequest req)
        {
            var username = UserContext.GetUsername(User);
            var success = await _bookingService.CancelAdminAsync(id, req, username);
            if (!success) return BadRequest(new { message = "Failed to cancel booking." });
            var updated = await _bookingService.GetByIdAdminAsync(id);
            return Ok(updated);
        }

        [Authorize(Roles = "owner,staff")]
        [HttpDelete("admin/bookings/{id}")]
        public async Task<IActionResult> DeleteBooking(int id)
        {
            var (success, error) = await _bookingService.DeleteAsync(id);
            if (!success) return BadRequest(new { message = error ?? "Failed to delete booking." });
            return NoContent();
        }
    }
}

