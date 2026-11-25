namespace SriSakthiLodge_API.Model
{
    public class DashboardSummary
    {
        public int TodayCheckIns { get; set; }
        public int TodayCheckOuts { get; set; }
        public int CurrentlyOccupied { get; set; }
        public int PendingBookings { get; set; }
        public int NewEnquiries { get; set; }
        public decimal RevenueThisMonth { get; set; }
        public decimal RevenueToday { get; set; }
        public int TotalRooms { get; set; }
        public decimal OccupancyPercent { get; set; }
    }

    public class DailyReport
    {
        public string Date { get; set; } = "";
        public int CheckIns { get; set; }
        public int CheckOuts { get; set; }
        public int NewBookings { get; set; }
        public decimal Revenue { get; set; }
    }

    public class MonthlyReport
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthName { get; set; } = "";
        public int TotalBookings { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal OccupancyPercent { get; set; }
    }

    public class OccupancyReport
    {
        public string RoomName { get; set; } = "";
        public int TotalNights { get; set; }
        public int OccupiedNights { get; set; }
        public decimal OccupancyPercent { get; set; }
    }
}

