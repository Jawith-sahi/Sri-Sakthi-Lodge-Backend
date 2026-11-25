using Microsoft.AspNetCore.Identity;
using MySqlConnector;
using SriSakthiLodge_API.DB;
using SriSakthiLodge_API.Model;
using System.Data;

namespace SriSakthiLodge_API.Services
{
    public class StaffService
    {
        private readonly DbConnection _dbFactory;
        private readonly IPasswordHasher<object> _passwordHasher;

        public StaffService(DbConnection dbFactory, IPasswordHasher<object> passwordHasher)
        {
            _dbFactory = dbFactory;
            _passwordHasher = passwordHasher;
        }

        public async Task<List<StaffMember>> GetAllAsync()
        {
            var list = new List<StaffMember>();
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Staff_GetAll", conn) { CommandType = CommandType.StoredProcedure };
            using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new StaffMember
                {
                    Id = r.GetInt32("Id"),
                    Username = r.GetString("Username"),
                    Name = r.GetString("Name"),
                    Role = r.GetString("Role"),
                    IsActive = r.GetBoolean("IsActive"),
                    CreatedAt = r.GetDateTime("CreatedAt")
                });
            }
            return list;
        }

        public async Task<int?> CreateAsync(CreateStaffRequest req, int createdByUserId)
        {
            var passwordHash = _passwordHasher.HashPassword(new object(), req.Password);
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Staff_Create", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Username", req.Username);
            cmd.Parameters.AddWithValue("p_PasswordHash", passwordHash);
            cmd.Parameters.AddWithValue("p_Name", req.Name);
            cmd.Parameters.AddWithValue("p_Role", req.Role);
            var res = await cmd.ExecuteScalarAsync();
            return res != null && int.TryParse(res.ToString(), out int id) ? id : null;
        }

        public async Task<bool> UpdateAsync(int id, UpdateStaffRequest req, int updatedByUserId)
        {
            using var conn = (MySqlConnection)_dbFactory.CreateConnection();
            await conn.OpenAsync();
            using var cmd = new MySqlCommand("sp_Staff_Update", conn) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("p_Id", id);
            cmd.Parameters.AddWithValue("p_Name", req.Name);
            cmd.Parameters.AddWithValue("p_Role", req.Role);
            cmd.Parameters.AddWithValue("p_IsActive", req.IsActive);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
    }
}

