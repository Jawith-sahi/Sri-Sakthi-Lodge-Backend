namespace SriSakthiLodge_API.Model
{
    public class Review
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public int Rating { get; set; }
        public string? BookingReference { get; set; }
        public string ReviewText { get; set; } = "";
        public string Status { get; set; } = "pending";
        public DateTime CreatedAt { get; set; }
    }

    public class SubmitReviewRequest
    {
        public string Name { get; set; } = "";
        public int Rating { get; set; }
        public string? BookingReference { get; set; }
        public string ReviewText { get; set; } = "";
    }

    public class ModerateReviewRequest
    {
        public string Status { get; set; } = "";
    }
}

