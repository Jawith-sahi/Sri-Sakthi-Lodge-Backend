namespace SriSakthiLodge_API.Model
{
    public class Enquiry
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Email { get; set; }
        public string? Subject { get; set; }
        public string Message { get; set; } = "";
        public string Status { get; set; } = "new";
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class SubmitEnquiryRequest
    {
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Email { get; set; }
        public string? Subject { get; set; }
        public string Message { get; set; } = "";
    }

    public class UpdateEnquiryRequest
    {
        public string? Status { get; set; }
        public string? Notes { get; set; }
    }
}

