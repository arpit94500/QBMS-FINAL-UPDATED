using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class VetterRepository : IVetterRepository
    {
        private readonly string _connectionString;

        public VetterRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<VetterQuestionViewModel>>
            GetQuestionsAsync(
                long vetterUserId)
        {
            var result =
                new List<VetterQuestionViewModel>();

            const string sql = @"
SELECT DISTINCT

    q.question_id,

    q.main_exam_id,
    q.paper_id,
    q.section_id,
    q.subsection_id,
    q.topic_id,

    t.main_exam_name,
    t.paper_name,
    t.section_name,
    t.subsection_name,
    t.topic_name,

    q.difficulty_level,
    q.question_text,
    q.option1,
    q.option2,
    q.option3,
    q.option4,
    q.correct_answer,
    q.status

FROM question q

INNER JOIN question_assignment qa
    ON qa.main_exam_id = q.main_exam_id

INNER JOIN tos t
    ON t.main_exam_id = q.main_exam_id
   AND t.paper_id = q.paper_id
   AND t.section_id = q.section_id
   AND t.subsection_id IS NOT DISTINCT FROM q.subsection_id
   AND t.topic_id = q.topic_id

WHERE qa.vetter_user_id = @vetter_user_id

  AND qa.is_active = TRUE

  AND q.status = 'SUBMITTED_TO_VETTER'

  AND
  (
      qa.assignment_level = 'EXAM'

      OR
      (
          qa.assignment_level = 'PAPER'
          AND q.paper_id = qa.paper_id
      )

      OR
      (
          qa.assignment_level = 'SECTION'
          AND q.paper_id = qa.paper_id
          AND q.section_id = qa.section_id
      )

      OR
      (
          qa.assignment_level = 'SUBSECTION'
          AND q.paper_id = qa.paper_id
          AND q.section_id = qa.section_id
          AND q.subsection_id = qa.subsection_id
      )

      OR
      (
          qa.assignment_level = 'TOPIC'
          AND q.paper_id = qa.paper_id
          AND q.section_id = qa.section_id
          AND q.subsection_id = qa.subsection_id
          AND q.topic_id = qa.topic_id
      )
  )

ORDER BY q.question_id DESC;
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
                        "vetter_user_id",
                        vetterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new VetterQuestionViewModel
                                {
                                    QuestionId =
                                        Convert.ToInt64(
                                            reader["question_id"]),

                                    MainExamId =
                                        Convert.ToInt64(
                                            reader["main_exam_id"]),

                                    PaperId =
                                        Convert.ToInt64(
                                            reader["paper_id"]),

                                    SectionId =
                                        Convert.ToInt64(
                                            reader["section_id"]),

                                    SubsectionId =
                                        reader["subsection_id"]
                                            == DBNull.Value
                                            ? (long?)null
                                            : Convert.ToInt64(
                                                reader["subsection_id"]),

                                    TopicId =
                                        Convert.ToInt64(
                                            reader["topic_id"]),

                                    ExamName =
                                        reader["main_exam_name"].ToString(),

                                    PaperName =
                                        reader["paper_name"].ToString(),

                                    SectionName =
                                        reader["section_name"]
                                            .ToString(),

                                    SubsectionName =
                                        reader["subsection_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["subsection_name"]
                                                .ToString(),

                                    TopicName =
                                        reader["topic_name"]
                                            .ToString(),

                                    DifficultyLevel =
                                        reader["difficulty_level"]
                                            .ToString(),

                                    QuestionText =
                                       EncryptionEnhanced.Decrypt( reader["question_text"]
                                            .ToString()),
                                    Option1= EncryptionEnhanced.Decrypt(reader["option1"]
                                            .ToString()),
                                    Option2 = EncryptionEnhanced.Decrypt(reader["option2"].ToString()),

                                    Option3 = EncryptionEnhanced.Decrypt(reader["option3"].ToString()),
                                    Option4 = EncryptionEnhanced.Decrypt(reader["option4"].ToString()),
                                    CorrectAnswer = EncryptionEnhanced.Decrypt(reader["correct_answer"].ToString()),



                                    Status =
                                        reader["status"].ToString()
                                });
                        }
                    }
                }
            }

            return result;
        }

        // Exam-wise count of questions still waiting for this Vetter.
        // Uses the same rule as the Review Question page
        // (assignment_id match + SUBMITTED_TO_VETTER), so the count
        // always equals the number of questions the Vetter can open.
        // Exams with no pending questions are not returned.
        public async Task<List<VetterExamSummaryViewModel>>
            GetPendingExamSummaryAsync(
                long vetterUserId)
        {
            var result =
                new List<VetterExamSummaryViewModel>();

            const string sql = @"
SELECT
    q.main_exam_id,
    COALESCE(MAX(t.main_exam_name), '') AS main_exam_name,
    COUNT(DISTINCT q.question_id) AS pending_count

FROM question q

INNER JOIN question_assignment qa
    ON qa.assignment_id = q.assignment_id

LEFT JOIN tos t
    ON t.main_exam_id = q.main_exam_id

WHERE qa.vetter_user_id = @vetter_user_id
  AND qa.is_active = TRUE
  AND q.status = 'SUBMITTED_TO_VETTER'

GROUP BY q.main_exam_id

HAVING COUNT(DISTINCT q.question_id) > 0

ORDER BY main_exam_name;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "vetter_user_id",
                        vetterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new VetterExamSummaryViewModel
                                {
                                    MainExamId =
                                        Convert.ToInt64(
                                            reader["main_exam_id"]),

                                    ExamName =
                                        reader["main_exam_name"]
                                            .ToString(),

                                    PendingCount =
                                        Convert.ToInt32(
                                            reader["pending_count"])
                                });
                        }
                    }
                }
            }

            return result;
        }
    }
}
