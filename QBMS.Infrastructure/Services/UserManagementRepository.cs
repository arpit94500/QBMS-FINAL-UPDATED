using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class UserManagementRepository
        : IUserManagementRepository
    {
        private readonly string _connectionString;

        public UserManagementRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<UserListViewModel>>
            GetUsersAsync()
        {
            var result =
                new List<UserListViewModel>();

            const string sql = @"
SELECT
    u.user_id,
    u.personal_number,
    u.full_name,
    u.role_id,
    r.role_name,
    u.is_active
FROM mstr_user u
INNER JOIN mstr_role r
    ON r.role_id = u.role_id
ORDER BY
    u.user_id;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new UserListViewModel
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

                                    IsActive =
                                        Convert.ToBoolean(
                                            reader["is_active"])
                                });
                        }
                    }
                }
            }

            return result;
        }

        public async Task<bool>
            PersonnelNoExistsAsync(
                string personnelNo)
        {
            const string sql = @"
SELECT EXISTS
(
    SELECT 1
    FROM mstr_user
    WHERE personal_number = @personnel_no
);
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "personnel_no",
                        personnelNo);

                    return Convert.ToBoolean(
                        await command.ExecuteScalarAsync());
                }
            }
        }

        public async Task<long>
            CreateUserAsync(
                CreateUserViewModel model,
                string passwordHash)
        {
            const string sql = @"
INSERT INTO mstr_user
(
    personal_number,
    full_name,
    password_hash,
    role_id,
    is_active,
    created_at,
    must_change_password
)
VALUES
(
    @personnel_no,
    @user_name,
    @password_hash,
    @role_id,
    @is_active,
    CURRENT_TIMESTAMP,
    TRUE
)
RETURNING user_id;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "personnel_no",
                        model.PersonnelNo);

                    command.Parameters.AddWithValue(
                        "user_name",
                        model.UserName);

                    command.Parameters.AddWithValue(
                        "password_hash",
                        passwordHash);

                    command.Parameters.AddWithValue(
                        "role_id",
                        model.RoleId);

                    command.Parameters.AddWithValue(
                        "is_active",
                        model.IsActive);

                    var result =
                        await command.ExecuteScalarAsync();

                    return Convert.ToInt64(result);
                }
            }
        }

        public async Task<bool>
            UpdateStatusAsync(
                long userId,
                bool isActive)
        {
            const string sql = @"
UPDATE mstr_user
SET
    is_active = @is_active
WHERE user_id = @user_id;
";
            const string sql1 = @"
Select is_active from mstr_user
WHERE user_id = @user_id;
";
            

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var cmd=new NpgsqlCommand(sql1,connection))
                {
                    cmd.Parameters.AddWithValue("user_id", userId);
                    using (var a = await cmd.ExecuteReaderAsync())
                    {
                        while(await a.ReadAsync())
                        {
                            isActive = Convert.ToBoolean(a["is_active"]);
                        }
                        
                    }
                        
                }
                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "user_id",
                        userId);
                    if (isActive == true)
                        isActive = false;
                    else
                        isActive = true;
                    command.Parameters.AddWithValue(
                        "is_active",
                        isActive);

                    return
                        await command.ExecuteNonQueryAsync()
                        > 0;
                }
            }
        }

        public async Task<List<DropdownItem>> GetRolesAsync()
        {
            var result = new List<DropdownItem>();
            const string sql = @"Select role_id,role_name from mstr_role order by role_name;";
            using (var connection=new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using(var command=new NpgsqlCommand(sql,connection))
                {
                    using (var reader=await command.ExecuteReaderAsync())
                    {
                        while(await reader.ReadAsync())
                        {
                            result.Add(new DropdownItem
                            {
                                Id = Convert.ToInt64(reader["role_id"]),
                                Name = reader["role_name"].ToString()
                            });

                        }
                    }
                }
            }
            return result;
        }

        public async Task<EditUserViewModel> GetUserByIdAsync(long userId)
        {
            const string sql = @"
SELECT
    user_id,
    personal_number,
    full_name,
    role_id,
    is_active
FROM mstr_user
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

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        return new EditUserViewModel
                        {
                            UserId = Convert.ToInt64(reader["user_id"]),
                            PersonnelNo = reader["personal_number"].ToString(),
                            UserName = reader["full_name"].ToString(),
                            RoleId = Convert.ToInt32(reader["role_id"]),
                            IsActive = Convert.ToBoolean(reader["is_active"])
                        };
                    }
                }
            }
        }

        public async Task<bool> UpdateUserAsync(
            long userId,
            int roleId,
            bool isActive)
        {
            const string sql = @"
UPDATE mstr_user
SET
    role_id = @role_id,
    is_active = @is_active
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
                    command.Parameters.AddWithValue("role_id", roleId);
                    command.Parameters.AddWithValue("is_active", isActive);

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }
        }

        public async Task<bool> ResetPasswordAsync(
            long userId,
            string passwordHash)
        {
            const string sql = @"
UPDATE mstr_user
SET
    password_hash = @password_hash,
    must_change_password = TRUE
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

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }
        }
    }
}
