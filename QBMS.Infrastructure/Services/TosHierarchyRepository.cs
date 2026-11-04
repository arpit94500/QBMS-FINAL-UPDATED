using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class TosHierarchyRepository :
        ITosHierarchyRepository
    {
        private readonly string _connectionString;

        public TosHierarchyRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<DropdownItem>>
            GetExamsAsync()
        {
            const string sql = @"
                SELECT DISTINCT
                    main_exam_id,
                    main_exam_name
                FROM tos
                WHERE is_active = 1
                ORDER BY main_exam_name;";

            return await ExecuteDropdownQuery(
                sql);
        }

        public async Task<List<DropdownItem>>
            GetPapersAsync(long examId)
        {
            const string sql = @"
                SELECT DISTINCT
                    paper_id,
                    paper_name
                FROM tos
                WHERE main_exam_id = @exam_id
                  AND is_active = 1
                ORDER BY paper_name;";

            return await ExecuteDropdownQuery(
                sql,
                new NpgsqlParameter(
                    "exam_id",
                    examId));
        }

        public async Task<List<DropdownItem>>
            GetSectionsAsync(long paperId)
        {
            const string sql = @"
                SELECT DISTINCT
                    section_id,
                    section_name
                FROM tos
                WHERE paper_id = @paper_id
                  AND is_active = 1
                ORDER BY section_name;";

            return await ExecuteDropdownQuery(
                sql,
                new NpgsqlParameter(
                    "paper_id",
                    paperId));
        }

        public async Task<List<DropdownItem>>
            GetSubsectionsAsync(long sectionId)
        {
            const string sql = @"
                SELECT DISTINCT
                    subsection_id,
                    subsection_name
                FROM tos
                WHERE section_id = @section_id
                  AND subsection_id IS NOT NULL
                  AND is_active = 1
                ORDER BY subsection_name;";

            return await ExecuteDropdownQuery(
                sql,
                new NpgsqlParameter(
                    "section_id",
                    sectionId));
        }

        public async Task<List<DropdownItem>>
            GetTopicsAsync(long subsectionId)
        {
            const string sql = @"
                SELECT DISTINCT
                    topic_id,
                    topic_name
                FROM tos
                WHERE subsection_id = @subsection_id
                  AND is_active = 1
                ORDER BY topic_name;";

            return await ExecuteDropdownQuery(
                sql,
                new NpgsqlParameter(
                    "subsection_id",
                    subsectionId));
        }

        private async Task<List<DropdownItem>>
            ExecuteDropdownQuery(
                string sql,
                params NpgsqlParameter[] parameters)
        {
            var result =
                new List<DropdownItem>();

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    if (parameters != null)
                    {
                        command.Parameters.AddRange(
                            parameters);
                    }

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (
                            await reader.ReadAsync())
                        {
                            result.Add(
                                new DropdownItem
                                {
                                    Id =
                                        Convert.ToInt64(
                                            reader.GetValue(0)),

                                    Name =
                                        reader.IsDBNull(1)
                                            ? string.Empty
                                            : reader.GetString(1)
                                });
                        }
                    }
                }
            }

            return result;
        }
    }
}