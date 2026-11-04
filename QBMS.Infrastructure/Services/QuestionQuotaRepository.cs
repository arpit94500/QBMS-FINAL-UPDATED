using System;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class QuestionQuotaRepository
        : IQuestionQuotaRepository
    {
        private readonly string _connectionString;

        public QuestionQuotaRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<QuestionQuotaViewModel>
            GetQuotaAsync(
                long assignmentId,
                long setterUserId)
        {
            const string sql = @"
SELECT
    qa.assignment_level,
    qa.main_exam_id,
    qa.paper_id,
    qa.section_id,
    qa.subsection_id,
    qa.topic_id,
    qa.multiplication_factor
FROM question_assignment qa
WHERE qa.assignment_id = @assignment_id
  AND qa.setter_user_id = @setter_user_id
  AND qa.is_active = TRUE;
";

            string assignmentLevel = null;

            long mainExamId = 0;
            long? paperId = null;
            long? sectionId = null;
            long? subsectionId = null;
            long? topicId = null;

            decimal factor = 1.0m;

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
                        "assignment_id",
                        assignmentId);

                    command.Parameters.AddWithValue(
                        "setter_user_id",
                        setterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            throw new InvalidOperationException(
                                "Assignment not found for this Setter.");
                        }

                        assignmentLevel =
                            reader["assignment_level"]
                                .ToString();

                        mainExamId =
                            Convert.ToInt64(
                                reader["main_exam_id"]);

                        paperId =
                            reader["paper_id"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(
                                    reader["paper_id"]);

                        sectionId =
                            reader["section_id"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(
                                    reader["section_id"]);

                        subsectionId =
                            reader["subsection_id"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(
                                    reader["subsection_id"]);

                        topicId =
                            reader["topic_id"] == DBNull.Value
                                ? (long?)null
                                : Convert.ToInt64(
                                    reader["topic_id"]);

                        factor =
                            Convert.ToDecimal(
                                reader["multiplication_factor"]);
                    }
                }
            }

            string whereClause;

            switch (assignmentLevel)
            {
                case "EXAM":

                    whereClause = @"
t.main_exam_id = @main_exam_id
";

                    break;

                case "PAPER":

                    whereClause = @"
t.main_exam_id = @main_exam_id
AND t.paper_id = @paper_id
";

                    break;

                case "SECTION":

                    whereClause = @"
t.main_exam_id = @main_exam_id
AND t.paper_id = @paper_id
AND t.section_id = @section_id
";

                    break;

                case "SUBSECTION":

                    whereClause = @"
t.main_exam_id = @main_exam_id
AND t.paper_id = @paper_id
AND t.section_id = @section_id
AND t.subsection_id = @subsection_id
";

                    break;

                case "TOPIC":

                    whereClause = @"
t.main_exam_id = @main_exam_id
AND t.paper_id = @paper_id
AND t.section_id = @section_id
AND t.subsection_id = @subsection_id
AND t.topic_id = @topic_id
";

                    break;

                default:

                    throw new InvalidOperationException(
                        "Invalid assignment level.");
            }

            var tosSql = @"
SELECT
    COALESCE(SUM(t.topic_easy_question), 0),
    COALESCE(SUM(t.topic_medium_question), 0),
    COALESCE(SUM(t.topic_hard_question), 0)
FROM tos t
WHERE "
                + whereClause
                + @"
AND t.is_active = 1;
";

            int easyRequired;
            int mediumRequired;
            int hardRequired;

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(
                        tosSql,
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "main_exam_id",
                        mainExamId);

                    if (paperId.HasValue)
                    {
                        command.Parameters.AddWithValue(
                            "paper_id",
                            paperId.Value);
                    }

                    if (sectionId.HasValue)
                    {
                        command.Parameters.AddWithValue(
                            "section_id",
                            sectionId.Value);
                    }

                    if (subsectionId.HasValue)
                    {
                        command.Parameters.AddWithValue(
                            "subsection_id",
                            subsectionId.Value);
                    }

                    if (topicId.HasValue)
                    {
                        command.Parameters.AddWithValue(
                            "topic_id",
                            topicId.Value);
                    }

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();

                        easyRequired =
                            Convert.ToInt32(
                                reader.GetValue(0));

                        mediumRequired =
                            Convert.ToInt32(
                                reader.GetValue(1));

                        hardRequired =
                            Convert.ToInt32(
                                reader.GetValue(2));
                    }
                }
            }

            easyRequired =
                (int)Math.Ceiling(
                    easyRequired * factor);

            mediumRequired =
                (int)Math.Ceiling(
                    mediumRequired * factor);

            hardRequired =
                (int)Math.Ceiling(
                    hardRequired * factor);

            int easyCreated;
            int mediumCreated;
            int hardCreated;

            const string countSql = @"
SELECT
    COALESCE(
        SUM(
            CASE
                WHEN q.difficulty_level = 'EASY'
                THEN 1
                ELSE 0
            END
        ), 0
    ),

    COALESCE(
        SUM(
            CASE
                WHEN q.difficulty_level = 'MEDIUM'
                THEN 1
                ELSE 0
            END
        ), 0
    ),

    COALESCE(
        SUM(
            CASE
                WHEN q.difficulty_level = 'HARD'
                THEN 1
                ELSE 0
            END
        ), 0
    )

FROM question q
INNER JOIN question_assignment qa
    ON qa.assignment_id = @assignment_id

WHERE q.created_by = @setter_user_id

  AND q.main_exam_id = qa.main_exam_id

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
  );
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(
                        countSql,
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "assignment_id",
                        assignmentId);

                    command.Parameters.AddWithValue(
                        "setter_user_id",
                        setterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        await reader.ReadAsync();

                        easyCreated =
                            Convert.ToInt32(
                                reader.GetValue(0));

                        mediumCreated =
                            Convert.ToInt32(
                                reader.GetValue(1));

                        hardCreated =
                            Convert.ToInt32(
                                reader.GetValue(2));
                    }
                }
            }

            return new QuestionQuotaViewModel
            {
                EasyRequired = easyRequired,
                MediumRequired = mediumRequired,
                HardRequired = hardRequired,

                EasyCreated = easyCreated,
                MediumCreated = mediumCreated,
                HardCreated = hardCreated,

                // These properties are used by the page's overall quota
                // display and navigation. They were previously left at 0,
                // making TotalRemaining always equal to 0.
                EasyAssigned = easyRequired,
                MediumAssigned = mediumRequired,
                HardAssigned = hardRequired,
                EasyUsed = easyCreated,
                MediumUsed = mediumCreated,
                HardUsed = hardCreated,
                Easyremaining = Math.Max(0, easyRequired - easyCreated),
                Mediumremaining = Math.Max(0, mediumRequired - mediumCreated),
                Hardremaining = Math.Max(0, hardRequired - hardCreated)
            };
        }
    }
}
