using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class EnquiryService
    {
        private readonly DbConnection _dbFactory;
        public EnquiryService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<int?> SubmitAsync(SubmitEnquiryRequest req)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Enquiries_Submit", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Name", req.Name);
            cmd.Parameters.AddWithValue("p_Phone", req.Phone);
            cmd.Parameters.AddWithValue("p_Email", (object?)req.Email ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Subject", (object?)req.Subject ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Message", req.Message);
            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<PagedResult<Enquiry>> GetAllAdminAsync(int page, int pageSize, string? status, string? search = null)
        {
            var res = new PagedResult<Enquiry> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            var where = "WHERE 1=1";
            if (!string.IsNullOrWhiteSpace(search))
            {
                where += " AND (Name LIKE @search OR Phone LIKE @search OR Email LIKE @search OR Subject LIKE @search OR Message LIKE @search)";
            }
            if (!string.IsNullOrWhiteSpace(status))
            {
                where += " AND Status = @status";
            }

            using (var countCmd = new MySqlCommand($"SELECT COUNT(*) FROM Enquiries {where}", conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) countCmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                if (!string.IsNullOrWhiteSpace(status)) countCmd.Parameters.AddWithValue("@status", status.Trim());
                res.TotalCount = Convert.ToInt32(await countCmd.ExecuteScalarAsync());
                res.TotalPages = pageSize > 0 ? (int)Math.Ceiling((double)res.TotalCount / pageSize) : 0;
            }

            var offset = Math.Max(0, (page - 1) * pageSize);
            var sql = $"SELECT Id, Name, Phone, Email, Subject, Message, Status, Notes, CreatedAt FROM Enquiries {where} ORDER BY Id DESC LIMIT @offset, @pageSize";
            using (var cmd = new MySqlCommand(sql, conn))
            {
                if (!string.IsNullOrWhiteSpace(search)) cmd.Parameters.AddWithValue("@search", $"%{search.Trim()}%");
                if (!string.IsNullOrWhiteSpace(status)) cmd.Parameters.AddWithValue("@status", status.Trim());
                cmd.Parameters.AddWithValue("@offset", offset);
                cmd.Parameters.AddWithValue("@pageSize", pageSize);

                using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync())
                {
                    res.Items.Add(new Enquiry
                    {
                        Id = r.GetInt32("Id"),
                        Name = r.GetString("Name"),
                        Phone = r.GetString("Phone"),
                        Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
                        Subject = r.IsDBNull("Subject") ? null : r.GetString("Subject"),
                        Message = r.GetString("Message"),
                        Status = r.GetString("Status"),
                        Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
                        CreatedAt = r.GetDateTime("CreatedAt")
                    });
                }
            }
            return res;
        }

        public async Task<Enquiry?> GetByIdAdminAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Enquiries_GetById_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new Enquiry
                {
                    Id = r.GetInt32("Id"),
                    Name = r.GetString("Name"),
                    Phone = r.GetString("Phone"),
                    Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
                    Subject = r.IsDBNull("Subject") ? null : r.GetString("Subject"),
                    Message = r.GetString("Message"),
                    Status = r.GetString("Status"),
                    Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                };
            }
            return null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateEnquiryRequest req, string staffUsername)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Enquiries_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Status", (object?)req.Status ?? DBNull.Value);
            cmd.Parameters.AddWithValue("p_Notes", (object?)req.Notes ?? DBNull.Value);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}

