using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;


namespace QBMS.Infrastructure.Services
{
    public class SetterRepository : ISetterRepository
    {
        private readonly string _connectionString;

        public SetterRepository(
            string connectionString)
        {
            _connectionString = connectionString;
        }

        // =========================================================
        // GET ASSIGNMENTS FOR SETTER
        // =========================================================

        public async Task<List<SetterAssignmentViewModel>>
            GetAssignmentsAsync(long setterUserId)
        {
            var result =
                new List<SetterAssignmentViewModel>();

            const string sql = @"
SELECT
    qa.assignment_id,
    qa.assignment_level,

    qa.main_exam_id,
    qa.paper_id,
    qa.section_id,
    qa.subsection_id,
    qa.topic_id,

    t.main_exam_name,
    t.paper_name,
    t.section_name,
    t.subsection_name,
    t.topic_name,

    qa.multiplication_factor,
    qa.is_active

FROM question_assignment qa

LEFT JOIN LATERAL
(
    SELECT
        main_exam_name,
        paper_name,
        section_name,
        subsection_name,
        topic_name

    FROM tos t

    WHERE t.main_exam_id = qa.main_exam_id

      AND
      (
          qa.assignment_level = 'EXAM'

          OR
          (
              qa.assignment_level = 'PAPER'
              AND t.paper_id = qa.paper_id
          )

          OR
          (
              qa.assignment_level = 'SECTION'
              AND t.paper_id = qa.paper_id
              AND t.section_id = qa.section_id
          )

          OR
          (
              qa.assignment_level = 'SUBSECTION'
              AND t.paper_id = qa.paper_id
              AND t.section_id = qa.section_id
              AND t.subsection_id = qa.subsection_id
          )

          OR
          (
              qa.assignment_level = 'TOPIC'
              AND t.paper_id = qa.paper_id
              AND t.section_id = qa.section_id
              AND t.subsection_id = qa.subsection_id
              AND t.topic_id = qa.topic_id
          )
      )

    ORDER BY
        t.paper_name,
        t.section_name,
        t.subsection_name,
        t.topic_name

    LIMIT 1

) t ON TRUE

WHERE qa.setter_user_id = @setter_user_id
  AND qa.is_active = TRUE

ORDER BY
    t.main_exam_name,
    t.paper_name,
    t.section_name,
    t.subsection_name,
    t.topic_name;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "setter_user_id",
                        setterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new SetterAssignmentViewModel
                                {
                                    AssignmentId =
                                        Convert.ToInt64(
                                            reader["assignment_id"]),

                                    AssignmentLevel =
                                        reader["assignment_level"]
                                            .ToString(),

                                    MainExamId =
                                        Convert.ToInt64(
                                            reader["main_exam_id"]),

                                    PaperId =
                                        reader["paper_id"]
                                            == DBNull.Value
                                            ? (long?)null
                                            : Convert.ToInt64(
                                                reader["paper_id"]),

                                    SectionId =
                                        reader["section_id"]
                                            == DBNull.Value
                                            ? (long?)null
                                            : Convert.ToInt64(
                                                reader["section_id"]),

                                    SubsectionId =
                                        reader["subsection_id"]
                                            == DBNull.Value
                                            ? (long?)null
                                            : Convert.ToInt64(
                                                reader["subsection_id"]),

                                    TopicId =
                                        reader["topic_id"]
                                            == DBNull.Value
                                            ? (long?)null
                                            : Convert.ToInt64(
                                                reader["topic_id"]),

                                    ExamName =
                                        reader["main_exam_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["main_exam_name"]
                                                .ToString(),

                                    PaperName =
                                        reader["paper_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["paper_name"]
                                                .ToString(),

                                    SectionName =
                                        reader["section_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["section_name"]
                                                .ToString(),

                                    SubsectionName =
                                        reader["subsection_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["subsection_name"]
                                                .ToString(),

                                    TopicName =
                                        reader["topic_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["topic_name"]
                                                .ToString(),

                                    MultiplicationFactor =
                                        Convert.ToDecimal(
                                            reader[
                                                "multiplication_factor"]),

                                    IsActive =
                                        Convert.ToBoolean(
                                            reader["is_active"])
                                });
                        }
                    }
                }
            }

            return result;
        }


