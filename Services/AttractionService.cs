using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class AttractionService
    {
        private readonly DbConnection _dbFactory;
        public AttractionService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<List<Attraction>> GetAllPublicAsync()
        {
            var list = new List<Attraction>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Attractions_GetAll_Public", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(ReadAttraction(r));
            return list;
        }

        public async Task<Attraction?> GetBySlugAsync(string slug)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Attractions_GetBySlug", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Slug", slug);
            using var r = await cmd.ExecuteReaderAsync();
            return await r.ReadAsync() ? ReadAttraction(r) : null;
        }

        public async Task<List<Attraction>> GetAllAdminAsync()
        {
            var list = new List<Attraction>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Attractions_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(ReadAttraction(r));
            return list;
        }

        public async Task<PagedResult<Attraction>> GetAllAdminPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null, string? sort = null, string? dir = null)
        {
            var paged = new PagedResult<Attraction> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (NameEn LIKE @search OR NameTa LIKE @search OR Slug LIKE @search OR Category LIKE @search OR DescEn LIKE @search)";
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLowerInvariant();
                if (s == "published") where += " AND IsPublished = 1";
                else if (s == "draft") where += " AND IsPublished = 0";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Attractions {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                paged.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                paged.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)paged.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var orderBy = "ORDER BY SortOrder ASC, Id ASC";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var direction = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (sort.Equals("name", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY NameEn {direction}";
                else if (sort.Equals("category", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY Category {direction}";
                else if (sort.Equals("order", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY SortOrder {direction}";
            }

            var sql = $"SELECT Id, Slug, NameEn, NameTa, DescEn, DescTa, Category, ImageUrl, MapsUrl, IsPublished, SortOrder FROM Attractions {where} {orderBy} LIMIT @offset, @pageSize";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) paged.Items.Add(ReadAttraction(r));
            }

            return paged;
        }

        private static string GenerateSlug(string? customSlug, string nameEn)
        {
            var raw = !string.IsNullOrWhiteSpace(customSlug) ? customSlug.Trim() : (nameEn ?? "").Trim();
            var slug = System.Text.RegularExpressions.Regex.Replace(raw.ToLowerInvariant(), @"[^a-z0-9]+", "-").Trim('-');
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = "attraction-" + Guid.NewGuid().ToString("N")[..8];
            }
            return slug;
        }

        public async Task<int?> CreateAsync(CreateAttractionRequest req, int userId)
        {
            var slug = GenerateSlug(req.Slug, req.NameEn);
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Attractions_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Slug", slug);
            cmd.Parameters.AddWithValue("p_NameEn", req.NameEn);
            cmd.Parameters.AddWithValue("p_NameTa", req.NameTa);
            cmd.Parameters.AddWithValue("p_DescEn", (object?)req.DescEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescTa", (object?)req.DescTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Category", (object?)req.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ImageUrl", (object?)req.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_MapsUrl", (object?)req.MapsUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsPublished", req.IsPublished);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateAttractionRequest req, int userId)
        {
            var slug = GenerateSlug(req.Slug, req.NameEn);
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Attractions_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Slug", slug);
            cmd.Parameters.AddWithValue("p_NameEn", req.NameEn);
            cmd.Parameters.AddWithValue("p_NameTa", req.NameTa);
            cmd.Parameters.AddWithValue("p_DescEn", (object?)req.DescEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescTa", (object?)req.DescTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Category", (object?)req.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ImageUrl", (object?)req.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_MapsUrl", (object?)req.MapsUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsPublished", req.IsPublished);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Attractions_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static Attraction ReadAttraction(MySqlDataReader r) => new Attraction
        {
            Id = r.GetInt32("Id"),
            Slug = r.GetString("Slug"),
            Name = new LocalizedString { En = r.GetString("NameEn"), Ta = r.GetString("NameTa") },
            Description = new LocalizedString { En = r.IsDBNull("DescEn") ? "" : r.GetString("DescEn"), Ta = r.IsDBNull("DescTa") ? "" : r.GetString("DescTa") },
            Category = r.IsDBNull("Category") ? null : r.GetString("Category"),
            ImageUrl = r.IsDBNull("ImageUrl") ? null : r.GetString("ImageUrl"),
            MapsUrl = r.IsDBNull("MapsUrl") ? null : r.GetString("MapsUrl"),
            IsPublished = r.GetBoolean("IsPublished"),
            SortOrder = r.GetInt32("SortOrder")
        };
    }
}

