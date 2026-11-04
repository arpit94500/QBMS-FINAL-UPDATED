using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class UserRepository : IUserRepository
    {
        private readonly string _connectionString;
        public UserRepository(string connectionString)
        {
            _connectionString = connectionString;
        }
        public Task<List<DropdownItem>> GetSettersAsync()
        {
            return GetUsersAsync(2);
        }
        public Task<List<DropdownItem>> GetVetterAsync()
        {
            return GetUsersAsync(3);
        }
        private async Task<List<DropdownItem>> GetUsersAsync(int roleId)
        {
            var result = new List<DropdownItem>();
            const string sql = @"Select user_id,personal_number,full_name from mstr_user where role_id =@role_id and is_active=TRUE order by personal_number, full_name";
            using(var connection=new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command=new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("role_id",roleId);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while(await reader.ReadAsync())
                        {
                            result.Add(
                                new DropdownItem { 
                                    Id= Convert.ToInt64(reader["user_id"]),
                                    // shown as "personal number - name" in the Setter / Vetter drop-downs
                                    Name = (reader["personal_number"] == DBNull.Value || string.IsNullOrWhiteSpace(reader["personal_number"].ToString()))
                                        ? reader["full_name"].ToString()
                                        : reader["personal_number"].ToString().Trim() + " - " + reader["full_name"].ToString()
                                });
                        }
                    }
                }
            }
            return result;

        }

    }
}
