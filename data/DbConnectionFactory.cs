
using Microsoft.Data.SqlClient;
namespace QLess.Data
{
    public class DbConnectionFactory
    {
        private readonly string _connectionString;

        public DbConnectionFactory()
        {
            _connectionString = Environment.GetEnvironmentVariable("QLessConnection") ?? throw new InvalidOperationException(
                    "QLessConnection was not found in the environment variables.");
        }

        public SqlConnection CreateConnection()
        {
            return new SqlConnection(_connectionString);
        }
    }
}
