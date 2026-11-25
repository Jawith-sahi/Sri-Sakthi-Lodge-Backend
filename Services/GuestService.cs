using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class GuestService
    {
        private readonly DbConnection _dbFactory;
        public GuestService(DbConnection dbFactory) => _dbFactory = dbFactory;

        public async Task<PagedResult<Guest>> GetAllAsync(int page, int pageSize, string? search)
        {
            var res = new PagedResult<Guest> { Page = page, PageSize = pageSize };
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Guests_GetAll_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Page", page);
            cmd.Parameters.AddWithValue("p_PageSize", pageSize);
            cmd.Parameters.AddWithValue("p_Search", (object?)search ?? DBNull.Value);

            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                res.Items.Add(new Guest
                {
                    Id = r.GetInt32("Id"),
                    Name = r.GetString("Name"),
                    Phone = r.GetString("Phone"),
                    Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
                    IdType = r.IsDBNull("IdType") ? null : r.GetString("IdType"),
                    Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
                    TotalBookings = r.GetInt32("TotalBookings"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                });
            }

            if (await r.NextResultAsync() && await r.ReadAsync())
            {
                res.TotalCount = r.GetInt32("TotalCount");
                res.TotalPages = (int)Math.Ceiling((double)res.TotalCount / pageSize);
            }

            return res;
        }

        public async Task<Guest?> GetByIdAsync(int id)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();

            using var cmd = new MySqlCommand("sp_Guests_GetById_Admin", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);

            using var r = await cmd.ExecuteReaderAsync();
            if (await r.ReadAsync())
            {
                return new Guest
                {
                    Id = r.GetInt32("Id"),
                    Name = r.GetString("Name"),
                    Phone = r.GetString("Phone"),
                    Email = r.IsDBNull("Email") ? null : r.GetString("Email"),
                    IdType = r.IsDBNull("IdType") ? null : r.GetString("IdType"),
                    Notes = r.IsDBNull("Notes") ? null : r.GetString("Notes"),
                    TotalBookings = r.GetInt32("TotalBookings"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                };
            }
            return null;
        }
    }
}

