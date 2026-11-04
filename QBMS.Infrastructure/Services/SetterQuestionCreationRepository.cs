using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using System.Configuration;
using System.Linq;

namespace QBMS.Infrastructure.Services
{
    public class SetterQuestionCreationRepository : ISetterQuestionCreationRepository
    {     
        private readonly string _connectionString;

        public SetterQuestionCreationRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        /* =========================================================
           ASSIGNMENT
           ========================================================= */

        public async Task<SetterAssignmentInfo> GetAssignmentAsync(
            long assignmentId,
            long setterUserId)
        {
            const string sql = @"
                SELECT
                    assignment_id,
                    assignment_level,
                    main_exam_id,
                    paper_id,
                    section_id,
                    subsection_id,
                    topic_id,
                    multiplication_factor
                FROM question_assignment
                WHERE assignment_id = @assignmentId
                  AND setter_user_id = @setterUserId
                  AND is_active = TRUE
                LIMIT 1;
            ";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@assignmentId",assignmentId);

                    command.Parameters.AddWithValue("@setterUserId",setterUserId);

                    using (var reader =await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        return new SetterAssignmentInfo
                        {
                            AssignmentId = reader.GetInt64(0),

                            AssignmentLevel = reader.GetString(1),

                            MainExamId = reader.GetInt64(2),

                            PaperId = reader.IsDBNull(3) ? (long?)null : reader.GetInt64(3),

                            SectionId = reader.IsDBNull(4)  ? (long?)null: reader.GetInt64(4),

                            SubsectionId =reader.IsDBNull(5)? (long?)null : reader.GetInt64(5),

                            TopicId = reader.IsDBNull(6)? (long?)null: reader.GetInt64(6),

                            MultiplicationFactor = reader.GetDecimal(7)
                        };
                    }
                }
            }
        }


        /* =========================================================
           TOPICS FOR ASSIGNMENT
           ========================================================= */

        public async Task<List<CreateQuestionViewModel>> GetTopicsAsync(
            long assignmentId,
            long setterUserId)
        {
            const string sql = @"
                SELECT DISTINCT
                    t.topic_id,
                    t.topic_name,
                    t.main_exam_id,
                    t.main_exam_name,
                    t.paper_id,
                    t.paper_name,
                    t.section_id,
                    t.section_name,
                    t.subsection_id,
                    t.subsection_name,
                    t.topic_type
                FROM question_assignment a
                INNER JOIN tos t
                    ON t.main_exam_id = a.main_exam_id
                   AND
                   (
                        a.assignment_level = 'EXAM'

                        OR
                        (
                            a.assignment_level = 'PAPER'
                            AND t.paper_id = a.paper_id
                        )

                        OR
                        (
                            a.assignment_level = 'SECTION'
                            AND t.paper_id = a.paper_id
                            AND t.section_id = a.section_id
                        )

                        OR
                        (
                            a.assignment_level = 'SUBSECTION'
                            AND t.paper_id = a.paper_id
                            AND t.section_id = a.section_id
                            AND t.subsection_id = a.subsection_id
                        )

                        OR
                        (
                            a.assignment_level = 'TOPIC'
                            AND t.paper_id = a.paper_id
                            AND t.section_id = a.section_id
                            AND t.subsection_id IS NOT DISTINCT FROM a.subsection_id
                            AND t.topic_id = a.topic_id
                        )
                   )
                WHERE a.assignment_id = @assignmentId
                  AND a.setter_user_id = @setterUserId
                  AND a.is_active = TRUE
                  AND t.is_active = 1
                ORDER BY
                    t.main_exam_name,
                    t.paper_name,
                    t.section_name,
                    t.subsection_name,
                    t.topic_name;
            ";

            var result =new List<CreateQuestionViewModel>();

            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@assignmentId",assignmentId);

                    command.Parameters.AddWithValue("@setterUserId",setterUserId);

                    using (var reader =await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add( new CreateQuestionViewModel
                                {
                                    TopicId = reader.GetInt64(0),

                                    TopicName = reader.GetString(1),

                                    MainExamId =reader.GetInt64(2),

                                    ExamName = reader.IsDBNull(3)? null: reader.GetString(3),

                                    PaperId =reader.GetInt64(4),

                                    PaperName = reader.IsDBNull(5)? null: reader.GetString(5),

                                    SectionId =reader.GetInt64(6),

                                    SectionName = reader.IsDBNull(7)? null: reader.GetString(7),

                                    SubsectionId = reader.IsDBNull(8)? (long?)null: reader.GetInt64(8),

                                    SubsectionName =reader.IsDBNull(9)? null: reader.GetString(9),
                                    TopicType = NormalizeQuestionType(reader.IsDBNull(10) ? null : reader.GetString(10))
                                });
                        }
                    }
                }
            }

            return result;
        }


        /* =========================================================
           GET QUESTION
           ========================================================= */

        public async Task<CreateQuestionViewModel> GetQuestionAsync(
            long assignmentId,
            long setterUserId,
            long questionId)
        {
            const string sql = @"
                SELECT
                    q.question_id,
                    q.assignment_id,
                    q.main_exam_id,
                    q.paper_id,
                    q.section_id,
                    q.subsection_id,
                    q.topic_id,

                    q.question_text,
                    q.option1,
                    q.option2,
                    q.option3,
                    q.option4,
                    q.correct_answer,

                    q.question_type,
                    q.difficulty_level,
                    q.status,
                    q.latest_remarks,

                    t.main_exam_name,
                    t.paper_name,
                    t.section_name,
                    t.subsection_name,
                    t.topic_name

                FROM question q

                LEFT JOIN tos t
                    ON t.main_exam_id = q.main_exam_id
                   AND t.paper_id = q.paper_id
                   AND t.section_id = q.section_id
                   AND t.subsection_id IS NOT DISTINCT FROM q.subsection_id
                   AND t.topic_id = q.topic_id

                WHERE q.question_id = @questionId
                  AND q.assignment_id = @assignmentId
                  AND q.created_by = @setterUserId

                ORDER BY t.topic_id
                LIMIT 1;
            ";

            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue( "@questionId",questionId);

                    command.Parameters.AddWithValue("@assignmentId",assignmentId);

                    command.Parameters.AddWithValue( "@setterUserId",setterUserId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        return new CreateQuestionViewModel
                        {
                            QuestionId =  reader.GetInt64(0),

                            AssignmentID = reader.GetInt64(1),

                            MainExamId = reader.GetInt64(2),

                            PaperId = reader.GetInt64(3),

                            SectionId =reader.GetInt64(4),

                            SubsectionId = reader.IsDBNull(5)? (long?)null: reader.GetInt64(5),

                            TopicId =reader.GetInt64(6),

                            QuestionText = EncryptionEnhanced.Decrypt(reader.IsDBNull(7)? null: reader.GetString(7)),

                            Option1 =EncryptionEnhanced.Decrypt(reader.IsDBNull(8)? null: reader.GetString(8)),

                            Option2 = EncryptionEnhanced.Decrypt(reader.IsDBNull(9)? null: reader.GetString(9)),

                            Option3 = EncryptionEnhanced.Decrypt(reader.IsDBNull(10)? null: reader.GetString(10)),

                            Option4 = EncryptionEnhanced.Decrypt(reader.IsDBNull(11)? null: reader.GetString(11)),

                            CorrectAnswer = EncryptionEnhanced.Decrypt(reader.IsDBNull(12)? null: reader.GetString(12)),

                            QuestionType =reader.IsDBNull(13)? null: NormalizeQuestionType(reader.GetString(13)),

                            DifficultyLevel =reader.IsDBNull(14)? null : reader.GetString(14),

                            Status =reader.IsDBNull(15)? null: reader.GetString(15),

                            LatestRemarks =reader.IsDBNull(16)? null: reader.GetString(16),

                            ExamName =reader.IsDBNull(17)? null: reader.GetString(17),

                            PaperName =reader.IsDBNull(18)? null: reader.GetString(18),

                            SectionName =reader.IsDBNull(19)? null: reader.GetString(19),

                            SubsectionName =reader.IsDBNull(20)? null: reader.GetString(20),

                            TopicName = reader.IsDBNull(21)? null: reader.GetString(21)
                        };
                    }
                }
            }
        }


        /* =========================================================
           RESUME
           ========================================================= */

        public async Task<long?> GetResumeQuestionIdAsync(
            long assignmentId,
            long setterUserId)
        {
            const string sql = @"
                SELECT COALESCE
                (
                    (
                        SELECT q.question_id
                        FROM question_creation_progress p
                        INNER JOIN question q
                            ON q.question_id =
                               p.current_question_id
                        WHERE p.assignment_id = @assignmentId
                          AND p.setter_user_id = @setterUserId
                          AND q.assignment_id = @assignmentId
                          AND q.created_by = @setterUserId
                          AND q.status = 'DRAFT'
                        LIMIT 1
                    ),

                    (
                        SELECT q.question_id
                        FROM question q
                        WHERE q.assignment_id = @assignmentId
                          AND q.created_by = @setterUserId
                          AND q.status = 'DRAFT'
                        ORDER BY q.question_id DESC
                        LIMIT 1
                    )
                );
            ";

            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue( "@assignmentId",assignmentId);

                    command.Parameters.AddWithValue( "@setterUserId", setterUserId);

                    var value = await command.ExecuteScalarAsync();

                    if (value == null ||value == DBNull.Value)
                    {
                        return null;
                    }

                    return Convert.ToInt64(value);
                }
            }
        }


        /* =========================================================
           PREVIOUS
           ========================================================= */

        public async Task<long?> GetPreviousQuestionIdAsync(
            long assignmentId,
            long setterUserId,
            long questionId)
        {
            const string sql = @"
                SELECT question_id
                FROM question
                WHERE assignment_id = @assignmentId
                  AND created_by = @setterUserId
                  AND status = 'DRAFT'
                  AND question_id < @questionId
                ORDER BY question_id DESC
                LIMIT 1;
            ";

            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@assignmentId", assignmentId);

                    command.Parameters.AddWithValue("@setterUserId",setterUserId);

                    command.Parameters.AddWithValue( "@questionId",questionId);

                    var value =await command.ExecuteScalarAsync();

                    if (value == null ||value == DBNull.Value)
                    {
                        return null;
                    }

                    return Convert.ToInt64(value);
                }
            }
        }


        /* =========================================================
           NEXT
           ========================================================= */

        public async Task<long?> GetNextQuestionIdAsync(
            long assignmentId,
            long setterUserId,
            long questionId)
        {
            const string sql = @"
                SELECT question_id
                FROM question
                WHERE assignment_id = @assignmentId
                  AND created_by = @setterUserId
                  AND status = 'DRAFT'
                  AND question_id > @questionId
                ORDER BY question_id ASC
                LIMIT 1;
            ";

            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =   new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue( "@assignmentId",assignmentId);

                    command.Parameters.AddWithValue("@setterUserId",setterUserId);

                    command.Parameters.AddWithValue( "@questionId", questionId);

                    var value =await command.ExecuteScalarAsync();

                    if (value == null ||
                        value == DBNull.Value)
                    {
                        return null;
                    }

                    return Convert.ToInt64(value);
                }
            }
        }


        /* =========================================================
           QUOTA
           ========================================================= */

        public async Task<QuestionQuotaViewModel> GetQuotaAsync(
            long assignmentId,
            long setterUserId)
        {
            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =new NpgsqlCommand(GetQuotaSql(),connection))
                {
                    command.Parameters.AddWithValue("@assignmentId", assignmentId);

                    command.Parameters.AddWithValue( "@setterUserId",setterUserId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return new QuestionQuotaViewModel();
                        }

                        var easyAssigned =ToQuestionCount(reader.GetDecimal(0));

                        var mediumAssigned =ToQuestionCount( reader.GetDecimal(1));

                        var hardAssigned =ToQuestionCount(reader.GetDecimal(2));

                        reader.Close();

                        var used =await GetUsedQuotaAsync(connection, null,assignmentId,setterUserId, 0);

                        return BuildQuota(easyAssigned,mediumAssigned,hardAssigned,used.easy,used.medium,used.hard);
                    }
                }
            }
        }


        private string GetQuotaSql()
        {
            return @"
                SELECT

                    COALESCE(
                        SUM(t.topic_easy_question), 0
                    ) * a.multiplication_factor,

                    COALESCE(
                        SUM(t.topic_medium_question), 0
                    ) * a.multiplication_factor,

                    COALESCE(
                        SUM(t.topic_hard_question), 0
                    ) * a.multiplication_factor

                FROM question_assignment a

                LEFT JOIN tos t
                    ON t.main_exam_id = a.main_exam_id
                   AND t.is_active = 1

                   AND
                   (
                        a.assignment_level = 'EXAM'

                        OR

                        (
                            a.assignment_level = 'PAPER'
                            AND t.paper_id = a.paper_id
                        )

                        OR

                        (
                            a.assignment_level = 'SECTION'
                            AND t.paper_id = a.paper_id
                            AND t.section_id = a.section_id
                        )

                        OR

                        (
                            a.assignment_level = 'SUBSECTION'
                            AND t.paper_id = a.paper_id
                            AND t.section_id = a.section_id
                            AND t.subsection_id = a.subsection_id
                        )

                        OR

                        (
                            a.assignment_level = 'TOPIC'
                            AND t.paper_id = a.paper_id
                            AND t.section_id = a.section_id
                            AND t.subsection_id IS NOT DISTINCT FROM a.subsection_id
                            AND t.topic_id = a.topic_id
                        )
                   )

                WHERE a.assignment_id = @assignmentId
                  AND a.setter_user_id = @setterUserId
                  AND a.is_active = TRUE

                GROUP BY
                    a.assignment_id,
                    a.multiplication_factor;
            ";
        }


        /* =========================================================
           SAVE QUESTION
           ========================================================= */

        public async Task<SaveQuestionResult> SaveQuestionAsync(long assignmentId,long setterUserId, CreateQuestionViewModel question)
        {
            var result =new SaveQuestionResult();

            using (var connection =new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =await connection.BeginTransactionAsync())
                {
                    try
                    {
                        var assignment = await GetAssignmentInternalAsync(connection,transaction, assignmentId, setterUserId);

                        if (assignment == null)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Assignment not found or not assigned to this Setter.";
                            return result;
                        }

                        if (!IsValidContent(question.QuestionText))
                        {
                            result.Success = false;
                            result.ErrorMessage = "Please enter the question.";
                            return result;
                        }

                        var questionType = NormalizeQuestionType(question.QuestionType);
                        question.QuestionType = questionType;
                        if (questionType != "OBJ" && questionType != "SUBJECTIVE")
                        {
                            result.Success = false;
                            result.ErrorMessage = "Please select a valid question type (MCQ or SUBJECTIVE).";
                            return result;
                        }

                        // Options and correct answer are mandatory only for MCQs.
                        if (questionType == "OBJ")
                        {
                            if (!IsValidContent(question.Option1))
                            {
                                result.Success = false;
                                result.ErrorMessage = "Please enter Option 1.";
                                return result;
                            }
                            if (!IsValidContent(question.Option2))
                            {
                                result.Success = false;
                                result.ErrorMessage = "Please enter Option 2.";
                                return result;
                            }
                            if (!IsValidContent(question.Option3))
                            {
                                result.Success = false;
                                result.ErrorMessage = "Please enter Option 3.";
                                return result;
                            }
                            if (!IsValidContent(question.Option4))
                            {
                                result.Success = false;
                                result.ErrorMessage = "Please enter Option 4.";
                                return result;
                            }
                            if (!IsValidContent(question.CorrectAnswer))
                            {
                                result.Success = false;
                                result.ErrorMessage = "Please enter the correct answer.";
                                return result;
                            }
                        }
                        else
                        {
                            question.Option1 = question.Option1 ?? string.Empty;
                            question.Option2 = question.Option2 ?? string.Empty;
                            question.Option3 = question.Option3 ?? string.Empty;
                            question.Option4 = question.Option4 ?? string.Empty;
                            question.CorrectAnswer = question.CorrectAnswer ?? string.Empty;
                        }

                        var difficulty = (question.DifficultyLevel ?? "").Trim().ToUpperInvariant();
                            if (difficulty != "EASY" &&difficulty != "MEDIUM" && difficulty != "HARD")
                            {
                                   result.Success = false;
                                   result.ErrorMessage = "Please select a valid difficulty.";
                                   return result;
                            }

                        var topicValid =await IsTopicValidAsync(connection,transaction,assignment,question.TopicId);

                        if (!topicValid)
                        {
                            result.Success = false;
                            result.ErrorMessage = "Selected Topic does not belong to this assignment.";
                            return result;
                        }


                        long questionId =question.QuestionId;
                         string previousDifficulty = null;
                        if (questionId > 0)
                        {
                            previousDifficulty =await GetExistingDifficultyAsync(connection,transaction, questionId,assignmentId,setterUserId);

                            if (previousDifficulty == null)
                            {
                                result.Success = false;
                                result.ErrorMessage = "Question was not found.";
                                return result;
                            }

                            var existingStatus = await GetExistingStatusAsync( connection,transaction,questionId,assignmentId,setterUserId);

                            if (!string.Equals( existingStatus,"DRAFT", StringComparison.OrdinalIgnoreCase))
                            {
                                result.Success = false;
                                result.ErrorMessage = "Only DRAFT questions can be saved here.";
                                return result;
                            }
                        }


                        /* =================================================
                           GET ASSIGNED QUOTA
                           ================================================= */

                        var assignedQuota = await GetAssignedQuotaInternalAsync( connection, transaction,assignmentId,setterUserId);

                        var usedQuota = await GetUsedQuotaAsync(connection,transaction,assignmentId,setterUserId, questionId);
                        int selectedRemaining;
                        if (difficulty == "EASY")
                        {
                            selectedRemaining = assignedQuota.EasyAssigned - usedQuota.easy;
                        }
                        else if (difficulty == "MEDIUM")
                        {
                            selectedRemaining = assignedQuota.MediumAssigned - usedQuota.medium;
                        }
                        else
                        {
                            selectedRemaining =assignedQuota.HardAssigned - usedQuota.hard;
                        }


                        /* =================================================
                           QUOTA CHECK

                           Existing question is excluded above,
                           so changing difficulty is handled correctly.
                           ================================================= */

                        if (selectedRemaining <= 0)
                        {
                            result.Success = false;
                            result.ErrorMessage ="Question quota for " + difficulty + " is already completed.";

                            result.Quota = BuildQuota( assignedQuota.EasyAssigned, assignedQuota.MediumAssigned, assignedQuota.HardAssigned,usedQuota.easy,usedQuota.medium, usedQuota.hard);
                            return result;
                        }


                        /* =================================================
                           INSERT
                           ================================================= */

                        if (questionId == 0)
                        {
                            const string insertSql = @"
                                INSERT INTO question
                                (
                                    assignment_id,
                                    main_exam_id,
                                    paper_id,
                                    section_id,
                                    subsection_id,
                                    topic_id,

                                    question_text,

                                    option1,
                                    option2,
                                    option3,
                                    option4,
                                    correct_answer,

                                    question_type,
                                    difficulty_level,
                                    created_by,
                                    status,
                                    created_at
                                )
                                VALUES
                                (
                                    @assignmentId,
                                    @mainExamId,
                                    @paperId,
                                    @sectionId,
                                    @subsectionId,
                                    @topicId,

                                    @questionText,

                                    @option1,
                                    @option2,
                                    @option3,
                                    @option4,
                                    @correctAnswer,

                                    @questionType,
                                    @difficulty,
                                    @createdBy,
                                    'DRAFT',
                                    CURRENT_TIMESTAMP
                                )
                                RETURNING question_id;
                            ";

                            using (var command =new NpgsqlCommand(insertSql,connection,transaction))
                            {
                                AddQuestionParameters(command, assignmentId,setterUserId,question,difficulty);

                                var value =await command.ExecuteScalarAsync();

                                questionId =Convert.ToInt64(value);
                            }
                        }

                        /* =================================================
                           UPDATE EXISTING DRAFT
                           ================================================= */

                        else
                        {
                            const string updateSql = @"
                                UPDATE question
                                SET
                                    main_exam_id = @mainExamId,
                                    paper_id = @paperId,
                                    section_id = @sectionId,
                                    subsection_id = @subsectionId,
                                    topic_id = @topicId,

                                    question_text = @questionText,

                                    option1 = @option1,
                                    option2 = @option2,
                                    option3 = @option3,
                                    option4 = @option4,
                                    correct_answer = @correctAnswer,

                                    question_type = @questionType,
                                    difficulty_level = @difficulty,
                                    updated_at = CURRENT_TIMESTAMP

                                WHERE question_id = @questionId
                                  AND assignment_id = @assignmentId
                                  AND created_by = @createdBy
                                  AND status = 'DRAFT';
                            ";

                            using (var command =new NpgsqlCommand( updateSql,connection,transaction))
                            {
                                AddQuestionParameters(command, assignmentId,setterUserId,question,difficulty);
                                command.Parameters.AddWithValue( "@questionId",questionId);
                                await command.ExecuteNonQueryAsync();
                            }
                        }


                        /* =================================================
                           UPDATE PROGRESS
                           ================================================= */

                        const string progressSql = @"
                            INSERT INTO question_creation_progress
                            (
                                assignment_id,
                                setter_user_id,
                                current_question_id,
                                last_updated_at
                            )
                            VALUES
                            (
                                @assignmentId,
                                @setterUserId,
                                @questionId,
                                CURRENT_TIMESTAMP
                            )
                            ON CONFLICT
                            (
                                assignment_id,
                                setter_user_id
                            )
                            DO UPDATE SET
                                current_question_id =
                                    EXCLUDED.current_question_id,
                                last_updated_at =
                                    CURRENT_TIMESTAMP;
                        ";

                        using (var command =new NpgsqlCommand(progressSql,connection,transaction))
                        {
                            command.Parameters.AddWithValue( "@assignmentId",assignmentId);
                            command.Parameters.AddWithValue("@setterUserId",setterUserId);
                            command.Parameters.AddWithValue("@questionId",questionId);
                            await command.ExecuteNonQueryAsync();
                        }


                        /* =================================================
                           FINAL QUOTA AFTER SAVE
                           ================================================= */

                        var usedAfter =await GetUsedQuotaAsync(connection, transaction,assignmentId,setterUserId,0);
                        result.Quota =BuildQuota(assignedQuota.EasyAssigned,assignedQuota.MediumAssigned, assignedQuota.HardAssigned,usedAfter.easy,usedAfter.medium, usedAfter.hard);
                        result.Success = true;
                        result.QuestionId = questionId;
                        await transaction.CommitAsync();
                        return result;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }


        /* =========================================================
           INTERNAL HELPERS
           ========================================================= */

        private async Task<SetterAssignmentInfo>
            GetAssignmentInternalAsync( NpgsqlConnection connection,NpgsqlTransaction transaction,long assignmentId,long setterUserId)
        {
            const string sql = @"
                SELECT
                    assignment_id,
                    assignment_level,
                    main_exam_id,
                    paper_id,
                    section_id,
                    subsection_id,
                    topic_id,
                    multiplication_factor
                FROM question_assignment
                WHERE assignment_id = @assignmentId
                  AND setter_user_id = @setterUserId
                  AND is_active = TRUE
                LIMIT 1;
            ";

            using (var command =new NpgsqlCommand(sql,connection,transaction))
            {
                command.Parameters.AddWithValue( "@assignmentId",assignmentId);
                command.Parameters.AddWithValue( "@setterUserId", setterUserId);
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return null;
                    }

                    return new SetterAssignmentInfo
                    {
                        AssignmentId = reader.GetInt64(0),
                        AssignmentLevel =reader.GetString(1),
                        MainExamId = reader.GetInt64(2),
                        PaperId =  reader.IsDBNull(3) ? (long?)null : reader.GetInt64(3),
                        SectionId = reader.IsDBNull(4)? (long?)null: reader.GetInt64(4),
                        SubsectionId =reader.IsDBNull(5) ? (long?)null: reader.GetInt64(5),
                        TopicId = reader.IsDBNull(6) ? (long?)null : reader.GetInt64(6),
                        MultiplicationFactor = reader.GetDecimal(7)
                    };
                }
            }
        }


        private async Task<bool> IsTopicValidAsync(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            SetterAssignmentInfo assignment,
            long topicId)
        {
            const string sql = @"
                SELECT EXISTS
                (
                    SELECT 1
                    FROM tos t
                    WHERE t.topic_id = @topicId
                      AND t.main_exam_id = @mainExamId
                      AND t.is_active = 1

                      AND
                      (
                            @assignmentLevel = 'EXAM'

                            OR
                            (
                                @assignmentLevel = 'PAPER'
                                AND t.paper_id = @paperId
                            )

                            OR
                            (
                                @assignmentLevel = 'SECTION'
                                AND t.paper_id = @paperId
                                AND t.section_id = @sectionId
                            )

                            OR
                            (
                                @assignmentLevel = 'SUBSECTION'
                                AND t.paper_id = @paperId
                                AND t.section_id = @sectionId
                                AND t.subsection_id = @subsectionId
                            )

                            OR
                            (
                                @assignmentLevel = 'TOPIC'
                                AND t.paper_id = @paperId
                                AND t.section_id = @sectionId
                                AND t.subsection_id IS NOT DISTINCT FROM @subsectionId
                                AND t.topic_id = @assignedTopicId
                            )
                      )
                );
            ";

            using (var command =new NpgsqlCommand( sql,connection,transaction))
            {
                command.Parameters.AddWithValue("@topicId",topicId);
                command.Parameters.AddWithValue("@mainExamId", assignment.MainExamId);
                command.Parameters.AddWithValue("@assignmentLevel",assignment.AssignmentLevel);
                command.Parameters.AddWithValue("@paperId", (object)assignment.PaperId ?? DBNull.Value);
                command.Parameters.AddWithValue("@sectionId", (object)assignment.SectionId?? DBNull.Value);
                command.Parameters.AddWithValue("@subsectionId",(object)assignment.SubsectionId?? DBNull.Value);
                command.Parameters.AddWithValue("@assignedTopicId",(object)assignment.TopicId ?? DBNull.Value);
                return Convert.ToBoolean(await command.ExecuteScalarAsync());
            }
        }


        private async Task<QuestionQuotaViewModel>
            GetAssignedQuotaInternalAsync( NpgsqlConnection connection,NpgsqlTransaction transaction,long assignmentId,long setterUserId)
        {
            using (var command = new NpgsqlCommand(GetQuotaSql(),connection,transaction))
            {
                command.Parameters.AddWithValue("@assignmentId",assignmentId);

                command.Parameters.AddWithValue("@setterUserId",setterUserId);

                using (var reader =await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return new QuestionQuotaViewModel();
                    }

                    return new QuestionQuotaViewModel
                    {
                        EasyAssigned =ToQuestionCount(reader.GetDecimal(0)),

                        MediumAssigned =ToQuestionCount(reader.GetDecimal(1)),

                        HardAssigned =ToQuestionCount(reader.GetDecimal(2))
                    };
                }
            }
        }


        private async Task<(int easy, int medium, int hard)>
            GetUsedQuotaAsync( NpgsqlConnection connection,NpgsqlTransaction transaction,long assignmentId,long setterUserId,long excludeQuestionId)
            {
            const string sql = @"
                SELECT
                    COUNT(*) FILTER
                    (
                        WHERE difficulty_level = 'EASY'
                    ),

                    COUNT(*) FILTER
                    (
                        WHERE difficulty_level = 'MEDIUM'
                    ),

                    COUNT(*) FILTER
                    (
                        WHERE difficulty_level = 'HARD'
                    )

                FROM question

                WHERE assignment_id = @assignmentId
                  AND created_by = @setterUserId

                  AND
                  (
                      @excludeQuestionId = 0
                      OR question_id <> @excludeQuestionId
                  );
            ";

            bool ownsConnection = connection == null;

            if (ownsConnection)
            {
                connection =new NpgsqlConnection(  _connectionString);
                await connection.OpenAsync();
            }

            try
            {
                using (var command = new NpgsqlCommand(sql,connection, transaction))
                {
                    command.Parameters.AddWithValue( "@assignmentId",assignmentId);
                    command.Parameters.AddWithValue("@setterUserId", setterUserId);
                    command.Parameters.AddWithValue("@excludeQuestionId", excludeQuestionId);
                    using (var reader =await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return (0, 0, 0);
                        }

                        return
                        (
                            Convert.ToInt32( reader.GetInt64(0)),
                            Convert.ToInt32( reader.GetInt64(1)),
                            Convert.ToInt32( reader.GetInt64(2))
                        );
                    }
                }
            }
            finally
            {
                if (ownsConnection)
                {
                    connection.Dispose();
                }
            }
        }


        private async Task<string>
            GetExistingDifficultyAsync(NpgsqlConnection connection,NpgsqlTransaction transaction,long questionId, long assignmentId,long setterUserId)
        {
            const string sql = @"
                SELECT difficulty_level
                FROM question
                WHERE question_id = @questionId
                  AND assignment_id = @assignmentId
                  AND created_by = @setterUserId
                LIMIT 1;
            ";

            using (var command =new NpgsqlCommand( sql, connection,transaction))
            {
                command.Parameters.AddWithValue("@questionId",questionId);
                command.Parameters.AddWithValue( "@assignmentId", assignmentId);
                command.Parameters.AddWithValue("@setterUserId", setterUserId);
                var value = await command.ExecuteScalarAsync();
                return value == null ||value == DBNull.Value ? null: Convert.ToString(value);
            }
        }


        private async Task<string> GetExistingStatusAsync( NpgsqlConnection connection, NpgsqlTransaction transaction, long questionId, long assignmentId,long setterUserId)
        {
            const string sql = @"
                SELECT status
                FROM question
                WHERE question_id = @questionId
                  AND assignment_id = @assignmentId
                  AND created_by = @setterUserId
                LIMIT 1;
            ";

            using (var command = new NpgsqlCommand(sql,connection, transaction))
            {
                command.Parameters.AddWithValue( "@questionId",questionId);
                command.Parameters.AddWithValue( "@assignmentId",assignmentId);
                command.Parameters.AddWithValue( "@setterUserId",setterUserId);
                var value = await command.ExecuteScalarAsync();
                return value == null || value == DBNull.Value ? null : Convert.ToString(value);
            }
        }


        private void AddQuestionParameters(NpgsqlCommand command, long assignmentId,long setterUserId, CreateQuestionViewModel question,string difficulty)
        {
             command.Parameters.AddWithValue("@assignmentId", assignmentId);
             command.Parameters.AddWithValue("@mainExamId", question.MainExamId);
             command.Parameters.AddWithValue( "@paperId", question.PaperId);
             command.Parameters.AddWithValue( "@sectionId", question.SectionId);
             command.Parameters.AddWithValue( "@subsectionId",(object)question.SubsectionId?? DBNull.Value);
             command.Parameters.AddWithValue("@topicId",question.TopicId);
             command.Parameters.AddWithValue( "@questionText", EncryptionEnhanced.Encrypt(question.QuestionText ?? ""));
             command.Parameters.AddWithValue("@option1", EncryptionEnhanced.Encrypt(question.Option1 ?? ""));
             command.Parameters.AddWithValue( "@option2", EncryptionEnhanced.Encrypt(question.Option2 ?? ""));
             command.Parameters.AddWithValue( "@option3", EncryptionEnhanced.Encrypt(question.Option3 ?? ""));
             command.Parameters.AddWithValue( "@option4", EncryptionEnhanced.Encrypt(question.Option4 ?? ""));
             command.Parameters.AddWithValue("@correctAnswer", EncryptionEnhanced.Encrypt(question.CorrectAnswer ?? ""));
             command.Parameters.AddWithValue( "@questionType", question.QuestionType ?? "");
             command.Parameters.AddWithValue( "@difficulty",difficulty);
             command.Parameters.AddWithValue( "@createdBy",setterUserId);
        }


        private QuestionQuotaViewModel BuildQuota(int easyAssigned, int mediumAssigned,int hardAssigned, int easyUsed,int mediumUsed,int hardUsed)
        {
            return new QuestionQuotaViewModel
            {
                EasyAssigned = easyAssigned,

                MediumAssigned = mediumAssigned,

                HardAssigned = hardAssigned,

                EasyUsed = easyUsed,

                MediumUsed =mediumUsed,

                HardUsed =hardUsed,

                Easyremaining = Math.Max( 0,easyAssigned - easyUsed),
                Mediumremaining =Math.Max( 0,mediumAssigned - mediumUsed),
                Hardremaining = Math.Max(0,hardAssigned - hardUsed)
            };
        }


        private int ToQuestionCount(decimal value)
        {
            if (value <= 0)
            {
                return 0;
            }

            // Same rounding as the Admin quota and the Excel / Word templates (Math.Ceiling).
            return (int)Math.Ceiling(value);
        }


        // MCQ / OBJ / Objective / Multiple choice -> OBJ ; Subjective / Descriptive -> SUBJECTIVE.
        // The page used to offer "MCQ" while Save only accepted "OBJ", so Save and Next failed.
        public static string NormalizeQuestionType(string value)
        {
            var v = (value ?? "").Trim().ToUpperInvariant().Replace("_", " ").Replace("-", " ");
            switch (v)
            {
                case "OBJ":
                case "MCQ":
                case "M":
                case "O":
                case "OBJECTIVE":
                case "MULTIPLE CHOICE":
                case "MULTIPLE CHOICE QUESTION":
                    return "OBJ";
                case "SUBJECTIVE":
                case "SUB":
                case "S":
                case "D":
                case "DESCRIPTIVE":
                case "DESCRIPTIVE QUESTION":
                    return "SUBJECTIVE";
                default:
                    return v;
            }
        }

        private bool IsValidContent(
            string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
            var cleaned = value.Replace("&nbsp;", "") .Replace("<br>", "") .Replace("<br/>", "") .Replace("<br />", "") .Trim();
            return cleaned.Length > 0;
        }
        
        public async Task<List<TopicQuotaInfo>> GetTopicQuotaInfoAsync(
        long assignmentId,
        long setterUserId)
        {
            const string sql = @"
        SELECT
            t.topic_id,
            t.main_exam_id,
            t.paper_id,
            t.section_id,
            t.subsection_id,

            t.main_exam_name,
            t.paper_name,
            t.section_name,
            t.subsection_name,
            t.topic_name,
            t.topic_type,

            t.topic_easy_question,
            t.topic_medium_question,
            t.topic_hard_question,

            COUNT(q.question_id)
                FILTER (
                    WHERE q.difficulty_level = 'EASY'
                ) AS easy_used,

            COUNT(q.question_id)
                FILTER (
                    WHERE q.difficulty_level = 'MEDIUM'
                ) AS medium_used,

            COUNT(q.question_id)
                FILTER (
                    WHERE q.difficulty_level = 'HARD'
                ) AS hard_used

        FROM question_assignment a

        INNER JOIN tos t
            ON t.main_exam_id = a.main_exam_id
           AND t.is_active = 1

           AND
           (
                a.assignment_level = 'EXAM'

                OR
                (
                    a.assignment_level = 'PAPER'
                    AND t.paper_id = a.paper_id
                )

                OR
                (
                    a.assignment_level = 'SECTION'
                    AND t.paper_id = a.paper_id
                    AND t.section_id = a.section_id
                )

                OR
                (
                    a.assignment_level = 'SUBSECTION'
                    AND t.paper_id = a.paper_id
                    AND t.section_id = a.section_id
                    AND t.subsection_id = a.subsection_id
                )

                OR
                (
                    a.assignment_level = 'TOPIC'
                    AND t.paper_id = a.paper_id
                    AND t.section_id = a.section_id
                    AND t.subsection_id
                        IS NOT DISTINCT FROM a.subsection_id
                    AND t.topic_id = a.topic_id
                )
           )

        LEFT JOIN question q
            ON q.assignment_id = a.assignment_id
           AND q.created_by = a.setter_user_id
           AND q.topic_id = t.topic_id

        WHERE a.assignment_id = @assignmentId
          AND a.setter_user_id = @setterUserId
          AND a.is_active = TRUE

        GROUP BY
            t.topic_id,
            t.main_exam_id,
            t.paper_id,
            t.section_id,
            t.subsection_id,
            t.main_exam_name,
            t.paper_name,
            t.section_name,
            t.subsection_name,
            t.topic_name,
            t.topic_type,
            t.topic_easy_question,
            t.topic_medium_question,
            t.topic_hard_question

        ORDER BY
            t.main_exam_id,
            t.paper_id,
            t.section_id,
            t.subsection_id NULLS FIRST,
            t.topic_id;
    ";


            var result =
                new List<TopicQuotaInfo>();


            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@assignmentId",
                        assignmentId);

                    command.Parameters.AddWithValue(
                        "@setterUserId",
                        setterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new TopicQuotaInfo
                                {
                                    TopicId =
                                        reader.GetInt64(0),

                                    MainExamId =
                                        reader.GetInt64(1),

                                    PaperId =
                                        reader.GetInt64(2),

                                    SectionId =
                                        reader.GetInt64(3),

                                    SubsectionId =
                                        reader.IsDBNull(4)
                                        ? (long?)null
                                        : reader.GetInt64(4),

                                    ExamName =
                                        reader.IsDBNull(5)
                                        ? null
                                        : reader.GetString(5),

                                    PaperName =
                                        reader.IsDBNull(6)
                                        ? null
                                        : reader.GetString(6),

                                    SectionName =
                                        reader.IsDBNull(7)
                                        ? null
                                        : reader.GetString(7),

                                    SubsectionName =
                                        reader.IsDBNull(8)
                                        ? null
                                        : reader.GetString(8),

                                    TopicName =
                                        reader.IsDBNull(9)
                                        ? null
                                        : reader.GetString(9),

                                    QuestionType =
                                        reader.IsDBNull(10)
                                        ? "MCQ"
                                        : reader.GetString(10),

                                    EasyQuota =
                                        reader.GetInt32(11),

                                    MediumQuota =
                                        reader.GetInt32(12),

                                    HardQuota =
                                        reader.GetInt32(13),

                                    EasyUsed =
                                        Convert.ToInt32(
                                            reader.GetInt64(14)),

                                    MediumUsed =
                                        Convert.ToInt32(
                                            reader.GetInt64(15)),

                                    HardUsed =
                                        Convert.ToInt32(
                                            reader.GetInt64(16))
                                });
                        }
                    }
                }
            }

            return result;
        }

        public async Task<QuestionCreationSlot> GetNextCreationSlotAsync(
        long assignmentId,
        long setterUserId)
        {
            var topics =
                await GetTopicQuotaInfoAsync(
                    assignmentId,
                    setterUserId);

            if (topics == null ||
                topics.Count == 0)
            {
                return null;
            }

            var orderedTopics = topics
                .OrderBy(x => x.MainExamId)
                .ThenBy(x => x.PaperId)
                .ThenBy(x => x.SectionId)
                .ThenBy(x => x.SubsectionId)
                .ThenBy(x => x.TopicId)
                .ToList();

            int sequence = await GetNextCreationSequenceAsync(assignmentId,setterUserId);

            foreach (var topic in orderedTopics)
            {
                /* ===============================================
                   TOPIC EASY
                   =============================================== */

                if (topic.EasyUsed < topic.EasyQuota)
                {
                    return new QuestionCreationSlot
                    {
                        MainExamId =
                            topic.MainExamId,

                        PaperId =
                            topic.PaperId,

                        SectionId =
                            topic.SectionId,

                        SubsectionId =
                            topic.SubsectionId,

                        TopicId =
                            topic.TopicId,

                        ExamName =
                            topic.ExamName,

                        PaperName =
                            topic.PaperName,

                        SectionName =
                            topic.SectionName,

                        SubsectionName =
                            topic.SubsectionName,

                        TopicName =
                            topic.TopicName,

                        QuestionType =
                            topic.QuestionType,

                        DifficultyLevel =
                            "EASY",
                        CreationSequence = sequence
                    };
                }


                /* ===============================================
                   TOPIC MEDIUM
                   =============================================== */

                if (topic.MediumUsed < topic.MediumQuota)
                {
                    return new QuestionCreationSlot
                    {
                        MainExamId =
                            topic.MainExamId,

                        PaperId =
                            topic.PaperId,

                        SectionId =
                            topic.SectionId,

                        SubsectionId =
                            topic.SubsectionId,

                        TopicId =
                            topic.TopicId,

                        ExamName =
                            topic.ExamName,

                        PaperName =
                            topic.PaperName,

                        SectionName =
                            topic.SectionName,

                        SubsectionName =
                            topic.SubsectionName,

                        TopicName =
                            topic.TopicName,

                        QuestionType =
                            topic.QuestionType,

                        DifficultyLevel =
                            "MEDIUM",
                        CreationSequence = sequence
                    };
                }


                /* ===============================================
                   TOPIC HARD
                   =============================================== */

                if (topic.HardUsed < topic.HardQuota)
                {
                    return new QuestionCreationSlot
                    {
                        MainExamId =
                            topic.MainExamId,

                        PaperId =
                            topic.PaperId,

                        SectionId =
                            topic.SectionId,

                        SubsectionId =
                            topic.SubsectionId,

                        TopicId =
                            topic.TopicId,

                        ExamName =
                            topic.ExamName,

                        PaperName =
                            topic.PaperName,

                        SectionName =
                            topic.SectionName,

                        SubsectionName =
                            topic.SubsectionName,

                        TopicName =
                            topic.TopicName,

                        QuestionType =
                            topic.QuestionType,

                        DifficultyLevel =
                            "HARD",
                        CreationSequence = sequence
                    };
                }
            }


            return null;
        }

        public async Task<int> GetNextCreationSequenceAsync(long assignmentId,long setterUserId)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                const string sql = @"Select COALESCE(MAX(creation_sequence),0) + 1 from question where assignment_id=@AssignmentId and created_by=@SetterUserId;";
                using (var command=new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@AssignmentId",assignmentId);
                    command.Parameters.AddWithValue("@SetterUserId",setterUserId);
                    var result = await command.ExecuteScalarAsync();
                    return Convert.ToInt32(result);
                }
            }
        }
        public async Task<CreateQuestionViewModel> GetQuestionBySequenceAsync(long assignmentId, long setterUserId,int sequence)
        {
            using (var connection = new NpgsqlConnection(_connectionString))
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

                COALESCE(t.exam_name, '') AS exam_name,
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
                q.latest_remarks,
                q.creation_sequence

            FROM question q

            LEFT JOIN tos t
                ON t.topic_id = q.topic_id
               AND t.main_exam_id = q.main_exam_id
               AND t.paper_id = q.paper_id
               AND t.section_id = q.section_id

            WHERE q.assignment_id = @AssignmentId
              AND q.created_by = @SetterUserId
              AND q.creation_sequence = @Sequence

            LIMIT 1;
        ";

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "@AssignmentId",
                        assignmentId);

                    command.Parameters.AddWithValue(
                        "@SetterUserId",
                        setterUserId);

                    command.Parameters.AddWithValue(
                        "@Sequence",
                        sequence);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                            return null;

                        return new CreateQuestionViewModel
                        {
                            QuestionId = reader.GetInt64(
                                reader.GetOrdinal("question_id")),

                            AssignmentID = reader.IsDBNull(
                                reader.GetOrdinal("assignment_id"))
                                ? assignmentId
                                : reader.GetInt64(
                                    reader.GetOrdinal("assignment_id")),

                            MainExamId = reader.GetInt64(
                                reader.GetOrdinal("main_exam_id")),

                            PaperId = reader.GetInt64(
                                reader.GetOrdinal("paper_id")),

                            SectionId = reader.GetInt64(
                                reader.GetOrdinal("section_id")),

                            SubsectionId = reader.IsDBNull(
                                reader.GetOrdinal("subsection_id"))
                                ? (long?)null
                                : reader.GetInt64(
                                    reader.GetOrdinal("subsection_id")),

                            TopicId = reader.GetInt64(
                                reader.GetOrdinal("topic_id")),

                            ExamName = reader.GetString(
                                reader.GetOrdinal("exam_name")),

                            PaperName = reader.GetString(
                                reader.GetOrdinal("paper_name")),

                            SectionName = reader.GetString(
                                reader.GetOrdinal("section_name")),

                            SubsectionName = reader.GetString(
                                reader.GetOrdinal("subsection_name")),

                            TopicName = reader.GetString(
                                reader.GetOrdinal("topic_name")),

                            QuestionType = reader.GetString(
                                reader.GetOrdinal("question_type")),

                            DifficultyLevel = reader.GetString(
                                reader.GetOrdinal("difficulty_level")),

                            QuestionText = reader.GetString(
                                reader.GetOrdinal("question_text")),

                            Option1 = reader.IsDBNull(
                                reader.GetOrdinal("option1"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("option1")),

                            Option2 = reader.IsDBNull(
                                reader.GetOrdinal("option2"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("option2")),

                            Option3 = reader.IsDBNull(
                                reader.GetOrdinal("option3"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("option3")),

                            Option4 = reader.IsDBNull(
                                reader.GetOrdinal("option4"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("option4")),

                            CorrectAnswer = reader.IsDBNull(
                                reader.GetOrdinal("correct_answer"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("correct_answer")),

                            Status = reader.IsDBNull(
                                reader.GetOrdinal("status"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("status")),

                            LatestRemarks = reader.IsDBNull(
                                reader.GetOrdinal("latest_remarks"))
                                ? null
                                : reader.GetString(
                                    reader.GetOrdinal("latest_remarks")),

                            CreationSequence = reader.IsDBNull(
                                reader.GetOrdinal("creation_sequence"))
                                ? 0
                                : reader.GetInt32(
                                    reader.GetOrdinal("creation_sequence"))
                        };
                    }
                }
            }


        }

    }
}
