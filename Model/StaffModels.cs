namespace SriSakthiLodge_API.Model
{
    public class StaffMember
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public string Name { get; set; } = "";
        public string Role { get; set; } = "";
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateStaffRequest
    {
        public string Username { get; set; } = "";
        public string Name { get; set; } = "";
        public string Password { get; set; } = "";
        public string Role { get; set; } = "staff";
    }

    public class UpdateStaffRequest
    {
        public string Name { get; set; } = "";
        public string Role { get; set; } = "staff";
        public bool IsActive { get; set; } = true;
    }
}

