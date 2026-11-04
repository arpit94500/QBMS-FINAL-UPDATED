using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class TosRepository : ITosRepository
    {
        private readonly string _connectionString;

        public TosRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<TosViewModel>> GetAllAsync()
        {
            var result =
                new List<TosViewModel>();

            const string sql = @"
SELECT topic_id, topic_name, topic_type, topic_easy_question, topic_easy_marks, topic_medium_question, topic_medium_mark, topic_hard_question, topic_hard_marks, main_exam_id, main_exam_name, paper_id, paper_name, subsection_id, subsection_name, section_id, section_name, is_active, tos_status
	FROM public.tos

ORDER BY
    main_exam_name,
    paper_name,
    section_name,
    subsection_name,
    topic_name;
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
                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new TosViewModel
                            {
                                TopicId = reader.GetInt32(0),
                                TopicName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                TopicType = reader.IsDBNull(2) ? null : reader.GetString(2),
                                TopicEasyQuestion = reader.GetInt32(3),
                                TopicEasyMarks = reader.GetInt32(4),
                                TopicMediumQuestion = reader.GetInt32(5),
                                TopicMediumMarks = reader.GetInt32(6),
                                TopicHardQuestion = reader.GetInt32(7),
                                TopicHardMarks = reader.GetInt32(8),
                                ExamId = reader.GetInt32(9),
                                ExamName = reader.IsDBNull(10) ? null : reader.GetString(10),
                                PaperId = reader.GetInt32(11),
                                PaperName = reader.IsDBNull(12) ? null : reader.GetString(12),
                                SectionId = reader.GetInt32(15),
                                SectionName = reader.IsDBNull(16) ? null : reader.GetString(16),
                                SubsectionId = reader.IsDBNull(13) ? (long?)null : reader.GetInt32(13),
                                SubsectionName = reader.IsDBNull(14) ? null : reader.GetString(14),
                                IsActive = reader.GetInt32(17),
                                TosStatus = reader.GetInt32(18)

                              
                            });
                        }
                    }
                }
            }

            return result;
        }
    }
}