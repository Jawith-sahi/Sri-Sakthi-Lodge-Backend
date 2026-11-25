namespace SriSakthiLodge_API.Model
{
    public class CreateOrderRequest
    {
        public string BookingReference { get; set; } = "";
    }

    public class RazorpayOrderResponse
    {
        public string OrderId { get; set; } = "";
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";
        public string BookingReference { get; set; } = "";
        public string KeyId { get; set; } = "";
    }

    public class VerifyPaymentRequest
    {
        public string RazorpayOrderId { get; set; } = "";
        public string RazorpayPaymentId { get; set; } = "";
        public string RazorpaySignature { get; set; } = "";
        public string BookingReference { get; set; } = "";
    }

    public class PaymentStatusResponse
    {
        public string BookingReference { get; set; } = "";
        public string PaymentStatus { get; set; } = "";
        public decimal AmountPaid { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class UpiSettingsModel
    {
        public bool Enabled { get; set; } = true;
        public bool AllowPayAtHotel { get; set; } = true;
        public string UpiId { get; set; } = "srisakthi@ybl";
        public string PayeeName { get; set; } = "Sri Sakthi Lodge";
        public string? QrCodeUrl { get; set; }
        public int AdvancePercent { get; set; } = 100;
        public string? Instructions { get; set; }
    }

    public class SubmitUpiPaymentRequest
    {
        public string BookingReference { get; set; } = "";
        public string Utr { get; set; } = "";
        public decimal Amount { get; set; }
    }

    public class VerifyUpiPaymentAdminRequest
    {
        public int PaymentId { get; set; }
        public bool Approved { get; set; }
        public string? Notes { get; set; }
    }
}