        // =========================================================
        // GET TOPICS AVAILABLE UNDER ASSIGNMENT
        // =========================================================

        public async Task<List<CreateQuestionViewModel>>
            GetTopicsAsync(
                long assignmentId,
                long setterUserId)
        {
            var result =
                new List<CreateQuestionViewModel>();

            const string sql = @"
SELECT DISTINCT

    t.main_exam_id,
    t.paper_id,
    t.section_id,
    t.subsection_id,
    t.topic_id,

    t.main_exam_name,
    t.paper_name,
    t.section_name,
    t.subsection_name,
    t.topic_name

FROM question_assignment qa

INNER JOIN tos t
    ON t.main_exam_id = qa.main_exam_id

WHERE qa.assignment_id = @assignment_id

  AND qa.setter_user_id = @setter_user_id

  AND qa.is_active = TRUE

  AND t.is_active = 1

  AND
  (
      qa.assignment_level = 'EXAM'

      OR
      (
          qa.assignment_level = 'PAPER'
          AND t.paper_id = qa.paper_id
      )

      OR
      (
          qa.assignment_level = 'SECTION'
          AND t.paper_id = qa.paper_id
          AND t.section_id = qa.section_id
      )

      OR
      (
          qa.assignment_level = 'SUBSECTION'
          AND t.paper_id = qa.paper_id
          AND t.section_id = qa.section_id
          AND t.subsection_id = qa.subsection_id
      )

      OR
      (
          qa.assignment_level = 'TOPIC'
          AND t.paper_id = qa.paper_id
          AND t.section_id = qa.section_id
          AND t.subsection_id = qa.subsection_id
          AND t.topic_id = qa.topic_id
      )
  )

ORDER BY
    t.paper_name,
    t.section_name,
    t.subsection_name,
    t.topic_name;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
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
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new CreateQuestionViewModel
                                {
                                    MainExamId =
                                        Convert.ToInt64(
                                            reader[
                                                "main_exam_id"]),

                                    PaperId =
                                        Convert.ToInt64(
                                            reader["paper_id"]),

                                    SectionId =
                                        Convert.ToInt64(
                                            reader[
                                                "section_id"]),

                                    SubsectionId =
                                        reader[
                                            "subsection_id"]
                                            == DBNull.Value
                                            ? (long?)null
                                            : Convert.ToInt64(
                                                reader[
                                                    "subsection_id"]),

                                    TopicId =
                                        Convert.ToInt64(
                                            reader[
                                                "topic_id"]),

                                    ExamName =
                                        reader["main_exam_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader[
                                                "main_exam_name"]
                                                .ToString(),

                                    PaperName =
                                        reader["paper_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader[
                                                "paper_name"]
                                                .ToString(),

                                    SectionName =
                                        reader["section_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader[
                                                "section_name"]
                                                .ToString(),

                                    SubsectionName =
                                        reader[
                                            "subsection_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader[
                                                "subsection_name"]
                                                .ToString(),

                                    TopicName =
                                        reader["topic_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader[
                                                "topic_name"]
                                                .ToString()
                                });
                        }
                    }
                }
            }

            return result;
        }


        // =========================================================
        // CREATE QUESTION
        // =========================================================

        public async Task<long> CreateQuestionAsync(
            CreateQuestionViewModel model,
            long setterUserId)
        {
            const string sql = @"
INSERT INTO question
(
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
    @main_exam_id,
    @paper_id,
    @section_id,
    @subsection_id,
    @topic_id,

    @question_text,
    @option1,
    @option2,
    @option3,
    @option4,
    @correct_answer,
    @question_type,
    @difficulty_level,

    @created_by,
    'DRAFT',
    CURRENT_TIMESTAMP
)
RETURNING question_id;
";
            var encryptedQuestion = EncryptionEnhanced.Encrypt(model.QuestionText);

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "main_exam_id",
                        model.MainExamId);

                    command.Parameters.AddWithValue(
                        "paper_id",
                        model.PaperId);

                    command.Parameters.AddWithValue(
                        "section_id",
                        model.SectionId);

                    command.Parameters.AddWithValue(
                        "subsection_id",
                        (object)model.SubsectionId
                        ?? DBNull.Value);

                    command.Parameters.AddWithValue(
                        "topic_id",
                        model.TopicId);

                    command.Parameters.AddWithValue(
                        "question_text",
                        encryptedQuestion);

                    //command.Parameters.AddWithValue(
                    //    "question_type",
                    //    model.QuestionType);

                    var questionTypeParam = new NpgsqlParameter("question_type", NpgsqlTypes.NpgsqlDbType.Text)
                    {
                        Value = model.QuestionType ?? "SUBJECTIVE"
                    };
                    command.Parameters.Add(questionTypeParam);
                        

                    command.Parameters.AddWithValue(
                        "difficulty_level",
                        model.DifficultyLevel);

                    command.Parameters.AddWithValue(
                        "created_by",
                        setterUserId);
                    command.Parameters.AddWithValue("option1", EncryptionEnhanced.Encrypt(model.Option1));
                    command.Parameters.AddWithValue("option2", EncryptionEnhanced.Encrypt(model.Option2));
                    command.Parameters.AddWithValue("option3", EncryptionEnhanced.Encrypt(model.Option3));
                    command.Parameters.AddWithValue("option4", EncryptionEnhanced.Encrypt(model.Option4));
                    command.Parameters.AddWithValue("correct_answer", EncryptionEnhanced.Encrypt(model.CorrectAnswer));
                    var result =
                        await command.ExecuteScalarAsync();

                    return Convert.ToInt64(result);
                }
            }
        }

        public async Task<List<SetterQuestionViewModel>> GetQuestionsAsync(long setterUserId)
        {
            var result =
                new List<SetterQuestionViewModel>();

            const string sql = @"
SELECT
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

INNER JOIN tos t
    ON t.main_exam_id = q.main_exam_id
   AND t.paper_id = q.paper_id
   AND t.section_id = q.section_id
   AND t.subsection_id IS NOT DISTINCT FROM q.subsection_id
   AND t.topic_id = q.topic_id

WHERE q.created_by = @setter_user_id

ORDER BY q.created_at DESC;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue(
                        "setter_user_id",
                        setterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new SetterQuestionViewModel
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
                                        reader["section_name"].ToString(),

                                    SubsectionName =
                                        reader["subsection_name"]
                                            == DBNull.Value
                                            ? null
                                            : reader["subsection_name"]
                                                .ToString(),

                                    TopicName =
                                        reader["topic_name"].ToString(),

                                    DifficultyLevel =
                                        reader["difficulty_level"]
                                            .ToString(),

                                    QuestionText =
                                    EncryptionEnhanced.Decrypt(reader["question_text"]
                                            .ToString()),
                                    Option1=EncryptionEnhanced.Decrypt(reader["option1"].ToString()),
                                    Option2 = EncryptionEnhanced.Decrypt(reader["option2"].ToString()),
                                    Option3 = EncryptionEnhanced.Decrypt(reader["option3"].ToString()),
                                    Option4 = EncryptionEnhanced.Decrypt(reader["option4"].ToString()),
                                    CorrectAnswer= EncryptionEnhanced.Decrypt(reader["correct_answer"].ToString()),
                                    Status =
                                        reader["status"].ToString()
                                });
                        }
                    }
                }
            }

            return result;
        }
        public async Task<EditQuestionViewModel>
    GetQuestionForEditAsync(
        long questionId,
        long setterUserId)
        {
            const string sql = @"
SELECT
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
    q.question_type,
    q.question_text,
    q.option1,
    q.option2,
    q.option3,
    q.option4,
    q.correct_answer,
    q.status,
    q.latest_remarks,

    COALESCE(
        (
            SELECT MAX(qv.version_no)
            FROM question_version qv
            WHERE qv.question_id = q.question_id
        ),
        0
    ) AS current_version

FROM question q

INNER JOIN tos t
    ON t.main_exam_id = q.main_exam_id
   AND t.paper_id = q.paper_id
   AND t.section_id = q.section_id
   AND t.subsection_id IS NOT DISTINCT FROM q.subsection_id
   AND t.topic_id = q.topic_id

WHERE q.question_id = @question_id
  AND q.created_by = @setter_user_id
  AND q.status IN
  (
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

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return null;
                        }

                        return new EditQuestionViewModel
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
                                reader["section_name"].ToString(),

                            SubsectionName =
                                reader["subsection_name"] == DBNull.Value
                                    ? null
                                    : reader["subsection_name"].ToString(),

                            TopicName =
                                reader["topic_name"].ToString(),

                            DifficultyLevel =
                                reader["difficulty_level"].ToString(),

                            QuestionType =
                                reader["question_type"].ToString(),

                            QuestionText =
                               EncryptionEnhanced.Decrypt(reader["question_text"]
                                            .ToString()),
                            Option1 = EncryptionEnhanced.Decrypt(reader["option1"].ToString()),
                            Option2 = EncryptionEnhanced.Decrypt(reader["option2"].ToString()),
                            Option3 = EncryptionEnhanced.Decrypt(reader["option3"].ToString()),
                            Option4 = EncryptionEnhanced.Decrypt(reader["option4"].ToString()),
                            CorrectAnswer = EncryptionEnhanced.Decrypt(reader["correct_answer"].ToString()),

                            Status =
                                reader["status"].ToString(),

                            LatestRemarks =
                                reader["latest_remarks"] == DBNull.Value
                                    ? null
                                    : reader["latest_remarks"].ToString(),

                            CurrentVersion =
                                Convert.ToInt32(
                                    reader["current_version"])
                        };
                    }
                }
            }
        }

        public async Task<bool> UpdateQuestionAsync(
    EditQuestionViewModel model,
    long setterUserId)
        {
            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    await connection.BeginTransactionAsync())
                {
                    // Verify that this question belongs to the
                    // logged-in Setter and is actually rejected.
                    const string verifySql = @"
SELECT
    question_text,
 option1,
    option2,
    option3,
    option4,
    correct_answer,
    question_type,
    difficulty_level
FROM question
WHERE question_id = @question_id
  AND created_by = @setter_user_id
  AND status IN
  (
      'VETTER_REJECTED',
      'ADMIN_REJECTED'
  );
";
                    //==============================================================================================================================
                    string oldQuestionText = null;
                    string oldQuestionType = null;
                    string oldDifficulty = null;

                    using (var command =
                        new NpgsqlCommand(
                            verifySql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "question_id",
                            model.QuestionId);

                        command.Parameters.AddWithValue(
                            "setter_user_id",
                            setterUserId);

                        using (var reader =
                            await command.ExecuteReaderAsync())
                        {
                            if (!await reader.ReadAsync())
                            {
                                return false;
                            }

                            oldQuestionText =
                              EncryptionEnhanced.Decrypt(reader["question_text"]
                                            .ToString());

                            oldQuestionType =
                                reader["question_type"].ToString();

                            oldDifficulty =
                                reader["difficulty_level"].ToString();
                        }
                    }

                    // Get current version.
                    const string versionSql = @"
SELECT
    COALESCE(
        MAX(version_no),
        0
    )
FROM question_version
WHERE question_id = @question_id;
";

                    int nextVersion;

                    using (var command =
                        new NpgsqlCommand(
                            versionSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "question_id",
                            model.QuestionId);

                        var current =
                            Convert.ToInt32(
                                await command.ExecuteScalarAsync());

                        nextVersion = current + 1;
                    }

                    // Save the old version.
                    const string insertVersionSql = @"
INSERT INTO question_version
(
    question_id,
    version_no,
    question_text,
    question_type,
    difficulty_level,
    changed_by,
    change_reason
)
VALUES
(
    @question_id,
    @version_no,
    @question_text,
    @question_type,
    @difficulty_level,
    @changed_by,
    @change_reason
);
";
                    string encryptedQuestion = EncryptionEnhanced.Encrypt(oldQuestionText);
                                            

                    using (var command =
                        new NpgsqlCommand(
                            insertVersionSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "question_id",
                            model.QuestionId);

                        command.Parameters.AddWithValue(
                            "version_no",
                            nextVersion);

                        command.Parameters.AddWithValue(
                            "question_text",
                           encryptedQuestion);

                        //command.Parameters.AddWithValue(
                        //    "question_type",
                        //    oldQuestionType);

                        var questionTypeParam = new NpgsqlParameter("question_type", NpgsqlTypes.NpgsqlDbType.Text)
                        {
                            Value = model.QuestionType ?? "SUBJECTIVE"
                        };
                        command.Parameters.Add(questionTypeParam);

                        command.Parameters.AddWithValue(
                            "difficulty_level",
                            oldDifficulty);

                        command.Parameters.AddWithValue(
                            "changed_by",
                            setterUserId);

                        command.Parameters.AddWithValue(
                            "change_reason",
                            (object)model.LatestRemarks
                            ?? DBNull.Value);

                        await command.ExecuteNonQueryAsync();
                    }

                    // Update current question.
                    const string updateSql = @"
UPDATE question
SET
    question_text = @question_text,
    question_type = @question_type,
    difficulty_level = @difficulty_level,
    status = 'DRAFT',
    latest_remarks = NULL,
    updated_at = CURRENT_TIMESTAMP,
    option1= @option1,
    option2=@option2,
    option3=@option3,
    option4=@option4,
    correct_answer=@correct_answer

WHERE question_id = @question_id
  AND created_by = @setter_user_id;
";

                    using (var command =
                        new NpgsqlCommand(
                            updateSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "question_id",
                            model.QuestionId);

                        command.Parameters.AddWithValue(
                            "setter_user_id",
                            setterUserId);

                        command.Parameters.AddWithValue(
                            "question_text",
                           EncryptionEnhanced.Encrypt(model.QuestionText) );

                        //command.Parameters.AddWithValue(
                        //    "question_type",
                        //    model.QuestionType);

                        var questionTypeParam = new NpgsqlParameter("question_type", NpgsqlTypes.NpgsqlDbType.Text)
                        {
                            Value = model.QuestionType ?? "SUBJECTIVE"
                        };
                        command.Parameters.Add(questionTypeParam);

                        command.Parameters.AddWithValue(
                            "option1",
                            EncryptionEnhanced.Encrypt(model.Option1));

                        command.Parameters.AddWithValue(
                            "option2",
                            EncryptionEnhanced.Encrypt(model.Option2));

                        command.Parameters.AddWithValue(
                            "option3",
                            EncryptionEnhanced.Encrypt(model.Option3));

                        command.Parameters.AddWithValue(
                            "option4",
                            EncryptionEnhanced.Encrypt(model.Option4));

                        command.Parameters.AddWithValue(
                            "correct_answer",
                            EncryptionEnhanced.Encrypt(model.CorrectAnswer));

                        command.Parameters.AddWithValue(
                            "difficulty_level",
                            model.DifficultyLevel);

                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    return true;
                }
            }
        }
        public async Task<List<QuestionVersionViewModel>>
    GetQuestionVersionsAsync(
        long questionId,
        long setterUserId)
        {
            var result =
                new List<QuestionVersionViewModel>();

            const string sql = @"
SELECT
    qv.version_id,
    qv.question_id,
    qv.version_no,
    qv.question_text,
    qv.question_type,
    qv.difficulty_level,
    qv.changed_by,
    qv.change_reason,
    qv.created_at

FROM question_version qv

INNER JOIN question q
    ON q.question_id = qv.question_id

WHERE qv.question_id = @question_id
  AND q.created_by = @setter_user_id

ORDER BY qv.version_no DESC;
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
                        "question_id",
                        questionId);

                    command.Parameters.AddWithValue(
                        "setter_user_id",
                        setterUserId);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new QuestionVersionViewModel
                                {
                                    VersionId =
                                        Convert.ToInt64(
                                            reader["version_id"]),

                                    QuestionId =
                                        Convert.ToInt64(
                                            reader["question_id"]),

                                    VersionNo =
                                        Convert.ToInt32(
                                            reader["version_no"]),

                                    QuestionText =EncryptionEnhanced.Decrypt(
                                        reader["question_text"].ToString()),

                                    QuestionType =
                                        reader["question_type"].ToString(),

                                    DifficultyLevel =
                                        reader["difficulty_level"].ToString(),

                                    ChangedBy =
                                        Convert.ToInt64(
                                            reader["changed_by"]),

                                    ChangeReason =
                                        reader["change_reason"] == DBNull.Value
                                            ? null
                                            : reader["change_reason"].ToString(),

                                    CreatedAt =
                                        Convert.ToDateTime(
                                            reader["created_at"])
                                });
                        }
                    }
                }
            }

            return result;
        }

    }
}
