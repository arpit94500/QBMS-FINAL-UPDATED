using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data.SqlClient;

namespace QBMS.Infrastructure.Services
{
    public class TosSyncRepository : ITosSyncRepository
    {
        private readonly string _inoesConnectionString;
        private readonly string _qbmsConnectionString;
        public TosSyncRepository(string inoesConnectionString, string qbmsConnectionString) {
            _inoesConnectionString = inoesConnectionString;
            _qbmsConnectionString = qbmsConnectionString; 
        }
        public async Task<List<TosSyncModel>> GetFromInoesAsync() {
            var result = new List<TosSyncModel>();
            const string sql = @" 
SELECT  [TOPIC_ID]
      ,[TOPIC_NAME]
      ,[TOPIC_TYPE]
      ,[TOPIC_EASY_QUESTION]
      ,[TOPIC_EASY_MARKS]
      ,[TOPIC_MEDIUM_QUESTION]
      ,[TOPIC_MEDIUM_MARK]
      ,[TOPIC_HARD_QUESTION]
      ,[TOPIC_HARD_MARKS]
      ,td.[MAIN_EXAM_ID],me.EXAM_NAME
      ,[PAPER_ID],ep.PAPER_NAME
      ,td.[SUBSECTION_ID],ts.SUBSECTION_NAME
      ,td.[SECTION_ID],mps.SECTION_NAME
      ,td.[IS_ACTIVE]
      ,[TOS_STATUS]
  FROM [CEC_SUBJECTIVE_25].[dbo].[TOPIC_DETAILS] td left join MSTR_MAIN_EXAM me on MAIN_EXAM_ID=me.MAINEXAM_ID 
  left join MSTR_EXAM_PAPER ep on td.PAPER_ID=ep.EXAM_PAPER_ID 
  left join [TBL_SUBSECTION] TS on td.SUBSECTION_ID=ts.SUBSECTION_ID
  left join MSTR_PAPER_SECTION mps on td.SECTION_ID=mps.EXAM_PAPER_SEC_ID where me.EXAM_NAME is not null and ep.paper_name is not null"; 
            using (var connection = new SqlConnection(_inoesConnectionString)) 
            { 
                await connection.OpenAsync();
                using (var command = new SqlCommand(sql, connection)) 
                { 
                    using (var reader = await command.ExecuteReaderAsync()) 
                    {
                      
                        while (await reader.ReadAsync()) 
                        {
                            result.Add(new TosSyncModel
                            {
                                TopicId = reader.GetInt32(0),
                                TopicName = reader.IsDBNull(1) ? null : reader.GetString(1),
                                TopicType=  reader.IsDBNull(2) ? null : reader.GetString(2),
                                TopicEasyQuestion=reader.GetInt32(3),
                                TopicEasyMarks=reader.GetInt32(4),
                                TopicMediumQuestion=reader.GetInt32(5),
                                TopicMediumMarks=reader.GetInt32(6),
                                TopicHardQuestion=reader.GetInt32(7),
                                TopicHardMarks=reader.GetInt32(8),
                                ExamId = reader.GetInt32(9),
                                ExamName = reader.IsDBNull(10) ? null : reader.GetString(10),
                                PaperId=reader.GetInt32(11),
                                PaperName= reader.IsDBNull(12) ? null : reader.GetString(12),
                                SectionId = reader.GetInt32(15),
                                SectionName = reader.IsDBNull(16) ? null : reader.GetString(16),
                                SubsectionId = reader.IsDBNull(13) ? (long?)null : reader.GetInt32(13),
                                SubsectionName = reader.IsDBNull(14) ? null : reader.GetString(14),
                                IsActive=reader.GetInt32(17),
                                TosStatus=reader.GetInt32(18)
                            }); 
                            
                        } 
                    }
                } 
            } return result;
        }
        public async Task<int> SyncToQbmsAsync(List<TosSyncModel> tosList)
        {
            int affectedRows = 0;
            using (var connection = new NpgsqlConnection(_qbmsConnectionString)) 
            { 
                await connection.OpenAsync();
                const string sql1 = @"Truncate Table tos cascade;";
                using (var comm = new NpgsqlCommand(sql1, connection))
                {
                    comm.ExecuteNonQuery();
                }
                foreach (var item in tosList) 
                {

                    const string sql = @"INSERT INTO tos(topic_id, topic_name, topic_type, topic_easy_question, topic_easy_marks, topic_medium_question, topic_medium_mark, topic_hard_question, topic_hard_marks, main_exam_id, main_exam_name, paper_id, paper_name, subsection_id, subsection_name, section_id, section_name, is_active, created_on, created_by,  tos_status)
Values (
	@topic_id, @topic_name, @topic_type, @topic_easy_question, @topic_easy_marks, @topic_medium_question, @topic_medium_mark, 
@topic_hard_question, @topic_hard_marks, @main_exam_id, @main_exam_name, @paper_id, @paper_name, @subsection_id, @subsection_name, @section_id, @section_name, 1 , Current_timestamp, 1, @tos_status);
	";
                                          using (var command = new NpgsqlCommand(sql, connection)) 
                    { 
                        command.Parameters.AddWithValue("topic_id", item.TopicId);
                        command.Parameters.AddWithValue("topic_name", (object)item.TopicName ?? DBNull.Value);
                        command.Parameters.AddWithValue("topic_type", (object)item.TopicType ?? DBNull.Value);
                        command.Parameters.AddWithValue("topic_easy_question", item.TopicEasyQuestion);
                        command.Parameters.AddWithValue("topic_easy_marks", item.TopicEasyMarks);
                        command.Parameters.AddWithValue("topic_medium_question", item.TopicMediumQuestion);
                        command.Parameters.AddWithValue("topic_medium_mark", item.TopicMediumMarks);
                        command.Parameters.AddWithValue("topic_hard_question", item.TopicHardQuestion);
                        command.Parameters.AddWithValue("topic_hard_marks", item.TopicHardMarks);
                        command.Parameters.AddWithValue("main_exam_id", item.ExamId);
                        command.Parameters.AddWithValue("main_exam_name", (object)item.ExamName ?? DBNull.Value);
                        command.Parameters.AddWithValue("paper_id", item.PaperId);
                        command.Parameters.AddWithValue("paper_name", (object)item.PaperName ?? DBNull.Value);
                        command.Parameters.AddWithValue("subsection_id", item.SubsectionId);
                        command.Parameters.AddWithValue("subsection_name", (object)item.SubsectionName ?? DBNull.Value);
                        command.Parameters.AddWithValue("section_id", item.SectionId);
                        command.Parameters.AddWithValue("section_name", (object)item.SectionName ?? DBNull.Value);
                        command.Parameters.AddWithValue("tos_status", item.TosStatus);

                        await command.ExecuteNonQueryAsync(); 
                        affectedRows++; 
                    } 
                }
            }
            return affectedRows;
        }
    }
}
