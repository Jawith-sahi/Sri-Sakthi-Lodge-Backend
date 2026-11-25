using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace SriSakthiLodge_API.Services
{
    public class PaymentService
    {
        private readonly DbConnection _dbFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PaymentService> _logger;

        public PaymentService(DbConnection dbFactory, IConfiguration config, ILogger<PaymentService> logger)
        {
            _dbFactory = dbFactory;
            _config = config;
            _logger = logger;
        }

        public async Task<RazorpayOrderResponse?> CreateOrderAsync(CreateOrderRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_GetByReference_Public", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Reference", req.BookingReference);
            cmd.Parameters.AddWithValue("p_Token", DBNull.Value);

            using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) return null;

            var totalAmount = r.GetDecimal("TotalAmount");
            var amountPaid = r.GetDecimal("AmountPaid");
            var balanceAmount = totalAmount - amountPaid;
            var bookingId = r.GetInt32("Id");

            if (!r.IsClosed) await r.CloseAsync();

            var orderId = "order_mock_" + Guid.NewGuid().ToString("N")[..12];

            using var cmd2 = new MySqlCommand("sp_Payments_CreateOrder", conn) { CommandType = CommandType.StoredProcedure };
            cmd2.Parameters.AddWithValue("p_BookingId", bookingId);
            cmd2.Parameters.AddWithValue("p_RazorpayOrderId", orderId);
            cmd2.Parameters.AddWithValue("p_Amount", balanceAmount);
            await cmd2.ExecuteNonQueryAsync();

            return new RazorpayOrderResponse
            {
                OrderId = orderId,
                Amount = balanceAmount,
                Currency = "INR",
                BookingReference = req.BookingReference,
                KeyId = _config["Razorpay:KeyId"] ?? ""
            };
        }

        public async Task<bool> VerifyPaymentAsync(VerifyPaymentRequest req)
        {
            var keySecret = _config["Razorpay:KeySecret"] ?? "";
            var text = $"{req.RazorpayOrderId}|{req.RazorpayPaymentId}";
            var valid = true;

            if (!string.IsNullOrEmpty(keySecret) && keySecret != "YourSecretHere")
            {
                using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
                var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(text));
                var generatedSignature = Convert.ToHexString(hash).ToLower();
                valid = generatedSignature.Equals(req.RazorpaySignature, StringComparison.OrdinalIgnoreCase);
            }

            if (!valid) return false;

            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Payments_Verify", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RazorpayOrderId", req.RazorpayOrderId);
            cmd.Parameters.AddWithValue("p_RazorpayPaymentId", req.RazorpayPaymentId);
            cmd.Parameters.AddWithValue("p_BookingReference", req.BookingReference);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<PaymentStatusResponse?> GetStatusAsync(string bookingReference)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Payments_GetStatus", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_BookingReference", bookingReference);

            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new PaymentStatusResponse
                {
                    BookingReference = bookingReference,
                    PaymentStatus = r.GetString("PaymentStatus"),
                    AmountPaid = r.GetDecimal("AmountPaid"),
                    TotalAmount = r.GetDecimal("TotalAmount")
                };
            }
            return null;
        }

        public async Task<UpiSettingsModel> GetUpiSettingsAsync()
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("SELECT ValueJson FROM sitecontent WHERE ContentKey = 'upi_settings' LIMIT 1", conn);
            var val = await cmd.ExecuteScalarAsync() as string;
            if (!string.IsNullOrEmpty(val))
            {
                try
                {
                    return System.Text.Json.JsonSerializer.Deserialize<UpiSettingsModel>(val, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                           ?? new UpiSettingsModel();
                }
                catch { }
            }

            return new UpiSettingsModel();
        }

        public async Task<bool> UpdateUpiSettingsAsync(UpiSettingsModel model, string username)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var json = System.Text.Json.JsonSerializer.Serialize(model);

            using var cmd = new MySqlCommand(@"
                INSERT INTO sitecontent (ContentKey, ValueJson, IsDraft, UpdatedAt, UpdatedBy)
                VALUES ('upi_settings', @json, 0, NOW(), @username)
                ON DUPLICATE KEY UPDATE ValueJson = @json, UpdatedAt = NOW(), UpdatedBy = @username", conn);

            cmd.Parameters.AddWithValue("@json", json);
            cmd.Parameters.AddWithValue("@username", username);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> SubmitUpiPaymentAsync(SubmitUpiPaymentRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Payments_SubmitUpi", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_BookingReference", req.BookingReference.Trim());
            cmd.Parameters.AddWithValue("p_Utr", req.Utr.Trim());
            cmd.Parameters.AddWithValue("p_Amount", req.Amount);

            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return r.GetInt32("Success") == 1;
            }
            return false;
        }

        public async Task<bool> VerifyUpiPaymentAdminAsync(int bookingId, VerifyUpiPaymentAdminRequest req, string username)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Payments_VerifyUpi_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_BookingId", bookingId);
            cmd.Parameters.AddWithValue("p_PaymentId", req.PaymentId);
            cmd.Parameters.AddWithValue("p_Approved", req.Approved ? 1 : 0);
            cmd.Parameters.AddWithValue("p_AdminUsername", username);
            cmd.Parameters.AddWithValue("p_Notes", (object?)req.Notes ?? DBNull.Value);

            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return r.GetInt32("Success") == 1;
            }
            return false;
        }
    }
}

