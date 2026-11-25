using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class GalleryService
    {
        private readonly DbConnection _dbFactory;
        public GalleryService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<List<GalleryItem>> GetAllPublicAsync()
        {
            var list = new List<GalleryItem>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Gallery_GetAll_Public", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(ReadItem(r));
            return list;
        }

        public async Task<List<GalleryItem>> GetAllAdminAsync()
        {
            var list = new List<GalleryItem>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Gallery_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(ReadItem(r));
            return list;
        }

        public async Task<PagedResult<GalleryItem>> GetAllAdminPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null, string? category = null, string? sort = null, string? dir = null)
        {
            var paged = new PagedResult<GalleryItem> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (TitleEn LIKE @search OR TitleTa LIKE @search OR AltEn LIKE @search OR Category LIKE @search)";
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLowerInvariant();
                if (s == "published" || s == "active") where += " AND IsPublished = 1";
                else if (s == "draft" || s == "archived" || s == "inactive") where += " AND IsPublished = 0";
            }
            if (!string.IsNullOrWhiteSpace(category))
            {
                where += " AND Category = @category";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Gallery {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                if (!string.IsNullOrWhiteSpace(category)) countCmd.Parameters.AddWithValue("@category", category.Trim());
                paged.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                paged.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)paged.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var orderBy = "ORDER BY SortOrder ASC, Id ASC";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var direction = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (sort.Equals("title", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY TitleEn {direction}";
                else if (sort.Equals("category", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY Category {direction}";
                else if (sort.Equals("order", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY SortOrder {direction}";
            }

            var sql = $"SELECT Id, Type, Url, TitleEn, TitleTa, AltEn, AltTa, Category, SortOrder, IsPublished FROM Gallery {where} {orderBy} LIMIT @offset, @pageSize";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                if (!string.IsNullOrWhiteSpace(category)) cmd.Parameters.AddWithValue("@category", category.Trim());
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) paged.Items.Add(ReadItem(r));
            }

            return paged;
        }

        public async Task<int?> CreateAsync(CreateGalleryRequest req, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Gallery_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Type", req.Type);
            cmd.Parameters.AddWithValue("p_Url", req.Url);
            cmd.Parameters.AddWithValue("p_TitleEn", (object?)req.TitleEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TitleTa", (object?)req.TitleTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_AltEn", (object?)req.AltEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_AltTa", (object?)req.AltTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Category", (object?)req.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            cmd.Parameters.AddWithValue("p_IsPublished", req.IsPublished);
            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateGalleryRequest req, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Gallery_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Type", req.Type);
            cmd.Parameters.AddWithValue("p_Url", req.Url);
            cmd.Parameters.AddWithValue("p_TitleEn", (object?)req.TitleEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TitleTa", (object?)req.TitleTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_AltEn", (object?)req.AltEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_AltTa", (object?)req.AltTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Category", (object?)req.Category ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            cmd.Parameters.AddWithValue("p_IsPublished", req.IsPublished);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Gallery_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static GalleryItem ReadItem(MySqlDataReader r) => new GalleryItem
        {
            Id = r.GetInt32("Id"),
            Type = r.GetString("Type"),
            Url = r.GetString("Url"),
            Title = new LocalizedString { En = r.IsDBNull("TitleEn") ? "" : r.GetString("TitleEn"), Ta = r.IsDBNull("TitleTa") ? "" : r.GetString("TitleTa") },
            Alt = new LocalizedString { En = r.IsDBNull("AltEn") ? "" : r.GetString("AltEn"), Ta = r.IsDBNull("AltTa") ? "" : r.GetString("AltTa") },
            Category = r.IsDBNull("Category") ? null : r.GetString("Category"),
            SortOrder = r.GetInt32("SortOrder"),
            IsPublished = r.GetBoolean("IsPublished")
        };
    }
}

