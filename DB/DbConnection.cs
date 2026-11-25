using System.Data;
using MySqlConnector;

namespace SriSakthiLodge_API.DB
{
    public class DbConnection
    {
        private readonly string _connectionString;
        private readonly int _maxRetries = 3;

        public DbConnection(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString)
            {
                Pooling = true,
                MinimumPoolSize = 2,
                MaximumPoolSize = 20,
                ConnectionTimeout = 30,
                ConnectionLifeTime = 300,
                ConnectionIdleTimeout = 180,
                AllowZeroDateTime = false,
                ConvertZeroDateTime = true,
            };
            _connectionString = builder.ConnectionString;
        }

        public IDbConnection CreateConnection()
        {
            return new MySqlConnection(_connectionString);
        }

        public async Task<IDbConnection> CreateConnectionWithRetryAsync()
        {
            var retries = 0;
            while (true)
            {
                try
                {
                    var conn = new MySqlConnection(_connectionString);
                    await conn.OpenAsync();
                    return conn;
                }
                catch (MySqlException) when (retries < _maxRetries)
                {
                    retries++;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, retries)));
                }
            }
        }
    }
}

