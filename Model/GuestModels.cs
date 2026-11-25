namespace SriSakthiLodge_API.Model
{
    public class Guest
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Phone { get; set; } = "";
        public string? Email { get; set; }
        public string? IdType { get; set; }
        public string? Notes { get; set; }
        public int TotalBookings { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

