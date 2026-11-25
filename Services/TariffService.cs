using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class TariffService
    {
        private readonly DbConnection _dbFactory;
        public TariffService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<List<Tariff>> GetAllAsync()
        {
            var list = new List<Tariff>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Tariffs_GetAll", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new Tariff
                {
                    Id = r.GetInt32("Id"),
                    RoomId = r.GetInt32("RoomId"),
                    RoomName = r.GetString("RoomName"),
                    PlanName = r.GetString("PlanName"),
                    BaseRate = r.GetDecimal("BaseRate"),
                    StartDate = r.GetDateTime("StartDate").ToString("yyyy-MM-dd"),
                    EndDate = r.IsDBNull("EndDate") ? null : r.GetDateTime("EndDate").ToString("yyyy-MM-dd"),
                    Season = r.IsDBNull("Season") ? null : r.GetString("Season"),
                    TaxPercent = r.IsDBNull("TaxPercent") ? null : r.GetDecimal("TaxPercent"),
                    IsActive = r.GetBoolean("IsActive")
                });
            }
            return list;
        }

        public async Task<PagedResult<Tariff>> GetAllPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? sort = null, string? dir = null)
        {
            var paged = new PagedResult<Tariff> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (r.NameEn LIKE @search OR t.PlanName LIKE @search OR t.Season LIKE @search)";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Tariffs t JOIN Rooms r ON t.RoomId = r.Id {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                paged.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                paged.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)paged.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var orderBy = "ORDER BY t.Id DESC";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var direction = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (sort.Equals("planName", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY t.PlanName {direction}";
                else if (sort.Equals("baseRate", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY t.BaseRate {direction}";
                else if (sort.Equals("startDate", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY t.StartDate {direction}";
            }

            var sql = $@"
                SELECT t.Id, t.RoomId, r.NameEn AS RoomName, t.PlanName, t.BaseRate, t.StartDate, t.EndDate, t.Season, t.TaxPercent, t.IsActive
                FROM Tariffs t
                JOIN Rooms r ON t.RoomId = r.Id
                {where}
                {orderBy}
                LIMIT @offset, @pageSize";

            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    paged.Items.Add(new Tariff
                    {
                        Id = r.GetInt32("Id"),
                        RoomId = r.GetInt32("RoomId"),
                        RoomName = r.GetString("RoomName"),
                        PlanName = r.GetString("PlanName"),
                        BaseRate = r.GetDecimal("BaseRate"),
                        StartDate = r.GetDateTime("StartDate").ToString("yyyy-MM-dd"),
                        EndDate = r.IsDBNull("EndDate") ? null : r.GetDateTime("EndDate").ToString("yyyy-MM-dd"),
                        Season = r.IsDBNull("Season") ? null : r.GetString("Season"),
                        TaxPercent = r.IsDBNull("TaxPercent") ? null : r.GetDecimal("TaxPercent"),
                        IsActive = r.GetBoolean("IsActive")
                    });
                }
            }

            return paged;
        }

        public async Task<int?> CreateAsync(CreateTariffRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Tariffs_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_RoomId", req.RoomId);
            cmd.Parameters.AddWithValue("p_PlanName", req.PlanName);
            cmd.Parameters.AddWithValue("p_BaseRate", req.BaseRate);
            cmd.Parameters.AddWithValue("p_StartDate", req.StartDate);
            cmd.Parameters.AddWithValue("p_EndDate", (object?)req.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Season", (object?)req.Season ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TaxPercent", (object?)req.TaxPercent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsActive", req.IsActive);

            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateTariffRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Tariffs_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_RoomId", req.RoomId);
            cmd.Parameters.AddWithValue("p_PlanName", req.PlanName);
            cmd.Parameters.AddWithValue("p_BaseRate", req.BaseRate);
            cmd.Parameters.AddWithValue("p_StartDate", req.StartDate);
            cmd.Parameters.AddWithValue("p_EndDate", (object?)req.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Season", (object?)req.Season ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TaxPercent", (object?)req.TaxPercent ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsActive", req.IsActive);

            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Tariffs_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}

