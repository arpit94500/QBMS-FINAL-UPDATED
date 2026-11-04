using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Threading.Tasks;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class AdminQuestionRepository
        : IAdminQuestionRepository
    {
        private readonly string _inoesConnectionString;
        private readonly string _connectionString;

        public AdminQuestionRepository(
            string connectionString,string inoes)
        {
            _inoesConnectionString = inoes;
            _connectionString = connectionString;
        }

        public async Task<List<AdminQuestionReviewViewModel>>
            GetPendingQuestionsAsync(long? examId, long? paperId)
        {
            var result =
                new List<AdminQuestionReviewViewModel>();

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
    q.status,
    q.created_by

FROM question q

INNER JOIN tos t
    ON t.main_exam_id = q.main_exam_id
   AND t.paper_id = q.paper_id
   AND t.section_id = q.section_id
   AND t.subsection_id IS NOT DISTINCT FROM q.subsection_id
   AND t.topic_id = q.topic_id

WHERE q.status = 'VETTER_APPROVED'
AND (q.main_exam_id IS NULL OR q.main_exam_id=@exam_id)
AND (q.paper_id IS NULL OR q.paper_id=@paper_id)
ORDER BY q.question_id DESC;
";

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command =
                    new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("exam_id", (object)examId ?? DBNull.Value);
                    command.Parameters.AddWithValue("paper_id",(object)paperId ?? DBNull.Value);

                    using (var reader =
                        await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(
                                new AdminQuestionReviewViewModel
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

                                    DifficultyLevel =
                                        reader["difficulty_level"]
                                            .ToString(),

                                    QuestionText = EncryptionEnhanced.Decrypt(
                                        reader["question_text"]
                                            .ToString()),
                                    Option1 = EncryptionEnhanced.Decrypt(reader["option1"]
                                            .ToString()),
                                    Option2 = EncryptionEnhanced.Decrypt(reader["option2"].ToString()),

                                    Option3 = EncryptionEnhanced.Decrypt(reader["option3"].ToString()),
                                    Option4 = EncryptionEnhanced.Decrypt(reader["option4"].ToString()),
                                    CorrectAnswer = EncryptionEnhanced.Decrypt(reader["correct_answer"].ToString()),

                                    Status =
                                        reader["status"]
                                            .ToString(),

                                    SetterUserId =
                                        Convert.ToInt64(
                                            reader["created_by"])
                                });
                        }
                    }
                }
            }

            return result;
        }


        public async Task<bool> ReviewQuestionAsync(
            long questionId,
            long adminUserId,
            string decision,
            string remarks)
        {
            string newStatus;

            if (decision == "APPROVE")
            {
                newStatus = "COMPLETED";
                await ToINOESAsync(questionId, adminUserId, decision);

            }
            else if (decision == "REJECT")
            {
                newStatus = "ADMIN_REJECTED";
            }
            else
            {
                throw new ArgumentException(
                    "Invalid Admin decision.");
            }

            using (var connection =
                new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction =
                    await connection.BeginTransactionAsync())
                {
                    const string updateQuestionSql = @"
UPDATE question
SET
    status = @status,
    updated_at = CURRENT_TIMESTAMP
WHERE question_id = @question_id
  AND status = 'VETTER_APPROVED';
";

                    int updatedRows;

                    using (var command =
                        new NpgsqlCommand(
                            updateQuestionSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "status",
                            newStatus);

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

                    const string insertReviewSql = @"
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
    'ADMIN',
    @decision,
    @remarks
);
";

                    using (var command =
                        new NpgsqlCommand(
                            insertReviewSql,
                            connection,
                            transaction))
                    {
                        command.Parameters.AddWithValue(
                            "question_id",
                            questionId);

                        command.Parameters.AddWithValue(
                            "reviewer_id",
                            adminUserId);

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
        long main_exam_id;
        long paper_id;
        long section_id;
        long subsection_id;
        long topic_id;
        string question_text;
        string option1;
        string option2;
        string option3;
        string option4;
        string correct_answer;
        string question_type;
        string difficulty_level;
        long created_by;
        string created_by1;
        //string status;
        string created_at;
        string updated_at;
        //string latest_remarks;
        int question_mark;
        string vetterid;
        string adminid;

        public async Task<bool> ToINOESAsync(long questionId,long adminUserId,string decision)
        {

            

            if (decision == "APPROVE")
            {
                //POSTGRE CONNECTION
                string sql = "select * from question where question_id="+ questionId;
                string sql2 = "Insert into MSTR_QUESTION_BANK (QUESLANGUAGE ,MAIN_EXAM_ID ,EXAM_PAPER_ID ,SECTION_ID ,SUBSECTION_ID ,TOPIC_ID ,DIFFICULTY_LEVEL,QUESTION_TYPE ,QUESTION_TEXT ,OPTION_1  ,OPTION_2 ,OPTION_3 ,OPTION_4, CORRECT_ANSWER  ,QUESTION_MARK, CREATED_ON, CREATED_BY, UPDATED_ON ,UPDATED_BY,IS_ACTIVE ,APPROVED_ON ,APPROVED_BY,FLAG ,QB_MAKER,QB_VALIDATE,QB_APPROVED) " +
                    "values('English', @main_exam_id,@paper_id,@section_id,@subsection_id,@topic_id,@difficulty_level,@question_type,@question_text,@option1,@option2,@option3,@option4,@correct_answer,@question_mark,convert(datetime,@created_at,105),'1',convert(datetime,@updated_at,105),'1','1',getdate(),1,0,@created_by1,@vetterid,@adminUserId)";
                string sql3 = "Select topic_easy_marks from tos where topic_id=@topic_id";
                string sql4 = "select topic_medium_mark from tos where topic_id=@topic_id";
                string sql5 = "select topic_hard_marks from tos where topic_id=@topic_id";
                string sql6= "SELECT m.personal_number FROM mstr_user m, question q where q.created_by = m.user_id and q.question_id = @question_id";
                string sql7= "Select m.personal_number from mstr_user m, question_assignment qa , question q "+
"where qa.vetter_user_id = m.user_id and q.main_exam_id = qa.main_exam_id and q.question_id = @question_id";
                string sql8 = "select personal_number from mstr_user where user_id=@adminUserId";
                using (var connection = new NpgsqlConnection(_connectionString))
                {
                    await connection.OpenAsync();
                    using (var command= new NpgsqlCommand(sql,connection))
                    {
                        using (var reader= await command.ExecuteReaderAsync())
                        {
                                while (await reader.ReadAsync())
                                {
                                    main_exam_id = reader.GetInt64(1);
                                    paper_id = reader.GetInt64(2);
                                    section_id = reader.GetInt64(3);
                                    subsection_id = reader.GetInt64(4);
                                    topic_id = reader.GetInt64(5);
                                    question_text = reader.GetString(6);
                                    option1 = reader.GetString(7);
                                    option2 = reader.GetString(8);
                                    option3 = reader.GetString(9);
                                    option4 = reader.GetString(10);
                                    correct_answer = reader.GetString(11);
                                    question_type = reader.GetString(12);
                                if(question_type=="MCQ")
                                {
                                    question_type = "OBJ";
                                }else if (question_type=="SUBJECTIVE")
                                {
                                    question_type = "SUB";
                                }
                                    difficulty_level = reader.GetString(13);
                                    if(difficulty_level=="EASY")
                                    {
                                        difficulty_level = "E";
                                    var con = new NpgsqlConnection(_connectionString);
                                    await con.OpenAsync();
                                        var command1 = new NpgsqlCommand(sql3,con);
                                    command1.Parameters.AddWithValue("topic_id", topic_id);
                                    question_mark =(int)command1.ExecuteScalar();

                                    }else if (difficulty_level == "MEDIUM")
                                    {
                                        difficulty_level = "M";
                                    var con = new NpgsqlConnection(_connectionString);
                                    await con.OpenAsync();
                                    var command1 = new NpgsqlCommand(sql4, con);
                                    command1.Parameters.AddWithValue("topic_id", topic_id);

                                        question_mark = (int)command1.ExecuteScalar();
                                }
                                else if (difficulty_level == "HARD")
                                    {
                                        difficulty_level = "H";
                                    var con = new NpgsqlConnection(_connectionString);
                                    await con.OpenAsync();
                                    var command1 = new NpgsqlCommand(sql5, con);
                                    command1.Parameters.AddWithValue("topic_id", topic_id);
                                    question_mark = (int)command1.ExecuteScalar();
                                }
                                    created_by = reader.GetInt64(14);
                                var con1 = new NpgsqlConnection(_connectionString);
                                await con1.OpenAsync();
                                var command2 = new NpgsqlCommand(sql6, con1);
                                command2.Parameters.AddWithValue("question_id",reader.GetInt64(0));
                                created_by1 = command2.ExecuteScalar().ToString();

                                var con2 = new NpgsqlConnection(_connectionString);
                                await con2.OpenAsync();
                                var command3 = new NpgsqlCommand(sql7, con2);
                                command3.Parameters.AddWithValue("question_id", reader.GetInt64(0));
                                vetterid = command3.ExecuteScalar().ToString();


                                var con3 = new NpgsqlConnection(_connectionString);
                                await con3.OpenAsync();
                                var command4 = new NpgsqlCommand(sql8, con3);
                                command4.Parameters.AddWithValue("adminUserId", adminUserId);
                                adminid = command4.ExecuteScalar().ToString();

                                //status = reader.GetString(15);
                                created_at = reader.GetDateTime(16).ToString();
                                    updated_at = reader.GetDateTime(17).ToString();
                                    //latest_remarks = reader.GetString(18)?.;
                                
                                
                                }
                        }
                    }
                }
                using (var connection = new SqlConnection(_inoesConnectionString))
                {
                    connection.Open();
                    using (var command = new SqlCommand(sql2, connection))
                    {
                        command.Parameters.AddWithValue("main_exam_id", main_exam_id);
                        command.Parameters.AddWithValue("paper_id", paper_id);
                        command.Parameters.AddWithValue("section_id", section_id);
                        command.Parameters.AddWithValue("subsection_id", subsection_id);
                        command.Parameters.AddWithValue("topic_id", topic_id);
                        command.Parameters.AddWithValue("question_text", question_text);
                        command.Parameters.AddWithValue("option1", option1);
                        command.Parameters.AddWithValue("option2", option2);
                        command.Parameters.AddWithValue("option3", option3);
                        command.Parameters.AddWithValue("option4", option4);
                        command.Parameters.AddWithValue("question_mark", question_mark);
                        command.Parameters.AddWithValue("correct_answer", correct_answer);
                        command.Parameters.AddWithValue("question_type", question_type);
                        command.Parameters.AddWithValue("difficulty_level", difficulty_level);
                        command.Parameters.AddWithValue("created_by1", created_by1);
                        command.Parameters.AddWithValue("adminUserId", adminid);
                        command.Parameters.AddWithValue("created_at", created_at);
                        command.Parameters.AddWithValue("updated_at", updated_at);
                        command.Parameters.AddWithValue("vetterid", vetterid);

                        var updatedRows = await command.ExecuteNonQueryAsync(); 
                        
                    }
                }
                return true;
            }
            else 
                return false;
        }
    }

}
