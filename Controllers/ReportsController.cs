using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SriSakthiLodge_API.Services;
using System.Text;

namespace SriSakthiLodge_API.Controllers
{
    [ApiController]
    [Route("api/v1/admin/reports")]
    public class ReportsController : ControllerBase
    {
        private readonly ReportService _reportService;
        private readonly BookingService _bookingService;

        public ReportsController(ReportService reportService, BookingService bookingService)
        {
            _reportService = reportService;
            _bookingService = bookingService;
        }

        [Authorize(Roles = "owner")]
        [HttpGet("dashboard-summary")]
        public async Task<IActionResult> GetDashboardSummary() => Ok(await _reportService.GetDashboardSummaryAsync());

        [Authorize(Roles = "owner")]
        [HttpGet("daily")]
        public async Task<IActionResult> GetDaily([FromQuery] string? from, [FromQuery] string? to, [FromQuery] string? date) =>
            Ok(await _reportService.GetDailyAsync(date ?? from ?? DateTime.UtcNow.ToString("yyyy-MM-dd")));

        [Authorize(Roles = "owner")]
        [HttpGet("monthly")]
        public async Task<IActionResult> GetMonthly([FromQuery] int? year, [FromQuery] int? month, [FromQuery] string? from, [FromQuery] string? to)
        {
            var y = year ?? DateTime.UtcNow.Year;
            var m = month ?? DateTime.UtcNow.Month;
            return Ok(await _reportService.GetMonthlyAsync(y, m));
        }

        [Authorize(Roles = "owner")]
        [HttpGet("occupancy")]
        public async Task<IActionResult> GetOccupancy([FromQuery] string? from = null, [FromQuery] string? to = null) =>
            Ok(await _reportService.GetOccupancyAsync(from ?? DateTime.UtcNow.AddDays(-30).ToString("yyyy-MM-dd"), to ?? DateTime.UtcNow.ToString("yyyy-MM-dd")));

        [Authorize(Roles = "owner")]
        [HttpGet("enquiries")]
        public async Task<IActionResult> GetEnquiries([FromQuery] string? from, [FromQuery] string? to) =>
            Ok(new { message = "Enquiry stats summary", from, to });

        [Authorize(Roles = "owner,staff")]
        [HttpGet("export")]
        public async Task<IActionResult> ExportReport(
            [FromQuery] string? type,
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] string? status = null,
            [FromQuery] string? paymentStatus = null,
            [FromQuery] string? search = null,
            [FromQuery] string format = "csv")
        {
            var reportType = (type ?? "bookings").ToLowerInvariant();
            if (reportType == "bookings")
            {
                var bookingsPaged = await _bookingService.GetAllAdminAsync(
                    page: 1,
                    pageSize: 10000,
                    status: status,
                    search: search,
                    paymentStatus: paymentStatus,
                    checkInFrom: from,
                    checkInTo: to
                );

                var sb = new StringBuilder();
                sb.AppendLine("Booking Ref,User Booking Ref (UTR),Guest Name,Phone,Source,Room,Check In,Check Out,Nights,Total Amount (INR),Amount Paid (INR),Balance Amount (INR),Payment Status,Booking Status,Created At (IST)");

                foreach (var b in bookingsPaged.Items)
                {
                    var utr = !string.IsNullOrEmpty(b.PaymentRef) && !b.PaymentRef.StartsWith("Manual") ? $"UTR: {b.PaymentRef}" : "";
                    var createdAtIst = b.CreatedAt.AddHours(5).AddMinutes(30).ToString("yyyy-MM-dd HH:mm:ss");

                    sb.AppendLine(string.Join(",",
                        EscapeCsv(b.Reference),
                        EscapeCsv(utr),
                        EscapeCsv(b.GuestName),
                        EscapeCsv(b.GuestPhone),
                        EscapeCsv(b.Source),
                        EscapeCsv(b.RoomName),
                        EscapeCsv(b.CheckIn),
                        EscapeCsv(b.CheckOut),
                        b.Nights,
                        b.TotalAmount,
                        b.AmountPaid,
                        b.BalanceAmount,
                        EscapeCsv(b.PaymentStatus),
                        EscapeCsv(b.Status),
                        EscapeCsv(createdAtIst)
                    ));
                }

                var bytes = Encoding.UTF8.GetBytes(sb.ToString());
                var filename = $"bookings-{DateTime.UtcNow:yyyyMMdd-HHmmss}.csv";
                return File(bytes, "text/csv; charset=utf-8", filename);
            }

            var csv = $"Type,From,To,GeneratedAt\n{reportType},{from},{to},{DateTime.UtcNow:O}";
            var defaultBytes = Encoding.UTF8.GetBytes(csv);
            var defaultFilename = $"report-{reportType}-{DateTime.UtcNow:yyyyMMdd}.csv";
            return File(defaultBytes, "text/csv; charset=utf-8", defaultFilename);
        }

        private static string EscapeCsv(string? field)
        {
            if (string.IsNullOrEmpty(field)) return "";
            if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            {
                return $"\"{field.Replace("\"", "\"\"")}\"";
            }
            return field;
        }
    }
}
