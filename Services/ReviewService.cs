using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class ReviewService
    {
        private readonly DbConnection _dbFactory;
        public ReviewService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<List<Review>> GetAllPublicAsync()
        {
            var list = new List<Review>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reviews_GetAll_Public", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                string? bRef = null;
                try
                {
                    var ord = r.GetOrdinal("BookingReference");
                    if (ord >= 0 && !r.IsDBNull(ord)) bRef = r.GetString(ord);
                }
                catch { }

                list.Add(new Review
                {
                    Id = r.GetInt32("Id"),
                    Name = r.GetString("Name"),
                    Rating = r.GetInt32("Rating"),
                    BookingReference = bRef,
                    ReviewText = r.GetString("ReviewText"),
                    Status = r.GetString("Status"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                });
            }
            return list;
        }

        public async Task<int?> SubmitAsync(SubmitReviewRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reviews_Submit", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Name", req.Name);
            cmd.Parameters.AddWithValue("p_Rating", req.Rating);
            cmd.Parameters.AddWithValue("p_ReviewText", req.ReviewText);
            cmd.Parameters.AddWithValue("p_BookingReference", (object?)req.BookingReference ?? DBNull.Value);
            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<List<Review>> GetAllAdminAsync()
        {
            var list = new List<Review>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reviews_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                string? bookingRef = null;
                try
                {
                    var ord = r.GetOrdinal("BookingReference");
                    if (ord >= 0 && !r.IsDBNull(ord)) bookingRef = r.GetString(ord);
                }
                catch { }

                list.Add(new Review
                {
                    Id = r.GetInt32("Id"),
                    Name = r.GetString("Name"),
                    Rating = r.GetInt32("Rating"),
                    BookingReference = bookingRef,
                    ReviewText = r.GetString("ReviewText"),
                    Status = r.GetString("Status"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                });
            }
            return list;
        }

        public async Task<PagedResult<Review>> GetAllAdminPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null, string? sort = null, string? dir = null)
        {
            var paged = new PagedResult<Review> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (Name LIKE @search OR ReviewText LIKE @search OR BookingReference LIKE @search)";
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                where += " AND Status = @status";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Reviews {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                if (!string.IsNullOrWhiteSpace(status)) countCmd.Parameters.AddWithValue("@status", status.Trim());
                paged.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                paged.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)paged.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var orderBy = "ORDER BY CreatedAt DESC";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var direction = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (sort.Equals("name", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY Name {direction}";
                else if (sort.Equals("rating", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY Rating {direction}";
                else if (sort.Equals("createdAt", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY CreatedAt {direction}";
            }

            var sql = $"SELECT Id, Name, Rating, BookingReference, ReviewText, Status, CreatedAt FROM Reviews {where} {orderBy} LIMIT @offset, @pageSize";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                if (!string.IsNullOrWhiteSpace(status)) cmd.Parameters.AddWithValue("@status", status.Trim());
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    string? bookingRef = null;
                    try
                    {
                        var ord = r.GetOrdinal("BookingReference");
                        if (ord >= 0 && !r.IsDBNull(ord)) bookingRef = r.GetString(ord);
                    }
                    catch { }

                    paged.Items.Add(new Review
                    {
                        Id = r.GetInt32("Id"),
                        Name = r.GetString("Name"),
                        Rating = r.GetInt32("Rating"),
                        BookingReference = bookingRef,
                        ReviewText = r.GetString("ReviewText"),
                        Status = r.GetString("Status"),
                        CreatedAt = r.GetDateTime("CreatedAt")
                    });
                }
            }

            return paged;
        }

        public async Task<bool> ModerateAsync(int id, ModerateReviewRequest req, string moderatedBy)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Reviews_Moderate", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Status", req.Status);
            cmd.Parameters.AddWithValue("p_ModeratedBy", moderatedBy);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}

