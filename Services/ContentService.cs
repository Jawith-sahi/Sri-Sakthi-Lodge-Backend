using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class ContentService
    {
        private readonly DbConnection _dbFactory;
        public ContentService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<string> GetPublicAsync()
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Content_Get_Public", conn) { CommandType = CommandType.StoredProcedure };
            var res = await cmd.ExecuteScalarAsync();
            return res?.ToString() ?? "{}";
        }

        public async Task<string> GetAdminAsync()
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Content_Get_Admin", conn) { CommandType = CommandType.StoredProcedure };
            var res = await cmd.ExecuteScalarAsync();
            return res?.ToString() ?? "{}";
        }

        public async Task<bool> UpdateAsync(UpdateContentRequest req, string username)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Content_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_ContentKey", string.IsNullOrWhiteSpace(req.ContentKey) ? "main" : req.ContentKey);
            cmd.Parameters.AddWithValue("p_ValueJson", req.ValueJson ?? "{}");
            cmd.Parameters.AddWithValue("p_UpdatedBy", username);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }

        public async Task<bool> PublishAsync(string username)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Content_Publish", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_UpdatedBy", username);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}

