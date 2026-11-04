using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class ActivityLogRepository : IActivityLogRepository
    {
        private readonly string _connectionString;
        private static volatile bool _tableReady;

        public ActivityLogRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        private async Task EnsureTableAsync(NpgsqlConnection connection)
        {
            if (_tableReady) return;

            // If the table already exists (created by a DBA with the script in Database/user_activity_log.sql)
            // nothing needs to be created, so the application user does not need CREATE rights on the schema.
            using (var check = new NpgsqlCommand("SELECT to_regclass('public.user_activity_log') IS NOT NULL", connection))
            {
                if ((bool)await check.ExecuteScalarAsync())
                {
                    _tableReady = true;
                    return;
                }
            }

            const string sql = @"
CREATE TABLE IF NOT EXISTS user_activity_log
(
    log_id BIGSERIAL PRIMARY KEY,
    logged_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
    user_id BIGINT NULL,
    personal_number VARCHAR(50) NULL,
    user_name VARCHAR(200) NULL,
    role_name VARCHAR(50) NULL,
    activity VARCHAR(200) NOT NULL,
    http_method VARCHAR(10) NULL,
    path VARCHAR(500) NULL,
    query_string VARCHAR(1000) NULL,
    ip_address VARCHAR(64) NULL,
    is_success BOOLEAN NOT NULL DEFAULT TRUE,
    details VARCHAR(1000) NULL
);
CREATE INDEX IF NOT EXISTS ix_user_activity_log_time ON user_activity_log (logged_at DESC);
CREATE INDEX IF NOT EXISTS ix_user_activity_log_user ON user_activity_log (personal_number);";

            try
            {
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
            catch (PostgresException ex) when (ex.SqlState == "42501")
            {
                throw new InvalidOperationException(
                    "The table user_activity_log does not exist and the database user is not allowed to create it. " +
                    "Run Database/user_activity_log.sql as a database administrator and grant the application user access " +
                    "(see the GRANT lines in that script).", ex);
            }
            _tableReady = true;
        }

        private static string Cut(string value, int max)
        {
            if (value == null) return null;
            return value.Length <= max ? value : value.Substring(0, max);
        }

        public async Task InsertAsync(ActivityLogEntry e)
        {
            try
            {
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    await EnsureTableAsync(connection);

                    const string sql = @"
INSERT INTO user_activity_log
(user_id, personal_number, user_name, role_name, activity, http_method, path, query_string, ip_address, is_success, details)
VALUES
(@user_id, @pn, @un, @role, @activity, @method, @path, @query, @ip, @ok, @details);";

                    using (var command = new NpgsqlCommand(sql, connection))
                    {
                        command.Parameters.AddWithValue("user_id", (object)e.UserId ?? DBNull.Value);
                        command.Parameters.AddWithValue("pn", (object)Cut(e.PersonnelNo, 50) ?? DBNull.Value);
                        command.Parameters.AddWithValue("un", (object)Cut(e.UserName, 200) ?? DBNull.Value);
                        command.Parameters.AddWithValue("role", (object)Cut(e.Role, 50) ?? DBNull.Value);
                        command.Parameters.AddWithValue("activity", Cut(e.Activity ?? "", 200));
                        command.Parameters.AddWithValue("method", (object)Cut(e.HttpMethod, 10) ?? DBNull.Value);
                        command.Parameters.AddWithValue("path", (object)Cut(e.Path, 500) ?? DBNull.Value);
                        command.Parameters.AddWithValue("query", (object)Cut(e.QueryString, 1000) ?? DBNull.Value);
                        command.Parameters.AddWithValue("ip", (object)Cut(e.IpAddress, 64) ?? DBNull.Value);
                        command.Parameters.AddWithValue("ok", e.IsSuccess);
                        command.Parameters.AddWithValue("details", (object)Cut(e.Details, 1000) ?? DBNull.Value);
                        await command.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.WriteLine("Activity log write failed: " + ex.Message);
            }
        }

        public async Task<ActivityLogPage> GetPageAsync(ActivityLogQuery query)
        {
            if (query.PageSize < 5) query.PageSize = 25;
            if (query.PageSize > 200) query.PageSize = 200;
            if (query.Page < 1) query.Page = 1;

            var result = new ActivityLogPage { Query = query };

            const string where = @"
WHERE (@pn IS NULL OR personal_number = @pn)
  AND (@role IS NULL OR role_name = @role)
  AND (@from IS NULL OR logged_at >= @from)
  AND (@to IS NULL OR logged_at < @to)
  AND (@search IS NULL OR activity ILIKE @search OR path ILIKE @search OR user_name ILIKE @search
       OR personal_number ILIKE @search OR details ILIKE @search OR ip_address ILIKE @search)";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                await EnsureTableAsync(connection);

                Action<NpgsqlCommand> addFilters = c =>
                {
                    c.Parameters.AddWithValue("pn", NpgsqlTypes.NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(query.PersonnelNo) ? (object)DBNull.Value : query.PersonnelNo.Trim());
                    c.Parameters.AddWithValue("role", NpgsqlTypes.NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(query.Role) ? (object)DBNull.Value : query.Role.Trim());
                    c.Parameters.AddWithValue("from", NpgsqlTypes.NpgsqlDbType.Timestamp, query.From.HasValue ? (object)query.From.Value.Date : DBNull.Value);
                    c.Parameters.AddWithValue("to", NpgsqlTypes.NpgsqlDbType.Timestamp, query.To.HasValue ? (object)query.To.Value.Date.AddDays(1) : DBNull.Value);
                    c.Parameters.AddWithValue("search", NpgsqlTypes.NpgsqlDbType.Varchar, string.IsNullOrWhiteSpace(query.Search) ? (object)DBNull.Value : "%" + query.Search.Trim() + "%");
                };

                using (var count = new NpgsqlCommand("SELECT COUNT(*) FROM user_activity_log " + where, connection))
                {
                    addFilters(count);
                    result.Total = Convert.ToInt32(await count.ExecuteScalarAsync());
                }

                if (query.Page > result.TotalPages) query.Page = result.TotalPages;

                using (var command = new NpgsqlCommand(
                    "SELECT log_id, logged_at, user_id, personal_number, user_name, role_name, activity, http_method, path, query_string, ip_address, is_success, details FROM user_activity_log "
                    + where + " ORDER BY logged_at DESC, log_id DESC OFFSET @offset LIMIT @limit", connection))
                {
                    addFilters(command);
                    command.Parameters.AddWithValue("offset", (query.Page - 1) * query.PageSize);
                    command.Parameters.AddWithValue("limit", query.PageSize);

                    using (var r = await command.ExecuteReaderAsync())
                    {
                        while (await r.ReadAsync())
                        {
                            result.Items.Add(new ActivityLogEntry
                            {
                                LogId = r.GetInt64(0),
                                LoggedAt = r.GetDateTime(1),
                                UserId = r.IsDBNull(2) ? (long?)null : r.GetInt64(2),
                                PersonnelNo = r.IsDBNull(3) ? null : r.GetString(3),
                                UserName = r.IsDBNull(4) ? null : r.GetString(4),
                                Role = r.IsDBNull(5) ? null : r.GetString(5),
                                Activity = r.GetString(6),
                                HttpMethod = r.IsDBNull(7) ? null : r.GetString(7),
                                Path = r.IsDBNull(8) ? null : r.GetString(8),
                                QueryString = r.IsDBNull(9) ? null : r.GetString(9),
                                IpAddress = r.IsDBNull(10) ? null : r.GetString(10),
                                IsSuccess = r.GetBoolean(11),
                                Details = r.IsDBNull(12) ? null : r.GetString(12)
                            });
                        }
                    }
                }
            }

            return result;
        }
    }
}
