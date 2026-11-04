using Npgsql;

namespace QBMS.Infrastructure
{
    public class PostgreSqlConnectionFactory
    {
        private readonly string _connectionString;
        public PostgreSqlConnectionFactory(string connectionString)
        {
            _connectionString = connectionString;
        }
        public NpgsqlConnection CreateConnection()
        {
            return new NpgsqlConnection(_connectionString);
        }
    }
}
