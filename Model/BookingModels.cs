namespace SriSakthiLodge_API.Model
{
    public class GuestInfo
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Email { get; set; }
        public string? IdType { get; set; }
        public string? Notes { get; set; }
    }

    public class PaymentRecord
    {
        public int Id { get; set; }
        public string Method { get; set; } = "";
        public decimal Amount { get; set; }
        public string Status { get; set; } = "";
        public string? Reference { get; set; }
        public DateTime? PaidAt { get; set; }
    }

    public class StatusHistoryItem
    {
        public string Status { get; set; } = "";
        public DateTime At { get; set; }
        public string By { get; set; } = "";
    }

    public class Booking
    {
        public int Id { get; set; }
        public string Reference { get; set; } = "";
        public string Status { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public string Source { get; set; } = "";
        public int RoomId { get; set; }
        public string RoomName { get; set; } = "";
        public string? RoomNumber { get; set; }
        public string CheckIn { get; set; } = "";
        public string CheckOut { get; set; } = "";
        public int Nights { get; set; }
        public int Adults { get; set; }
        public int Children { get; set; }
        public int Rooms { get; set; } = 1;
        public int RoomsCount { get => Rooms; set => Rooms = value; }
        public decimal TotalAmount { get; set; }
        public decimal AdvanceAmount { get; set; }   // amount due now for online/UPI payment
        public decimal AmountPaid { get; set; }
        public decimal BalanceAmount { get; set; }
        public bool CanPay { get; set; }             // true when booking is pending and payment is expected
        public string? PaymentMethod { get; set; }
        public string? AccessToken { get; set; }
        public string Currency { get; set; } = "INR";
        public GuestInfo? Guest { get; set; }
        public List<PaymentRecord> Payments { get; set; } = new();
        public List<StatusHistoryItem> History { get; set; } = new();
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class GuestRequestInfo
    {
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Email { get; set; }
        public string? IdType { get; set; }
        public string? ArrivalTime { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateBookingRequest
    {
        private string? _guestName;
        private string? _guestPhone;
        private string? _guestEmail;
        private string? _notes;

        public int RoomId { get; set; }
        public string CheckIn { get; set; } = "";
        public string CheckOut { get; set; } = "";
        public int Adults { get; set; } = 1;
        public int Children { get; set; } = 0;
        public int Rooms { get; set; } = 1;
        public string? RoomNumber { get; set; }
        public List<string>? RoomNumbers { get; set; }
        public string? PaymentMethod { get; set; }

        public GuestRequestInfo? Guest { get; set; }

        public string GuestName
        {
            get => !string.IsNullOrWhiteSpace(_guestName) ? _guestName : Guest?.Name ?? "";
            set => _guestName = value;
        }

        public string GuestPhone
        {
            get => !string.IsNullOrWhiteSpace(_guestPhone) ? _guestPhone : Guest?.Phone ?? "";
            set => _guestPhone = value;
        }

        public string? GuestEmail
        {
            get => _guestEmail ?? Guest?.Email;
            set => _guestEmail = value;
        }

        public string? IdType { get; set; }

        public string? Notes
        {
            get => _notes ?? Guest?.Notes;
            set => _notes = value;
        }

        public string? IdempotencyKey { get; set; }
    }

    public class LookupBookingRequest
    {
        public string Reference { get; set; } = "";
        public string Phone { get; set; } = "";
    }

    public class CancelBookingRequest
    {
        public string? Reason { get; set; }
    }

    public class UpdateBookingStatusRequest
    {
        public string Status { get; set; } = "";
        public string? Notes { get; set; }
        public string? RoomNumber { get; set; }
    }

    public class UpdateBookingPaymentStatusRequest
    {
        public string PaymentStatus { get; set; } = "";
        public decimal? AmountPaid { get; set; }
        public string? Notes { get; set; }
    }

    public class BookingListItem
    {
        public int Id { get; set; }
        public string Reference { get; set; } = "";
        public string? RoomNumber { get; set; }
        public string? PaymentRef { get; set; }
        public string GuestName { get; set; } = "";
        public string GuestPhone { get; set; } = "";
        public string RoomName { get; set; } = "";
        public string CheckIn { get; set; } = "";
        public string CheckOut { get; set; } = "";
        public int Nights { get; set; }
        public string Status { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public decimal TotalAmount { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceAmount { get; set; }
        public string Source { get; set; } = "";
        public DateTime CreatedAt { get; set; }
    }

    public class PagedResult<T>
    {
        public List<T> Items { get; set; } = new();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages { get; set; }
    }
}

