using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class QuestionAssignmentRepository: IQuestionAssignmentRepository
    {
        private readonly string _connectionString;
        public QuestionAssignmentRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // Target / Completed / Remaining
        //   Target    = what the TOS asks for in the assignment's scope: for EASY, MEDIUM and HARD,
        //               ceil(sum of the active TOS topic counts x multiplication factor).
        //   Completed = questions the setter has created for the assignment, counted per difficulty and
        //               never more than the target of that difficulty (same rule as the Setter quota).
        //   Remaining = Target - Completed.
        // The old query counted question rows after joining them to every TOS row (so one question was
        // counted once per TOS row) and called a question "completed" only after the final Admin approval.
        public async Task<List<TosAssignmentManagementRow>>
    GetAllForManagementAsync(string search, long? examId = null, long? paperId = null)
        {
            var result = new List<TosAssignmentManagementRow>();

            const string sql = @"
SELECT
    a.assignment_id,
    a.assignment_level,
    a.multiplication_factor,
    a.setter_user_id,
    a.vetter_user_id,
    a.is_active,
    a.assigned_at,
    a.main_exam_id,
    a.paper_id,

    setter.full_name AS setter_name,
    vetter.full_name AS vetter_name,

    COALESCE(nm.exam_name, '')       AS exam_name,
    COALESCE(nm.paper_name, '')      AS paper_name,
    COALESCE(nm.section_name, '')    AS section_name,
    COALESCE(nm.subsection_name, '') AS subsection_name,
    COALESCE(nm.topic_name, '')      AS topic_name,

    COALESCE(nm.easy_sum, 0)   AS easy_sum,
    COALESCE(nm.medium_sum, 0) AS medium_sum,
    COALESCE(nm.hard_sum, 0)   AS hard_sum,

    COALESCE(cr.easy_created, 0)   AS easy_created,
    COALESCE(cr.medium_created, 0) AS medium_created,
    COALESCE(cr.hard_created, 0)   AS hard_created,
    COALESCE(cr.approved, 0)       AS approved_questions
FROM question_assignment a

LEFT JOIN mstr_user setter ON setter.user_id = a.setter_user_id
LEFT JOIN mstr_user vetter ON vetter.user_id = a.vetter_user_id

LEFT JOIN LATERAL
(
    SELECT
        MAX(t.main_exam_name) AS exam_name,
        CASE WHEN a.paper_id      IS NULL THEN '' ELSE MAX(t.paper_name)      END AS paper_name,
        CASE WHEN a.section_id    IS NULL THEN '' ELSE MAX(t.section_name)    END AS section_name,
        CASE WHEN a.subsection_id IS NULL THEN '' ELSE MAX(t.subsection_name) END AS subsection_name,
        CASE WHEN a.topic_id      IS NULL THEN '' ELSE MAX(t.topic_name)      END AS topic_name,
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
        COUNT(*) FILTER (WHERE q.status = 'COMPLETED')        AS approved
    FROM question q
    WHERE q.assignment_id = a.assignment_id
) cr ON TRUE

WHERE
    (CAST(@exam_id AS bigint)  IS NULL OR a.main_exam_id = @exam_id)
AND (CAST(@paper_id AS bigint) IS NULL OR a.paper_id = @paper_id)
AND
(
    @search = ''
    OR CAST(a.assignment_id AS TEXT) ILIKE '%' || @search || '%'
    OR COALESCE(nm.exam_name, '') ILIKE '%' || @search || '%'
    OR COALESCE(nm.paper_name, '') ILIKE '%' || @search || '%'
    OR COALESCE(nm.section_name, '') ILIKE '%' || @search || '%'
    OR COALESCE(nm.subsection_name, '') ILIKE '%' || @search || '%'
    OR COALESCE(nm.topic_name, '') ILIKE '%' || @search || '%'
    OR COALESCE(setter.full_name, '') ILIKE '%' || @search || '%'
    OR COALESCE(vetter.full_name, '') ILIKE '%' || @search || '%'
)
ORDER BY a.assignment_id DESC;
";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("search", (search ?? "").Trim());
                    command.Parameters.AddWithValue("exam_id", NpgsqlTypes.NpgsqlDbType.Bigint, (object)examId ?? DBNull.Value);
                    command.Parameters.AddWithValue("paper_id", NpgsqlTypes.NpgsqlDbType.Bigint, (object)paperId ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            decimal factor = Convert.ToDecimal(reader["multiplication_factor"]);

                            int easyReq = Required(reader["easy_sum"], factor);
                            int mediumReq = Required(reader["medium_sum"], factor);
                            int hardReq = Required(reader["hard_sum"], factor);

                            int completed =
                                Math.Min(Convert.ToInt32(reader["easy_created"]), easyReq)
                              + Math.Min(Convert.ToInt32(reader["medium_created"]), mediumReq)
                              + Math.Min(Convert.ToInt32(reader["hard_created"]), hardReq);

                            int target = easyReq + mediumReq + hardReq;

                            result.Add(new TosAssignmentManagementRow
                            {
                                AssignmentId = Convert.ToInt64(reader["assignment_id"]),
                                AssignmentLevel = reader["assignment_level"].ToString(),
                                MultiplicationFactor = factor,
                                MainExamId = Convert.ToInt64(reader["main_exam_id"]),
                                PaperId = reader["paper_id"] == DBNull.Value ? (long?)null : Convert.ToInt64(reader["paper_id"]),

                                SetterUserId = Convert.ToInt64(reader["setter_user_id"]),
                                VetterUserId = reader["vetter_user_id"] == DBNull.Value
                                    ? (long?)null
                                    : Convert.ToInt64(reader["vetter_user_id"]),

                                SetterName = reader["setter_name"] == DBNull.Value ? "" : reader["setter_name"].ToString(),
                                VetterName = reader["vetter_name"] == DBNull.Value
                                    ? "Not assigned"
                                    : reader["vetter_name"].ToString(),

                                ExamName = reader["exam_name"].ToString(),
                                PaperName = reader["paper_name"].ToString(),
                                SectionName = reader["section_name"].ToString(),
                                SubsectionName = reader["subsection_name"].ToString(),
                                TopicName = reader["topic_name"].ToString(),

                                TotalQuestions = target,
                                CompletedQuestions = completed,
                                RemainingQuestions = Math.Max(0, target - completed),
                                ApprovedQuestions = Convert.ToInt32(reader["approved_questions"]),

                                IsActive = Convert.ToBoolean(reader["is_active"]),
                                AssignedAt = reader["assigned_at"] == DBNull.Value
                                    ? (DateTime?)null
                                    : Convert.ToDateTime(reader["assigned_at"])
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

        public async Task<ReassignTosAssignmentViewModel>
            GetForReassignmentAsync(long assignmentId)
        {
            const string sql = @"
SELECT
    a.assignment_id,
    a.assignment_level,
    a.setter_user_id,
    a.vetter_user_id,
    COALESCE(t.main_exam_name, '') AS exam_name,
    COALESCE(t.paper_name, '') AS paper_name,
    COALESCE(t.section_name, '') AS section_name,
    COALESCE(t.subsection_name, '') AS subsection_name,
    COALESCE(t.topic_name, '') AS topic_name
FROM question_assignment a
LEFT JOIN tos t
    ON t.main_exam_id = a.main_exam_id
   AND (a.paper_id IS NULL OR t.paper_id = a.paper_id)
   AND (a.section_id IS NULL OR t.section_id = a.section_id)
   AND (a.subsection_id IS NULL OR t.subsection_id = a.subsection_id)
   AND (a.topic_id IS NULL OR t.topic_id = a.topic_id)
WHERE a.assignment_id = @id
GROUP BY
    a.assignment_id,
    a.assignment_level,
    a.setter_user_id,
    a.vetter_user_id,
    t.main_exam_name,
    t.paper_name,
    t.section_name,
    t.subsection_name,
    t.topic_name
LIMIT 1;
";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("id", assignmentId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return null;

                        return new ReassignTosAssignmentViewModel
                        {
                            AssignmentId = Convert.ToInt64(reader["assignment_id"]),
                            AssignmentLevel = reader["assignment_level"].ToString(),
                            CurrentSetterUserId = Convert.ToInt64(reader["setter_user_id"]),

                            CurrentVetterUserId =
                                reader["vetter_user_id"] == DBNull.Value
                                    ? (long?)null
                                    : Convert.ToInt64(reader["vetter_user_id"]),

                            ExamName = reader["exam_name"].ToString(),
                            PaperName = reader["paper_name"].ToString(),
                            SectionName = reader["section_name"].ToString(),
                            SubsectionName = reader["subsection_name"].ToString(),
                            TopicName = reader["topic_name"].ToString()
                        };
                    }
                }
            }
        }

        public async Task ReassignAsync(
            long assignmentId,
            long newSetterUserId,
            long newVetterUserId,
            long assignedBy,
            bool transferUnfinishedQuestions)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        const string updateAssignmentSql = @"
UPDATE question_assignment
SET setter_user_id = @setter,
    vetter_user_id = @vetter,
    assigned_by = @assigned_by,
    assigned_at = CURRENT_TIMESTAMP
WHERE assignment_id = @id
  AND is_active = TRUE;
";

                        using (var command = new NpgsqlCommand(
                            updateAssignmentSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("id", assignmentId);
                            command.Parameters.AddWithValue("setter", newSetterUserId);
                            command.Parameters.AddWithValue("vetter", newVetterUserId);
                            command.Parameters.AddWithValue("assigned_by", assignedBy);

                            var rows = await command.ExecuteNonQueryAsync();

                            if (rows != 1)
                                throw new InvalidOperationException(
                                    "Assignment was not found or is inactive.");
                        }

                        if (transferUnfinishedQuestions)
                        {
                            // Only unfinished Setter-owned questions are transferred.
                            // Completed questions and pending Vetter reviews are unchanged.
                            const string transferSql = @"
UPDATE question
SET created_by = @new_setter
WHERE assignment_id = @assignment_id
  AND status IN ('DRAFT', 'VETTER_REJECTED', 'ADMIN_REJECTED')
  AND created_by <> @new_setter;
";

                            using (var command = new NpgsqlCommand(
                                transferSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue(
                                    "assignment_id", assignmentId);

                                command.Parameters.AddWithValue(
                                    "new_setter", newSetterUserId);

                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<long> CreateAsync(TosAssignmentRequest request,long assignedBy)
        {
            const string sql = @"Insert into question_assignment(assignment_level,main_exam_id,paper_id,section_id,subsection_id,topic_id,setter_user_id,vetter_user_id,
multiplication_factor,assigned_by) Values(
@assignment_level,
@main_exam_id,
@paper_id,
@section_id,
@subsection_id,
@topic_id,
@setter_user_id,
@vetter_user_id,
@multiplication_factor,
@assigned_by
)
RETURNING assignment_id;
";
            using (var connection=new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command=new NpgsqlCommand(sql,connection))
                {
                    command.Parameters.AddWithValue("assignment_level",request.AssignmentLevel);
                    command.Parameters.AddWithValue("main_exam_id", request.ExamId);
                    command.Parameters.AddWithValue("paper_id", (object)request.PaperId ?? DBNull.Value);
                    command.Parameters.AddWithValue("section_id", (object)request.SectionId ?? DBNull.Value);
                    command.Parameters.AddWithValue("subsection_id", (object)request.SubsectionId ?? DBNull.Value);
                    command.Parameters.AddWithValue("topic_id", (object)request.TopicId ?? DBNull.Value);
                    command.Parameters.AddWithValue("setter_user_id", request.SetterUserId);
                    command.Parameters.AddWithValue("vetter_user_id", request.VetterUserId);
                    command.Parameters.AddWithValue("multiplication_factor", request.MultiplicationFactor);
                    command.Parameters.AddWithValue("assigned_by", assignedBy);
                    var result = await command.ExecuteScalarAsync();
                    return System.Convert.ToInt64(result);
                }
            }
        }
    }
}
