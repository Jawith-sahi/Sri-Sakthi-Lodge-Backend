using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class BookingService
    {
        private readonly DbConnection _dbFactory;
        private readonly ILogger<BookingService> _logger;

        public BookingService(DbConnection dbFactory, ILogger<BookingService> logger)
        {
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public async Task<Booking?> CreateAsync(CreateBookingRequest req)
        {
            if (req.RoomNumbers != null && req.RoomNumbers.Count > 0)
            {
                req.RoomNumber = string.Join(", ", req.RoomNumbers);
                req.Rooms = Math.Max(req.Rooms, req.RoomNumbers.Count);
            }
            else if (!string.IsNullOrWhiteSpace(req.RoomNumber))
            {
                var parts = req.RoomNumber.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    req.Rooms = Math.Max(req.Rooms, parts.Length);
                }
            }

            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", req.RoomId);
            cmd.Parameters.AddWithValue("p_CheckIn", req.CheckIn);
            cmd.Parameters.AddWithValue("p_CheckOut", req.CheckOut);
            cmd.Parameters.AddWithValue("p_Adults", req.Adults);
            cmd.Parameters.AddWithValue("p_Children", req.Children);
            cmd.Parameters.AddWithValue("p_Rooms", req.Rooms);
            cmd.Parameters.AddWithValue("p_GuestName", req.GuestName);
            cmd.Parameters.AddWithValue("p_GuestPhone", req.GuestPhone);
            cmd.Parameters.AddWithValue("p_GuestEmail", (object?)req.GuestEmail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IdType", (object?)req.IdType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Notes", (object?)req.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IdempotencyKey", (object?)req.IdempotencyKey ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_RoomNumber", (object?)req.RoomNumber ?? DBNull.Value);

            Booking? booking = null;
            using (var r = await cmd.ExecuteReaderAsync())
            {
                booking = await ReadFullBooking(r);
            }

            if (booking != null)
            {
                var (advancePct, upiEnabled, allowPayAtHotel) = await GetUpiSettingsAsync(conn);

                // If user selected payAtHotel but allowPayAtHotel is disabled, force online advance payment
                var requestedMethod = req.PaymentMethod?.Trim();
                if (string.Equals(requestedMethod, "payAtHotel", StringComparison.OrdinalIgnoreCase) && !allowPayAtHotel)
                {
                    requestedMethod = "online";
                }

                if (string.Equals(requestedMethod, "online", StringComparison.OrdinalIgnoreCase)
                    && booking.TotalAmount > 0)
                {
                    booking.PaymentMethod = "online";
                    if (advancePct <= 0 || advancePct >= 100)
                    {
                        booking.AdvanceAmount = booking.TotalAmount; // 0% means full payment
                    }
                    else
                    {
                        booking.AdvanceAmount = Math.Round(booking.TotalAmount * (advancePct / 100.0m), 2);
                    }
                    booking.CanPay = true;
                }
                else
                {
                    booking.PaymentMethod = "payAtHotel";
                    booking.AdvanceAmount = 0;
                    booking.CanPay = false;
                }
            }

            return booking;
        }

        private static async Task<(int advancePercent, bool enabled, bool allowPayAtHotel)> GetUpiSettingsAsync(MySqlConnection conn)
        {
            try
            {
                using var cmd = new MySqlCommand("SELECT ValueJson FROM sitecontent WHERE ContentKey = 'upi_settings' LIMIT 1", conn);
                var val = await cmd.ExecuteScalarAsync() as string;
                if (!string.IsNullOrEmpty(val))
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(val);
                    var enabled = !doc.RootElement.TryGetProperty("Enabled", out var eProp) || eProp.GetBoolean();
                    var allowPay = doc.RootElement.TryGetProperty("AllowPayAtHotel", out var aProp) ? aProp.GetBoolean() : true;
                    var pct = doc.RootElement.TryGetProperty("AdvancePercent", out var pProp) ? pProp.GetInt32() : 30;
                    return (pct, enabled, allowPay);
                }
            }
            catch { }
            return (30, true, true);
        }

        public async Task<Booking?> GetByReferencePublicAsync(string reference, string? token)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_GetByReference_Public", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Reference", reference);
            cmd.Parameters.AddWithValue("p_Token", (object?)token ?? DBNull.Value);

            using var r = await cmd.ExecuteReaderAsync();
            return await ReadFullBooking(r);
        }

        public async Task<Booking?> LookupAsync(LookupBookingRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_Lookup", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Reference", req.Reference);
            cmd.Parameters.AddWithValue("p_Phone", req.Phone);

            using var r = await cmd.ExecuteReaderAsync();
            return await ReadFullBooking(r);
        }

        public async Task<bool> CancelPublicAsync(string reference, CancelBookingRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_Cancel_Public", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Reference", reference);
            cmd.Parameters.AddWithValue("p_Reason", (object?)req.Reason ?? DBNull.Value);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<PagedResult<BookingListItem>> GetAllAdminAsync(
            int page,
            int pageSize,
            string? status = null,
            string? search = null,
            string? paymentStatus = null,
            string? checkInFrom = null,
            string? checkInTo = null,
            string? sortKey = null,
            string? sortDir = null)
        {
            var result = new PagedResult<BookingListItem> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Page", page);
            cmd.Parameters.AddWithValue("p_PageSize", pageSize);
            cmd.Parameters.AddWithValue("p_Status", string.IsNullOrWhiteSpace(status) ? DBNull.Value : status.Trim());
            cmd.Parameters.AddWithValue("p_Search", string.IsNullOrWhiteSpace(search) ? DBNull.Value : search.Trim());
            cmd.Parameters.AddWithValue("p_PaymentStatus", string.IsNullOrWhiteSpace(paymentStatus) ? DBNull.Value : paymentStatus.Trim());

            object pFrom = DateTime.TryParse(checkInFrom, out var dFrom) ? dFrom.ToString("yyyy-MM-dd") : DBNull.Value;
            object pTo = DateTime.TryParse(checkInTo, out var dTo) ? dTo.ToString("yyyy-MM-dd") : DBNull.Value;
            cmd.Parameters.AddWithValue("p_CheckInFrom", pFrom);
            cmd.Parameters.AddWithValue("p_CheckInTo", pTo);

            cmd.Parameters.AddWithValue("p_SortKey", string.IsNullOrWhiteSpace(sortKey) ? DBNull.Value : sortKey.Trim());
            cmd.Parameters.AddWithValue("p_SortDir", string.IsNullOrWhiteSpace(sortDir) ? DBNull.Value : sortDir.Trim());

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var totalAmount = r.GetDecimal("TotalAmount");
                var balanceAmount = r.GetDecimal("BalanceAmount");
                decimal amountPaid = totalAmount - balanceAmount;
                try
                {
                    var ord = r.GetOrdinal("AmountPaid");
                    if (ord >= 0) amountPaid = r.GetDecimal(ord);
                }
                catch { }

                string? paymentRef = null;
                try
                {
                    var ordP = r.GetOrdinal("PaymentRef");
                    if (ordP >= 0 && !r.IsDBNull(ordP)) paymentRef = r.GetString(ordP);
                }
                catch { }

                string? roomNumber = null;
                try
                {
                    var ordR = r.GetOrdinal("RoomNumber");
                    if (ordR >= 0 && !r.IsDBNull(ordR)) roomNumber = r.GetString(ordR);
                }
                catch { }

                result.Items.Add(new BookingListItem
                {
                    Id = r.GetInt32("Id"),
                    Reference = r.GetString("Reference"),
                    RoomNumber = roomNumber,
                    PaymentRef = paymentRef,
                    GuestName = r.GetString("GuestName"),
                    GuestPhone = r.GetString("GuestPhone"),
                    RoomName = r.GetString("RoomName"),
                    CheckIn = r.GetDateTime("CheckIn").ToString("yyyy-MM-dd"),
                    CheckOut = r.GetDateTime("CheckOut").ToString("yyyy-MM-dd"),
                    Nights = r.GetInt32("Nights"),
                    Status = r.GetString("Status"),
                    PaymentStatus = r.GetString("PaymentStatus"),
                    TotalAmount = totalAmount,
                    AmountPaid = amountPaid,
                    BalanceAmount = balanceAmount,
                    Source = r.GetString("Source"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                });
            }

            if (await r.NextResultAsync() && await r.ReadAsync())
            {
                result.TotalCount = r.GetInt32("TotalCount");
                result.TotalPages = (int)Math.Ceiling((double)result.TotalCount / pageSize);
            }

            return result;
        }

        public async Task<Booking?> GetByIdAdminAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_GetById_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);

            using var r = await cmd.ExecuteReaderAsync();
            return await ReadFullBooking(r);
        }

        public async Task<Booking?> CreateAdminAsync(CreateBookingRequest req, int staffUserId, string staffUsername)
        {
            if (req.RoomNumbers != null && req.RoomNumbers.Count > 0)
            {
                req.RoomNumber = string.Join(", ", req.RoomNumbers);
                req.Rooms = Math.Max(req.Rooms, req.RoomNumbers.Count);
            }
            else if (!string.IsNullOrWhiteSpace(req.RoomNumber))
            {
                var parts = req.RoomNumber.Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    req.Rooms = Math.Max(req.Rooms, parts.Length);
                }
            }

            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_Create_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", req.RoomId);
            cmd.Parameters.AddWithValue("p_CheckIn", req.CheckIn);
            cmd.Parameters.AddWithValue("p_CheckOut", req.CheckOut);
            cmd.Parameters.AddWithValue("p_Adults", req.Adults);
            cmd.Parameters.AddWithValue("p_Children", req.Children);
            cmd.Parameters.AddWithValue("p_Rooms", req.Rooms);
            cmd.Parameters.AddWithValue("p_GuestName", req.GuestName);
            cmd.Parameters.AddWithValue("p_GuestPhone", req.GuestPhone);
            cmd.Parameters.AddWithValue("p_GuestEmail", (object?)req.GuestEmail ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IdType", (object?)req.IdType ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Notes", (object?)req.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_StaffUserId", staffUserId);
            cmd.Parameters.AddWithValue("p_StaffUsername", staffUsername);
            cmd.Parameters.AddWithValue("p_RoomNumber", (object?)req.RoomNumber ?? DBNull.Value);

            using var r = await cmd.ExecuteReaderAsync();
            return await ReadFullBooking(r);
        }

        public async Task<bool> UpdateStatusAsync(int id, UpdateBookingStatusRequest req, string staffUsername)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            if (string.Equals(req.Status, "checked_out", StringComparison.OrdinalIgnoreCase))
            {
                using var checkCmd = new MySqlCommand("SELECT PaymentStatus FROM Bookings WHERE Id = @Id", conn);
                checkCmd.Parameters.AddWithValue("@Id", id);
                var payStatus = (await checkCmd.ExecuteScalarAsync())?.ToString();
                if (!string.Equals(payStatus, "paid", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Checkout cannot be completed until the booking has a fully paid payment status.");
                }
            }

            using var cmd = new MySqlCommand("sp_Bookings_UpdateStatus", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Status", req.Status);
            cmd.Parameters.AddWithValue("p_Notes", (object?)req.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_RoomNumber", (object?)req.RoomNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ChangedBy", staffUsername);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> UpdatePaymentStatusAsync(int id, UpdateBookingPaymentStatusRequest req, string staffUsername)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_UpdatePaymentStatus_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_PaymentStatus", req.PaymentStatus);
            cmd.Parameters.AddWithValue("p_AmountPaid", (object?)req.AmountPaid ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ChangedBy", staffUsername);
            cmd.Parameters.AddWithValue("p_Notes", (object?)req.Notes ?? DBNull.Value);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> CancelAdminAsync(int id, CancelBookingRequest req, string staffUsername)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Bookings_Cancel_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Reason", (object?)req.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ChangedBy", staffUsername);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<(bool Success, string? Error)> DeleteAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var checkCmd = new MySqlCommand("SELECT Status FROM Bookings WHERE Id = @Id;", conn);
            checkCmd.Parameters.AddWithValue("@Id", id);
            var statusObj = await checkCmd.ExecuteScalarAsync();
            if (statusObj == null)
            {
                return (false, "Booking not found.");
            }

            var status = statusObj.ToString()?.Trim().ToLowerInvariant();
            if (status != "cancelled" && status != "checked_out")
            {
                return (false, "Only Cancelled or Checked-out bookings can be deleted.");
            }

            using var delCmd = new MySqlCommand("DELETE FROM Bookings WHERE Id = @Id;", conn);
            delCmd.Parameters.AddWithValue("@Id", id);
            var rows = await delCmd.ExecuteNonQueryAsync();
            return (rows > 0, null);
        }

        private static async Task<Booking?> ReadFullBooking(MySqlDataReader r)
        {
            if (!await r.ReadAsync()) return null;

            var totalAmount = r.GetDecimal("TotalAmount");
            var balanceAmount = r.GetDecimal("BalanceAmount");
            decimal amountPaid = totalAmount - balanceAmount;
            try
            {
                var ord = r.GetOrdinal("AmountPaid");
                if (ord >= 0) amountPaid = r.GetDecimal(ord);
            }
            catch { }

            var booking = new Booking
            {
                Id = r.GetInt32("Id"),
                Reference = r.GetString("Reference"),
                Status = r.GetString("Status"),
                PaymentStatus = r.GetString("PaymentStatus"),
                Source = r.GetString("Source"),
                RoomId = r.GetInt32("RoomId"),
                RoomName = r.GetString("RoomName"),
                RoomNumber = r.IsDBNull("RoomNumber") ? null : r.GetString("RoomNumber"),
                CheckIn = r.GetDateTime("CheckIn").ToString("yyyy-MM-dd"),
                CheckOut = r.GetDateTime("CheckOut").ToString("yyyy-MM-dd"),
                Nights = r.GetInt32("Nights"),
                Adults = r.GetInt32("Adults"),
                Children = r.GetInt32("Children"),
                RoomsCount = r.GetInt32("RoomsCount"),
                TotalAmount = totalAmount,
                AdvanceAmount = amountPaid > 0 ? amountPaid : (totalAmount - balanceAmount > 0 ? totalAmount - balanceAmount : totalAmount),
                AmountPaid = amountPaid,
                BalanceAmount = balanceAmount,
                Currency = r.GetString("Currency"),
                Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
                CreatedAt = r.GetDateTime("CreatedAt"),
                Guest = new GuestInfo
                {
                    Id = r.GetInt32("GuestId"),
                    Name = r.GetString("GuestName"),
                    Phone = r.GetString("GuestPhone"),
                    Email = r.IsDBNull("GuestEmail") ? null : r.GetString("GuestEmail"),
                    IdType = r.IsDBNull("GuestIdType") ? null : r.GetString("GuestIdType"),
                    Notes = r.IsDBNull("GuestNotes") ? null : r.GetString("GuestNotes")
                }
            };

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    booking.Payments.Add(new PaymentRecord
                    {
                        Id = r.GetInt32("Id"),
                        Method = r.GetString("Method"),
                        Amount = r.GetDecimal("Amount"),
                        Status = r.GetString("Status"),
                        Reference = r.IsDBNull("Reference") ? null : r.GetString("Reference"),
                        PaidAt = r.IsDBNull("PaidAt") ? null : r.GetDateTime("PaidAt")
                    });
                }
            }

            if (await r.NextResultAsync())
            {
                while (await r.ReadAsync())
                {
                    booking.History.Add(new StatusHistoryItem
                    {
                        Status = r.GetString("Status"),
                        At = r.GetDateTime("ChangedAt"),
                        By = r.GetString("ChangedBy")
                    });
                }
            }

            return booking;
        }
    }
}

