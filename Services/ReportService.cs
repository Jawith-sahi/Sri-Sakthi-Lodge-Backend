using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class ReportService
    {
        private readonly DbConnection _dbFactory;
        public ReportService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<DashboardSummary> GetDashboardSummaryAsync()
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reports_DashboardSummary", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new DashboardSummary
                {
                    TodayCheckIns = r.GetInt32("TodayCheckIns"),
                    TodayCheckOuts = r.GetInt32("TodayCheckOuts"),
                    CurrentlyOccupied = r.GetInt32("CurrentlyOccupied"),
                    PendingBookings = r.GetInt32("PendingBookings"),
                    NewEnquiries = r.GetInt32("NewEnquiries"),
                    RevenueThisMonth = r.GetDecimal("RevenueThisMonth"),
                    RevenueToday = r.GetDecimal("RevenueToday"),
                    TotalRooms = r.GetInt32("TotalRooms"),
                    OccupancyPercent = r.GetDecimal("OccupancyPercent")
                };
            }
            return new DashboardSummary();
        }

        public async Task<List<DailyReport>> GetDailyAsync(string date)
        {
            var list = new List<DailyReport>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reports_Daily", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Date", date);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new DailyReport
                {
                    Date = r.GetDateTime("Date").ToString("yyyy-MM-dd"),
                    CheckIns = r.GetInt32("CheckIns"),
                    CheckOuts = r.GetInt32("CheckOuts"),
                    NewBookings = r.GetInt32("NewBookings"),
                    Revenue = r.GetDecimal("Revenue")
                });
            }
            return list;
        }

        public async Task<List<MonthlyReport>> GetMonthlyAsync(int year, int month)
        {
            var list = new List<MonthlyReport>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reports_Monthly", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Year", year);
            cmd.Parameters.AddWithValue("p_Month", month);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new MonthlyReport
                {
                    Year = r.GetInt32("Year"),
                    Month = r.GetInt32("Month"),
                    MonthName = r.GetString("MonthName"),
                    TotalBookings = r.GetInt32("TotalBookings"),
                    TotalRevenue = r.GetDecimal("TotalRevenue"),
                    OccupancyPercent = r.GetDecimal("OccupancyPercent")
                });
            }
            return list;
        }

        public async Task<List<OccupancyReport>> GetOccupancyAsync(string from, string to)
        {
            var list = new List<OccupancyReport>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reports_Occupancy", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_From", from);
            cmd.Parameters.AddWithValue("p_To", to);
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new OccupancyReport
                {
                    RoomName = r.GetString("RoomName"),
                    TotalNights = r.GetInt32("TotalNights"),
                    OccupiedNights = r.GetInt32("OccupiedNights"),
                    OccupancyPercent = r.GetDecimal("OccupancyPercent")
                });
            }
            return list;
        }
    }
}

