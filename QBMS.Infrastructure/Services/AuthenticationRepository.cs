using System;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class AuthenticationRepository
        : IAuthenticationRepository
    {
        private readonly string _connectionString;

        public AuthenticationRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<LoggedInUserModel>
            GetUserAsync(string personnelNo)
        {
            const string sql = @"
SELECT
    u.user_id,
    u.personal_number,
    u.full_name,
    u.password_hash,
    u.role_id,
    r.role_name,
    COALESCE(u.must_change_password, FALSE) AS must_change_password
FROM mstr_user u
INNER JOIN mstr_role r
    ON r.role_id = u.role_id
WHERE u.personal_number = @personnel_no
  AND u.is_active = TRUE
  AND r.is_active = TRUE;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                using (var command =
                    new NpgsqlCommand(
                        sql,
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "personnel_no",
                        personnelNo);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        return new LoggedInUserModel
                        {
                            UserId =
                                Convert.ToInt64(
                                    reader["user_id"]),

                            PersonnelNo =
                                reader["personal_number"]
                                    .ToString(),

                            UserName =
                                reader["full_name"]
                                    .ToString(),

                            RoleId =
                                Convert.ToInt32(
                                    reader["role_id"]),

                            RoleName =
                                reader["role_name"]
                                    .ToString(),

                            PasswordHash =
    reader["password_hash"].ToString(),

                            MustChangePassword =
                                Convert.ToBoolean(
                                    reader["must_change_password"]),

                        };
                    }
                }
            }
        }

        public async Task<bool>
            SetPasswordAsync(
                long userId,
                string passwordHash,
                bool mustChangePassword)
        {
            const string sql = @"
UPDATE mstr_user
SET
    password_hash = @password_hash,
    must_change_password = @must_change_password
WHERE user_id = @user_id;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("user_id", userId);
                    command.Parameters.AddWithValue("password_hash", passwordHash);
                    command.Parameters.AddWithValue("must_change_password", mustChangePassword);

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }
        }
    }
}
