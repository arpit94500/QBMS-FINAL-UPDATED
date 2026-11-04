using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class ReportRepository : IReportRepository
    {
        private readonly string _connectionString;

        public ReportRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<AssignmentProgressRow>> GetAssignmentProgressAsync(long? examId)
        {
            // Same target rule as TOS Assignment Management and the Setter quota:
            // ceil(sum of the active TOS topic counts in scope x multiplication factor), per difficulty.
            const string sql = @"
SELECT
    a.assignment_id,
    a.setter_user_id,
    setter.full_name AS setter_name,
    a.vetter_user_id,
    vetter.full_name AS vetter_name,
    a.main_exam_id,
    COALESCE(nm.exam_name, '') AS exam_name,
    a.multiplication_factor,
    COALESCE(nm.easy_sum, 0)   AS easy_sum,
    COALESCE(nm.medium_sum, 0) AS medium_sum,
    COALESCE(nm.hard_sum, 0)   AS hard_sum,
    COALESCE(cr.easy_created, 0)   AS easy_created,
    COALESCE(cr.medium_created, 0) AS medium_created,
    COALESCE(cr.hard_created, 0)   AS hard_created,
    COALESCE(cr.draft, 0)          AS draft,
    COALESCE(cr.submitted, 0)      AS submitted,
    COALESCE(cr.vetter_approved, 0) AS vetter_approved,
    COALESCE(cr.rejected, 0)       AS rejected,
    COALESCE(cr.completed, 0)      AS completed
FROM question_assignment a
LEFT JOIN mstr_user setter ON setter.user_id = a.setter_user_id
LEFT JOIN mstr_user vetter ON vetter.user_id = a.vetter_user_id
LEFT JOIN LATERAL
(
    SELECT
        MAX(t.main_exam_name) AS exam_name,
        SUM(t.topic_easy_question)   AS easy_sum,
        SUM(t.topic_medium_question) AS medium_sum,
        SUM(t.topic_hard_question)   AS hard_sum
    FROM tos t
    WHERE t.main_exam_id = a.main_exam_id
      AND t.is_active = 1
      AND (a.paper_id IS NULL OR t.paper_id = a.paper_id)
      AND (a.section_id IS NULL OR t.section_id = a.section_id)
      AND (a.subsection_id IS NULL OR t.subsection_id = a.subsection_id)
      AND (a.topic_id IS NULL OR t.topic_id = a.topic_id)
) nm ON TRUE
LEFT JOIN LATERAL
(
    SELECT
        COUNT(*) FILTER (WHERE q.difficulty_level = 'EASY')   AS easy_created,
        COUNT(*) FILTER (WHERE q.difficulty_level = 'MEDIUM') AS medium_created,
        COUNT(*) FILTER (WHERE q.difficulty_level = 'HARD')   AS hard_created,
        COUNT(*) FILTER (WHERE q.status = 'DRAFT')               AS draft,
        COUNT(*) FILTER (WHERE q.status = 'SUBMITTED_TO_VETTER') AS submitted,
        COUNT(*) FILTER (WHERE q.status = 'VETTER_APPROVED')     AS vetter_approved,
        COUNT(*) FILTER (WHERE q.status IN ('VETTER_REJECTED', 'ADMIN_REJECTED')) AS rejected,
        COUNT(*) FILTER (WHERE q.status = 'COMPLETED')           AS completed
    FROM question q
    WHERE q.assignment_id = a.assignment_id
) cr ON TRUE
WHERE a.is_active = TRUE
  AND (CAST(@exam_id AS bigint) IS NULL OR a.main_exam_id = @exam_id)
ORDER BY a.assignment_id;";

            var result = new List<AssignmentProgressRow>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("exam_id", NpgsqlTypes.NpgsqlDbType.Bigint, (object)examId ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            decimal factor = Convert.ToDecimal(reader["multiplication_factor"]);
                            int easy = Required(reader["easy_sum"], factor);
                            int medium = Required(reader["medium_sum"], factor);
                            int hard = Required(reader["hard_sum"], factor);

                            result.Add(new AssignmentProgressRow
                            {
                                AssignmentId = Convert.ToInt64(reader["assignment_id"]),
                                SetterUserId = Convert.ToInt64(reader["setter_user_id"]),
                                SetterName = reader["setter_name"] == DBNull.Value ? "(unknown)" : reader["setter_name"].ToString(),
                                VetterUserId = reader["vetter_user_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["vetter_user_id"]),
                                VetterName = reader["vetter_name"] == DBNull.Value ? null : reader["vetter_name"].ToString(),
                                ExamId = Convert.ToInt64(reader["main_exam_id"]),
                                ExamName = reader["exam_name"].ToString(),
                                Target = easy + medium + hard,
                                Created = Math.Min(Convert.ToInt32(reader["easy_created"]), easy)
                                        + Math.Min(Convert.ToInt32(reader["medium_created"]), medium)
                                        + Math.Min(Convert.ToInt32(reader["hard_created"]), hard),
                                Draft = Convert.ToInt32(reader["draft"]),
                                Submitted = Convert.ToInt32(reader["submitted"]),
                                VetterApproved = Convert.ToInt32(reader["vetter_approved"]),
                                Rejected = Convert.ToInt32(reader["rejected"]),
                                Completed = Convert.ToInt32(reader["completed"])
                            });
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<PaperProgressRow>> GetPaperProgressAsync(long? examId)
        {
            // Same target rule as everywhere else (ceil(sum of TOS counts x factor) per difficulty), but split
            // per paper: each assignment is counted once for every paper its scope touches.
            const string sql = @"
SELECT
    s.assignment_id,
    s.main_exam_id,
    s.exam_name,
    s.paper_id,
    s.paper_name,
    s.multiplication_factor,
    s.easy_sum, s.medium_sum, s.hard_sum,
    COALESCE(cr.easy_created, 0)    AS easy_created,
    COALESCE(cr.medium_created, 0)  AS medium_created,
    COALESCE(cr.hard_created, 0)    AS hard_created,
    COALESCE(cr.draft, 0)           AS draft,
    COALESCE(cr.submitted, 0)       AS submitted,
    COALESCE(cr.vetter_approved, 0) AS vetter_approved,
    COALESCE(cr.rejected, 0)        AS rejected,
    COALESCE(cr.completed, 0)       AS completed
FROM
(
    SELECT
        a.assignment_id,
        a.main_exam_id,
        a.multiplication_factor,
        t.paper_id,
        MAX(t.main_exam_name) AS exam_name,
        MAX(t.paper_name)     AS paper_name,
        SUM(t.topic_easy_question)   AS easy_sum,
        SUM(t.topic_medium_question) AS medium_sum,
        SUM(t.topic_hard_question)   AS hard_sum
    FROM question_assignment a
    JOIN tos t
      ON t.main_exam_id = a.main_exam_id
     AND t.is_active = 1
     AND (a.paper_id IS NULL OR t.paper_id = a.paper_id)
     AND (a.section_id IS NULL OR t.section_id = a.section_id)
     AND (a.subsection_id IS NULL OR t.subsection_id = a.subsection_id)
     AND (a.topic_id IS NULL OR t.topic_id = a.topic_id)
    WHERE a.is_active = TRUE
      AND (CAST(@exam_id AS bigint) IS NULL OR a.main_exam_id = @exam_id)
    GROUP BY a.assignment_id, a.main_exam_id, a.multiplication_factor, t.paper_id
) s
LEFT JOIN LATERAL
(
    SELECT
        COUNT(*) FILTER (WHERE q.difficulty_level = 'EASY')   AS easy_created,
        COUNT(*) FILTER (WHERE q.difficulty_level = 'MEDIUM') AS medium_created,
        COUNT(*) FILTER (WHERE q.difficulty_level = 'HARD')   AS hard_created,
        COUNT(*) FILTER (WHERE q.status = 'DRAFT')               AS draft,
        COUNT(*) FILTER (WHERE q.status = 'SUBMITTED_TO_VETTER') AS submitted,
        COUNT(*) FILTER (WHERE q.status = 'VETTER_APPROVED')     AS vetter_approved,
        COUNT(*) FILTER (WHERE q.status IN ('VETTER_REJECTED', 'ADMIN_REJECTED')) AS rejected,
        COUNT(*) FILTER (WHERE q.status = 'COMPLETED')           AS completed
    FROM question q
    WHERE q.assignment_id = s.assignment_id
      AND q.paper_id = s.paper_id
) cr ON TRUE
ORDER BY s.exam_name, s.paper_name, s.assignment_id;";

            var result = new List<PaperProgressRow>();
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("exam_id", NpgsqlTypes.NpgsqlDbType.Bigint, (object)examId ?? DBNull.Value);
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            decimal factor = Convert.ToDecimal(reader["multiplication_factor"]);
                            int easy = Required(reader["easy_sum"], factor);
                            int medium = Required(reader["medium_sum"], factor);
                            int hard = Required(reader["hard_sum"], factor);
                            result.Add(new PaperProgressRow
                            {
                                AssignmentId = Convert.ToInt64(reader["assignment_id"]),
                                ExamId = Convert.ToInt64(reader["main_exam_id"]),
                                ExamName = reader["exam_name"] == DBNull.Value ? "" : reader["exam_name"].ToString(),
                                PaperId = Convert.ToInt64(reader["paper_id"]),
                                PaperName = reader["paper_name"] == DBNull.Value ? "" : reader["paper_name"].ToString(),
                                Target = easy + medium + hard,
                                Created = Math.Min(Convert.ToInt32(reader["easy_created"]), easy)
                                        + Math.Min(Convert.ToInt32(reader["medium_created"]), medium)
                                        + Math.Min(Convert.ToInt32(reader["hard_created"]), hard),
                                Draft = Convert.ToInt32(reader["draft"]),
                                Submitted = Convert.ToInt32(reader["submitted"]),
                                VetterApproved = Convert.ToInt32(reader["vetter_approved"]),
                                Rejected = Convert.ToInt32(reader["rejected"]),
                                Completed = Convert.ToInt32(reader["completed"])
                            });
                        }
                    }
                }
            }
            return result;
        }

        private static int Required(object tosSum, decimal factor)
        {
            if (tosSum == null || tosSum == DBNull.Value) return 0;
            var value = Convert.ToDecimal(tosSum) * factor;
            return value <= 0 ? 0 : (int)Math.Ceiling(value);
        }
    }
}
