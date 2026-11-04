using System;
using System.Threading.Tasks;

using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using QBMS.Infrastructure.Services;

namespace QBMS.Infrastructure.Repositories
{
    public class VetterQuestionReviewRepository
        : IVetterQuestionReviewRepository
    {
        private readonly string _connectionString;

        //private readonly string _connectionString;

        public VetterQuestionReviewRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        // =========================================================
        // FIRST PENDING QUESTION
        // =========================================================

        public async Task<VetterQuestionReviewViewModel>
            GetFirstQuestionAsync(long vetterUserId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
                    SELECT q.question_id
                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id = q.assignment_id

                    WHERE qa.vetter_user_id = @VetterUserId
                      AND qa.is_active = TRUE
                      AND q.status = 'SUBMITTED_TO_VETTER'

                    ORDER BY q.question_id ASC

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result == null)
                        return null;

                    var questionId =
                        Convert.ToInt64(result);

                    return await GetQuestionAsync(
                        vetterUserId,
                        questionId);
                }
            }
        }

        // =========================================================
        // FIRST PENDING QUESTION OF A GIVEN EXAM
        // =========================================================

        public async Task<long?>
            GetFirstQuestionIdByExamAsync(
                long vetterUserId,
                long examId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
                    SELECT q.question_id
                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id = q.assignment_id

                    WHERE qa.vetter_user_id = @VetterUserId
                      AND qa.is_active = TRUE
                      AND q.main_exam_id = @ExamId
                      AND q.status = 'SUBMITTED_TO_VETTER'

                    ORDER BY q.question_id ASC

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    command.Parameters.AddWithValue(
                        "@ExamId",
                        examId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result == null)
                        return null;

                    return Convert.ToInt64(result);
                }
            }
        }

        // =========================================================
        // GET QUESTION
        // =========================================================

        public async Task<VetterQuestionReviewViewModel>
            GetQuestionAsync(
                long vetterUserId,
                long questionId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
                    SELECT
                        q.question_id,
                        q.assignment_id,
                        q.main_exam_id,
                        q.paper_id,
                        q.section_id,
                        q.subsection_id,
                        q.topic_id,

                        COALESCE(t.main_exam_name, '') AS main_exam_name,
                        COALESCE(t.paper_name, '') AS paper_name,
                        COALESCE(t.section_name, '') AS section_name,
                        COALESCE(t.subsection_name, '') AS subsection_name,
                        COALESCE(t.topic_name, '') AS topic_name,

                        q.question_type,
                        q.difficulty_level,
                        q.question_text,

                        q.option1,
                        q.option2,
                        q.option3,
                        q.option4,
                        q.correct_answer,

                        q.status,
                        q.latest_remarks

                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id =
                           q.assignment_id

                    LEFT JOIN tos t
                        ON t.main_exam_id = q.main_exam_id
                       AND t.paper_id = q.paper_id
                       AND t.section_id = q.section_id
                       AND t.topic_id = q.topic_id

                    WHERE q.question_id = @QuestionId

                      AND qa.vetter_user_id =
                          @VetterUserId

                      AND qa.is_active = TRUE

                      AND q.status =
                          'SUBMITTED_TO_VETTER'

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@QuestionId",
                        questionId);

                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return null;

                        return new VetterQuestionReviewViewModel
                        {
                            QuestionId =
                                reader.GetInt64(
                                    reader.GetOrdinal(
                                        "question_id")),

                            AssignmentId =
                                reader.GetInt64(
                                    reader.GetOrdinal(
                                        "assignment_id")),

                            MainExamId =
                                reader.GetInt64(
                                    reader.GetOrdinal(
                                        "main_exam_id")),

                            PaperId =
                                reader.GetInt64(
                                    reader.GetOrdinal(
                                        "paper_id")),

                            SectionId =
                                reader.GetInt64(
                                    reader.GetOrdinal(
                                        "section_id")),

                            SubsectionId =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "subsection_id"))
                                ? (long?)null
                                : reader.GetInt64(
                                    reader.GetOrdinal(
                                        "subsection_id")),

                            TopicId =
                                reader.GetInt64(
                                    reader.GetOrdinal(
                                        "topic_id")),

                            ExamName =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "main_exam_name"))
                                ? ""
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "main_exam_name")),

                            PaperName =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "paper_name"))
                                ? ""
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "paper_name")),

                            SectionName =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "section_name"))
                                ? ""
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "section_name")),

                            SubsectionName =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "subsection_name"))
                                ? ""
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "subsection_name")),

                            TopicName =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "topic_name"))
                                ? ""
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "topic_name")),

                            QuestionType =
                                reader.GetString(
                                    reader.GetOrdinal(
                                        "question_type")),

                            DifficultyLevel =
                                reader.GetString(
                                    reader.GetOrdinal(
                                        "difficulty_level")),

                            QuestionText =
                                EncryptionEnhanced.Decrypt(reader.GetString(
                                    reader.GetOrdinal(
                                        "question_text"))),

                            Option1 =
                                EncryptionEnhanced.Decrypt(reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "option1"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "option1"))),

                            Option2 =
                                EncryptionEnhanced.Decrypt(reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "option2"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "option2"))),

                            Option3 =
                                EncryptionEnhanced.Decrypt(reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "option3"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "option3"))),

                            Option4 =
                                EncryptionEnhanced.Decrypt(reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "option4"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "option4"))),

                            CorrectAnswer =
                                EncryptionEnhanced.Decrypt(reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "correct_answer"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "correct_answer"))),

                            Status =
                                reader.GetString(
                                    reader.GetOrdinal(
                                        "status")),

                            LatestRemarks =
                                reader.IsDBNull(
                                    reader.GetOrdinal(
                                        "latest_remarks"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal(
                                        "latest_remarks"))
                        };
                    }
                }
            }
        }

        // =========================================================
        // PREVIOUS
        // =========================================================

        public async Task<long?>
            GetPreviousQuestionIdAsync(
                long vetterUserId,
                long questionId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
                    SELECT q.question_id

                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id =
                           q.assignment_id

                    WHERE qa.vetter_user_id =
                          @VetterUserId

                      AND qa.is_active = TRUE

                      AND q.status =
                          'SUBMITTED_TO_VETTER'

                      AND q.question_id <
                          @QuestionId

                    ORDER BY q.question_id DESC

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    command.Parameters.AddWithValue(
                        "@QuestionId",
                        questionId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result == null)
                        return null;

                    return Convert.ToInt64(result);
                }
            }
        }

        // =========================================================
        // NEXT
        // =========================================================

        public async Task<long?>
            GetNextQuestionIdAsync(
                long vetterUserId,
                long questionId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
                    SELECT q.question_id

                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id =
                           q.assignment_id

                    WHERE qa.vetter_user_id =
                          @VetterUserId

                      AND qa.is_active = TRUE

                      AND q.status =
                          'SUBMITTED_TO_VETTER'

                      AND q.question_id >
                          @QuestionId

                    ORDER BY q.question_id ASC

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    command.Parameters.AddWithValue(
                        "@QuestionId",
                        questionId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result == null)
                        return null;

                    return Convert.ToInt64(result);
                }
            }
        }

        // =========================================================
        // RESUME
        // =========================================================

        public async Task<long?>
            GetResumeQuestionIdAsync(
                long vetterUserId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // First find the last question on which
                // the Vetter was working.
                const string sql = @"
                    SELECT
                        p.current_question_id

                    FROM vetter_review_progress p

                    WHERE p.vetter_user_id =
                          @VetterUserId

                    LIMIT 1;
                ";

                long? savedQuestionId = null;

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result != null)
                    {
                        savedQuestionId =
                            Convert.ToInt64(result);
                    }
                }

                if (!savedQuestionId.HasValue)
                {
                    return null;
                }

                // Check whether the saved question
                // is still pending.
                const string pendingSql = @"
                    SELECT q.question_id

                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id =
                           q.assignment_id

                    WHERE q.question_id =
                          @QuestionId

                      AND qa.vetter_user_id =
                          @VetterUserId

                      AND qa.is_active = TRUE

                      AND q.status =
                          'SUBMITTED_TO_VETTER'

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(
                        pendingSql,
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "@QuestionId",
                        savedQuestionId.Value);

                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result != null)
                    {
                        return savedQuestionId.Value;
                    }
                }

                // Saved question is already approved/rejected.
                // Resume from the next pending question.
                const string nextSql = @"
                    SELECT q.question_id

                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id =
                           q.assignment_id

                    WHERE qa.vetter_user_id =
                          @VetterUserId

                      AND qa.is_active = TRUE

                      AND q.status =
                          'SUBMITTED_TO_VETTER'

                      AND q.question_id >
                          @QuestionId

                    ORDER BY q.question_id ASC

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(
                        nextSql,
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    command.Parameters.AddWithValue(
                        "@QuestionId",
                        savedQuestionId.Value);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result != null)
                    {
                        return Convert.ToInt64(result);
                    }
                }

                // If no question after the saved question,
                // start from the first pending question.
                const string firstSql = @"
                    SELECT q.question_id

                    FROM question q

                    INNER JOIN question_assignment qa
                        ON qa.assignment_id =
                           q.assignment_id

                    WHERE qa.vetter_user_id =
                          @VetterUserId

                      AND qa.is_active = TRUE

                      AND q.status =
                          'SUBMITTED_TO_VETTER'

                    ORDER BY q.question_id ASC

                    LIMIT 1;
                ";

                using (var command =
                    new NpgsqlCommand(
                        firstSql,
                        connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    var result =
                        await command.ExecuteScalarAsync();

                    if (result == null)
                        return null;

                    return Convert.ToInt64(result);
                }
            }
        }

        // =========================================================
        // SAVE PROGRESS
        // =========================================================

        public async Task <bool>SaveProgressAsync(
            long vetterUserId,
            long questionId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                const string sql = @"
                    INSERT INTO vetter_review_progress
                    (
                        vetter_user_id,
                        current_question_id,
                        last_updated_at
                    )
                    VALUES
                    (
                        @VetterUserId,
                        @QuestionId,
                        CURRENT_TIMESTAMP
                    )

                    ON CONFLICT (vetter_user_id)

                    DO UPDATE SET
                        current_question_id =
                            EXCLUDED.current_question_id,

                        last_updated_at =
                            CURRENT_TIMESTAMP;
                ";

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@VetterUserId",
                        vetterUserId);

                    command.Parameters.AddWithValue(
                        "@QuestionId",
                        questionId);

                    await command.ExecuteNonQueryAsync();
                }
                return true;
            }
        }

        // =========================================================
        // APPROVE
        // =========================================================

        public async Task<bool>
            ApproveQuestionAsync(
                long vetterUserId,
                long questionId,
                string remarks)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Verify question belongs to this Vetter.
                        const string verifySql = @"
                            SELECT q.question_id

                            FROM question q

                            INNER JOIN question_assignment qa
                                ON qa.assignment_id =
                                   q.assignment_id

                            WHERE q.question_id =
                                  @QuestionId

                              AND qa.vetter_user_id =
                                  @VetterUserId

                              AND qa.is_active = TRUE

                              AND q.status =
                                  'SUBMITTED_TO_VETTER'

                            LIMIT 1;
                        ";

                        using (var verifyCommand =
                            new NpgsqlCommand(
                                verifySql,
                                connection,
                                transaction))
                        {
                            verifyCommand.Parameters.AddWithValue(
                                "@QuestionId",
                                questionId);

                            verifyCommand.Parameters.AddWithValue(
                                "@VetterUserId",
                                vetterUserId);

                            var result =
                                await verifyCommand
                                    .ExecuteScalarAsync();

                            if (result == null)
                            {
                                await transaction.RollbackAsync();
                                return false;
                            }
                        }

                        // Update question status.
                        const string updateSql = @"
                            UPDATE question

                            SET
                                status =
                                    'VETTER_APPROVED',

                                latest_remarks =
                                    @Remarks,

                                updated_at =
                                    CURRENT_TIMESTAMP

                            WHERE question_id =
                                  @QuestionId;
                        ";

                        using (var updateCommand =
                            new NpgsqlCommand(
                                updateSql,
                                connection,
                                transaction))
                        {
                            updateCommand.Parameters.AddWithValue(
                                "@QuestionId",
                                questionId);

                            updateCommand.Parameters.AddWithValue(
                                "@Remarks",
                                (object)remarks ??
                                DBNull.Value);

                            await updateCommand
                                .ExecuteNonQueryAsync();
                        }

                        // Store Vetter review.
                        const string reviewSql = @"
                            INSERT INTO question_review
                            (
                                question_id,
                                reviewer_id,
                                review_type,
                                decision,
                                remarks,
                                reviewed_at
                            )
                            VALUES
                            (
                                @QuestionId,
                                @ReviewerId,
                                'VETTER',
                                'APPROVED',
                                @Remarks,
                                CURRENT_TIMESTAMP
                            );
                        ";

                        using (var reviewCommand =
                            new NpgsqlCommand(
                                reviewSql,
                                connection,
                                transaction))
                        {
                            reviewCommand.Parameters.AddWithValue(
                                "@QuestionId",
                                questionId);

                            reviewCommand.Parameters.AddWithValue(
                                "@ReviewerId",
                                vetterUserId);

                            reviewCommand.Parameters.AddWithValue(
                                "@Remarks",
                                (object)remarks ??
                                DBNull.Value);

                            await reviewCommand
                                .ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();

                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        // =========================================================
        // REJECT
        // =========================================================

        public async Task<bool>
            RejectQuestionAsync(
                long vetterUserId,
                long questionId,
                string remarks)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    await connection.BeginTransactionAsync())
                {
                    try
                    {
                        // Verify question belongs to this Vetter.
                        const string verifySql = @"
                            SELECT q.question_id

                            FROM question q

                            INNER JOIN question_assignment qa
                                ON qa.assignment_id =
                                   q.assignment_id

                            WHERE q.question_id =
                                  @QuestionId

                              AND qa.vetter_user_id =
                                  @VetterUserId

                              AND qa.is_active = TRUE

                              AND q.status =
                                  'SUBMITTED_TO_VETTER'

                            LIMIT 1;
                        ";

                        using (var verifyCommand =
                            new NpgsqlCommand(
                                verifySql,
                                connection,
                                transaction))
                        {
                            verifyCommand.Parameters.AddWithValue(
                                "@QuestionId",
                                questionId);

                            verifyCommand.Parameters.AddWithValue(
                                "@VetterUserId",
                                vetterUserId);

                            var result =
                                await verifyCommand
                                    .ExecuteScalarAsync();

                            if (result == null)
                            {
                                await transaction.RollbackAsync();
                                return false;
                            }
                        }

                        // Rejected question returns to Setter.
                        const string updateSql = @"
                            UPDATE question

                            SET
                                status =
                                    'VETTER_REJECTED',

                                latest_remarks =
                                    @Remarks,

                                updated_at =
                                    CURRENT_TIMESTAMP

                            WHERE question_id =
                                  @QuestionId;
                        ";

                        using (var updateCommand =
                            new NpgsqlCommand(
                                updateSql,
                                connection,
                                transaction))
                        {
                            updateCommand.Parameters.AddWithValue(
                                "@QuestionId",
                                questionId);

                            updateCommand.Parameters.AddWithValue(
                                "@Remarks",
                                (object)remarks ??
                                DBNull.Value);

                            await updateCommand
                                .ExecuteNonQueryAsync();
                        }

                        // Store Vetter review.
                        const string reviewSql = @"
                            INSERT INTO question_review
                            (
                                question_id,
                                reviewer_id,
                                review_type,
                                decision,
                                remarks,
                                reviewed_at
                            )
                            VALUES
                            (
                                @QuestionId,
                                @ReviewerId,
                                'VETTER',
                                'REJECTED',
                                @Remarks,
                                CURRENT_TIMESTAMP
                            );
                        ";

                        using (var reviewCommand =
                            new NpgsqlCommand(
                                reviewSql,
                                connection,
                                transaction))
                        {
                            reviewCommand.Parameters.AddWithValue(
                                "@QuestionId",
                                questionId);

                            reviewCommand.Parameters.AddWithValue(
                                "@ReviewerId",
                                vetterUserId);

                            reviewCommand.Parameters.AddWithValue(
                                "@Remarks",
                                (object)remarks ??
                                DBNull.Value);

                            await reviewCommand
                                .ExecuteNonQueryAsync();
                        }

                        await transaction.CommitAsync();

                        return true;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }
        public async Task<bool> SaveQuestionReviewAsync(long vetterUserId, long questionId, string questionText, string option1, string option2, string option3,
    string option4, string correctAnswer, string remarks, string action)
        {
            using (var connection=new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var transaction=await connection.BeginTransactionAsync())
                {
                    try
                    {
                        const string verifySql = @"Select q.question_id from question q inner join question_assignment qa on qa.assignment_id=q.assignment_id
where q.question_id=@QuestionId
and qa.vetter_user_id=@VetterUserId and qa.is_active=True and q.status='SUBMITTED_TO_VETTER' LIMIT 1;";
                        using (var verifyCommand=new NpgsqlCommand(verifySql, connection, transaction))
                        {
                            verifyCommand.Parameters.AddWithValue("@QuestionId",questionId);
                            verifyCommand.Parameters.AddWithValue("@VetterUserId", vetterUserId);
                            var result = await verifyCommand.ExecuteScalarAsync();
                            if (result == null)
                            {
                                await transaction.RollbackAsync();
                                return false;
                            }
                        }
                        string newStatus = "SUBMITTED_TO_VETTER";
                        if (action == "APPROVE")
                        {
                            newStatus = "VETTER_APPROVED";
                        }
                        else if(action=="REJECT")
                        {
                            newStatus = "VETTER_REJECTED"; ;
                        }

                        const string updateSql = @"UPDATE question SET question_text=@QuestionText,
option1=@Option1,
option2=@Option2,
option3=@Option3,
option4=@Option4,
correct_answer=@CorrectAnswer,
status=@Status,
latest_remarks=@Remarks,
updated_at=CURRENT_TIMESTAMP
where question_id=@QuestionId;
";
                        using (var updateCommand=new NpgsqlCommand(updateSql, connection, transaction))
                        {
                            updateCommand.Parameters.AddWithValue("@QuestionId",questionId);
                            updateCommand.Parameters.AddWithValue("@QuestionText", EncryptionEnhanced.Encrypt((string)((object) questionText)));
                            updateCommand.Parameters.AddWithValue("@Option1", EncryptionEnhanced.Encrypt((string)((object)option1)));
                            updateCommand.Parameters.AddWithValue("@Option2", EncryptionEnhanced.Encrypt((string)((object)option2 )));
                            updateCommand.Parameters.AddWithValue("@Option3", EncryptionEnhanced.Encrypt((string)((object)option3 )));
                            updateCommand.Parameters.AddWithValue("@Option4", EncryptionEnhanced.Encrypt((string)((object)option4 )));
                            updateCommand.Parameters.AddWithValue("@CorrectAnswer", EncryptionEnhanced.Encrypt((string)((object)correctAnswer )));
                            updateCommand.Parameters.AddWithValue("@Status", (string)((object)newStatus ?? DBNull.Value));
                            updateCommand.Parameters.AddWithValue("@Remarks", (string)((object)remarks?? DBNull.Value));
                            await updateCommand.ExecuteNonQueryAsync();
                        }
                        if(action=="APPROVE"|| action == "REJECT")
                        {
                            string decision = action == "APPROVE" ? "APPROVED" : "REJECTED";
                            const string reviewSql = @"INSERT INTO question_review(question_id, reviewer_id,review_type,decision,remarks,reviewed_at) values(@QuestionId,@ReviewerId,'VETTER',@Decision,@Remarks,CURRENT_TIMESTAMP);";
                            using (var reviewCommand=new NpgsqlCommand(reviewSql, connection, transaction))
                            {
                                reviewCommand.Parameters.AddWithValue("@QuestionId",questionId);
                                reviewCommand.Parameters.AddWithValue("@ReviewerId",vetterUserId);
                                reviewCommand.Parameters.AddWithValue("@Decision",decision);
                                reviewCommand.Parameters.AddWithValue("@Remarks",(object)remarks ?? DBNull.Value);
                                await reviewCommand.ExecuteNonQueryAsync();
                            }
                        }
                        await transaction.CommitAsync();
                        return true;
                    }catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        //Task IVetterQuestionReviewRepository.SaveProgressAsync(long vetterUserId, long questionId)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
