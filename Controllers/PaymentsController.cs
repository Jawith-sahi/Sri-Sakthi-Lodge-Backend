using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Helpers;
using SriSakthiLodge_API.Model;
using SriSakthiLodge_API.Services;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1/payments")]
    public class PaymentsController : ControllerBase
    {
        private readonly PaymentService _paymentService;

        public PaymentsController(PaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpPost("orders")]
        public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest req)
        {
            var res = await _paymentService.CreateOrderAsync(req);
            return res != null ? Ok(res) : BadRequest();
        }

        [HttpPost("verify")]
        public async Task<IActionResult> Verify([FromBody] VerifyPaymentRequest req)
        {
            var success = await _paymentService.VerifyPaymentAsync(req);
            return success ? Ok(new { status = "success" }) : BadRequest(new { message = "Payment signature verification failed." });
        }

        [HttpGet("status")]
        public async Task<IActionResult> GetStatus([FromQuery] string bookingReference)
        {
            var res = await _paymentService.GetStatusAsync(bookingReference);
            return res != null ? Ok(res) : NotFound();
        }

        // --- UPI Endpoints ---

        [HttpGet("upi-config")]
        public async Task<IActionResult> GetPublicUpiConfig()
        {
            var config = await _paymentService.GetUpiSettingsAsync();
            return Ok(config);
        }

        [HttpPost("upi-submit")]
        public async Task<IActionResult> SubmitUpi([FromBody] SubmitUpiPaymentRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.BookingReference) || string.IsNullOrWhiteSpace(req.Utr))
            {
                return BadRequest(new { message = "Booking reference and UTR are required." });
            }

            var success = await _paymentService.SubmitUpiPaymentAsync(req);
            return success
                ? Ok(new { success = true, message = "UPI payment reference submitted successfully." })
                : BadRequest(new { success = false, message = "Could not record payment for this booking reference." });
        }

        [Authorize(Roles = "owner,staff")]
        [HttpGet("/api/v1/admin/payments/upi-config")]
        public async Task<IActionResult> GetAdminUpiConfig()
        {
            var config = await _paymentService.GetUpiSettingsAsync();
            return Ok(config);
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPut("/api/v1/admin/payments/upi-config")]
        public async Task<IActionResult> UpdateAdminUpiConfig([FromBody] UpiSettingsModel model)
        {
            var username = UserContext.GetUsername(User);
            var success = await _paymentService.UpdateUpiSettingsAsync(model, username);
            return success ? Ok(model) : BadRequest(new { message = "Failed to update UPI settings." });
        }

        [Authorize(Roles = "owner,staff")]
        [HttpPost("/api/v1/admin/bookings/{id}/verify-upi")]
        public async Task<IActionResult> VerifyUpiPaymentAdmin(int id, [FromBody] VerifyUpiPaymentAdminRequest req)
        {
            var username = UserContext.GetUsername(User);
            var success = await _paymentService.VerifyUpiPaymentAdminAsync(id, req, username);
            return success
                ? Ok(new { success = true, message = req.Approved ? "Payment verified and booking confirmed." : "Payment marked as rejected." })
                : BadRequest(new { success = false, message = "Could not verify UPI payment." });
        }
    }
}

