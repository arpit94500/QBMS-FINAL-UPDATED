using System;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;

namespace QBMS.Infrastructure.Services
{
    public class QuestionWorkflowRepository
        : IQuestionWorkflowRepository
    {
        private readonly string _connectionString;

        public QuestionWorkflowRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<bool> SubmitToVetterAsync(
            long questionId,
            long setterUserId)
        {

const string sql = @"UPDATE question
SET
    status = 'SUBMITTED_TO_VETTER',
    updated_at = CURRENT_TIMESTAMP
WHERE question_id = @question_id
  AND created_by = @setter_user_id
  AND status IN
  (
      'DRAFT',
      'VETTER_REJECTED',
      'ADMIN_REJECTED'
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
                        "question_id",
                        questionId);

                    command.Parameters.AddWithValue(
                        "setter_user_id",
                        setterUserId);

                    var rows =
                        await command.ExecuteNonQueryAsync();

                    return rows > 0;
                }
            }
        }

        public async Task<bool> ReviewQuestionAsync(
            long questionId,
            long vetterUserId,
            string decision,
            string remarks)
        {
            string status;

            if (decision == "APPROVE")
            {
                status = "VETTER_APPROVED";
            }
            else if (decision == "REJECT")
            {
                status = "VETTER_REJECTED";
            }
            else
            {
                throw new ArgumentException(
                    "Invalid review decision.");
            }

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    await connection.BeginTransactionAsync())
                {
                    const string questionSql = @"
UPDATE question
SET
    status = @status,
    updated_at = CURRENT_TIMESTAMP
WHERE question_id = @question_id
  AND status = 'SUBMITTED_TO_VETTER';
";

                    int updatedRows;

                    using (var command =
                        new NpgsqlCommand(
                            questionSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "status",
                            status);

                        command.Parameters.AddWithValue(
                            "question_id",
                            questionId);

                        updatedRows =
                            await command.ExecuteNonQueryAsync();
                    }

                    if (updatedRows == 0)
                    {
                        await transaction.RollbackAsync();

                        return false;
                    }

                    const string reviewSql = @"
INSERT INTO question_review
(
    question_id,
    reviewer_id,
    review_type,
    decision,
    remarks
)
VALUES
(
    @question_id,
    @reviewer_id,
    'VETTER',
    @decision,
    @remarks
);
";

                    using (var command =
                        new NpgsqlCommand(
                            reviewSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "question_id",
                            questionId);

                        command.Parameters.AddWithValue(
                            "reviewer_id",
                            vetterUserId);

                        command.Parameters.AddWithValue(
                            "decision",
                            decision);

                        command.Parameters.AddWithValue(
                            "remarks",
                            (object)remarks
                            ?? DBNull.Value);

                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    return true;
                }
            }
        }
    }
}
