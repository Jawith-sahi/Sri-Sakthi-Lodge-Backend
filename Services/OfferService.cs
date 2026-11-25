using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class OfferService
    {
        private readonly DbConnection _dbFactory;
        public OfferService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<List<Offer>> GetAllPublicAsync()
        {
            var list = new List<Offer>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Offers_GetAll_Public", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(ReadOffer(r));
            return list;
        }

        public async Task<List<Offer>> GetAllAdminAsync()
        {
            var list = new List<Offer>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Offers_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(ReadOffer(r));
            return list;
        }

        public async Task<PagedResult<Offer>> GetAllAdminPagedAsync(int page = 1, int pageSize = 10, string? search = null, string? status = null, string? sort = null, string? dir = null)
        {
            var paged = new PagedResult<Offer> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (TitleEn LIKE @search OR TitleTa LIKE @search OR DescEn LIKE @search)";
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                var s = status.Trim().ToLowerInvariant();
                if (s == "published") where += " AND IsPublished = 1";
                else if (s == "draft") where += " AND IsPublished = 0";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Offers {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                paged.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                paged.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)paged.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var orderBy = "ORDER BY SortOrder ASC, StartDate DESC";
            if (!string.IsNullOrWhiteSpace(sort))
            {
                var direction = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase) ? "DESC" : "ASC";
                if (sort.Equals("title", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY TitleEn {direction}";
                else if (sort.Equals("startDate", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY StartDate {direction}";
                else if (sort.Equals("endDate", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY EndDate {direction}";
                else if (sort.Equals("order", StringComparison.OrdinalIgnoreCase)) orderBy = $"ORDER BY SortOrder {direction}";
            }

            var sql = $"SELECT Id, TitleEn, TitleTa, DescEn, DescTa, ImageUrl, StartDate, EndDate, TermsEn, TermsTa, IsPublished, SortOrder FROM Offers {where} {orderBy} LIMIT @offset, @pageSize";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) paged.Items.Add(ReadOffer(r));
            }

            return paged;
        }

        public async Task<int?> CreateAsync(CreateOfferRequest req, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Offers_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_TitleEn", req.TitleEn);
            cmd.Parameters.AddWithValue("p_TitleTa", req.TitleTa);
            cmd.Parameters.AddWithValue("p_DescEn", (object?)req.DescEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescTa", (object?)req.DescTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ImageUrl", (object?)req.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_StartDate", req.StartDate);
            cmd.Parameters.AddWithValue("p_EndDate", (object?)req.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TermsEn", (object?)req.TermsEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TermsTa", (object?)req.TermsTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsPublished", req.IsPublished);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateOfferRequest req, int userId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Offers_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_TitleEn", req.TitleEn);
            cmd.Parameters.AddWithValue("p_TitleTa", req.TitleTa);
            cmd.Parameters.AddWithValue("p_DescEn", (object?)req.DescEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_DescTa", (object?)req.DescTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_ImageUrl", (object?)req.ImageUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_StartDate", req.StartDate);
            cmd.Parameters.AddWithValue("p_EndDate", (object?)req.EndDate ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TermsEn", (object?)req.TermsEn ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_TermsTa", (object?)req.TermsTa ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_IsPublished", req.IsPublished);
            cmd.Parameters.AddWithValue("p_SortOrder", req.SortOrder);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> DeleteAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Offers_Delete", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        private static Offer ReadOffer(MySqlDataReader r) => new Offer
        {
            Id = r.GetInt32("Id"),
            Title = new LocalizedString { En = r.GetString("TitleEn"), Ta = r.GetString("TitleTa") },
            Description = new LocalizedString { En = r.IsDBNull("DescEn") ? "" : r.GetString("DescEn"), Ta = r.IsDBNull("DescTa") ? "" : r.GetString("DescTa") },
            ImageUrl = r.IsDBNull("ImageUrl") ? null : r.GetString("ImageUrl"),
            StartDate = r.GetDateTime("StartDate").ToString("yyyy-MM-dd"),
            EndDate = r.IsDBNull("EndDate") ? null : r.GetDateTime("EndDate").ToString("yyyy-MM-dd"),
            Terms = new LocalizedString { En = r.IsDBNull("TermsEn") ? "" : r.GetString("TermsEn"), Ta = r.IsDBNull("TermsTa") ? "" : r.GetString("TermsTa") },
            IsPublished = r.GetBoolean("IsPublished"),
            SortOrder = r.GetInt32("SortOrder")
        };
    }
}

