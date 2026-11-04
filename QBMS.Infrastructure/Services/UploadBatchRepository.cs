using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Npgsql;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class UploadBatchRepository : IUploadBatchRepository
    {
        private readonly string _connectionString;

        public UploadBatchRepository(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<long> CreateBatchAsync(
            long assignmentId,
            long setterUserId,
            string fileName,
            string fileType,
            string[] headerRow,
            List<UploadBatchItemModel> items,
            byte[] fileData = null)
        {
            const string batchSql = @"
INSERT INTO question_upload_batch
    (assignment_id, setter_user_id, file_name, file_type, header_json, file_data, status, total_questions, processed_questions, duplicate_count, uploaded_at)
VALUES
    (@assignmentId, @setterUserId, @fileName, @fileType, @headerJson, @fileData, 'PENDING', @total, 0, 0, CURRENT_TIMESTAMP)
RETURNING batch_id;";

            const string itemSql = @"
INSERT INTO question_upload_item
    (batch_id, source_row, difficulty_level, payload, row_cells, check_status)
VALUES
    (@batchId, @rowNumber, @difficulty, @payload, @rowCells, 'PENDING');";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var transaction = await connection.BeginTransactionAsync())
                {
                    try
                    {
                        long batchId;

                        using (var command = new NpgsqlCommand(batchSql, connection, transaction))
                        {
                            command.Parameters.AddWithValue("@assignmentId", assignmentId);
                            command.Parameters.AddWithValue("@setterUserId", setterUserId);
                            command.Parameters.AddWithValue("@fileName", fileName ?? "");
                            command.Parameters.AddWithValue("@fileType", fileType ?? "");
                            command.Parameters.AddWithValue("@headerJson", JsonConvert.SerializeObject(headerRow ?? new string[0]));
                            command.Parameters.AddWithValue("@total", items.Count);
                            command.Parameters.AddWithValue("@fileData", fileData == null ? (object)DBNull.Value : EncryptionEnhanced.Encrypt(Convert.ToBase64String(fileData)));

                            batchId = Convert.ToInt64(await command.ExecuteScalarAsync());
                        }

                        foreach (var item in items)
                        {
                            using (var command = new NpgsqlCommand(itemSql, connection, transaction))
                            {
                                command.Parameters.AddWithValue("@batchId", batchId);
                                command.Parameters.AddWithValue("@rowNumber", item.RowNumber);
                                command.Parameters.AddWithValue("@difficulty", (item.Question.DifficultyLevel ?? "").Trim().ToUpperInvariant());
                                command.Parameters.AddWithValue("@payload", EncryptionEnhanced.Encrypt(JsonConvert.SerializeObject(item.Question)));
                                command.Parameters.AddWithValue("@rowCells", EncryptionEnhanced.Encrypt(JsonConvert.SerializeObject(item.RowCells ?? new string[0])));
                                await command.ExecuteNonQueryAsync();
                            }
                        }

                        await transaction.CommitAsync();
                        return batchId;
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                }
            }
        }

        public async Task<(int easy, int medium, int hard)> GetReservedCountsAsync(
            long assignmentId,
            long setterUserId,
            long excludeBatchId)
        {
            const string sql = @"
SELECT
    COUNT(*) FILTER (WHERE i.difficulty_level = 'EASY'),
    COUNT(*) FILTER (WHERE i.difficulty_level = 'MEDIUM'),
    COUNT(*) FILTER (WHERE i.difficulty_level = 'HARD')
FROM question_upload_item i
JOIN question_upload_batch b ON b.batch_id = i.batch_id
WHERE b.assignment_id = @assignmentId
  AND b.setter_user_id = @setterUserId
  AND b.status IN ('PENDING', 'CHECKING', 'SAVING')
  AND i.saved_question_id IS NULL
  AND (@excludeBatchId = 0 OR b.batch_id <> @excludeBatchId);";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@assignmentId", assignmentId);
                    command.Parameters.AddWithValue("@setterUserId", setterUserId);
                    command.Parameters.AddWithValue("@excludeBatchId", excludeBatchId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (!await reader.ReadAsync())
                        {
                            return (0, 0, 0);
                        }

                        return (Convert.ToInt32(reader.GetInt64(0)),
                                Convert.ToInt32(reader.GetInt64(1)),
                                Convert.ToInt32(reader.GetInt64(2)));
                    }
                }
            }
        }

        public async Task<List<UploadBatchViewModel>> GetBatchesAsync(long setterUserId)
        {
            const string sql = BatchSelect + @"
WHERE b.setter_user_id = @setterUserId
ORDER BY b.batch_id DESC
LIMIT 20;";

            var batches = new List<UploadBatchViewModel>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@setterUserId", setterUserId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            batches.Add(ReadBatch(reader));
                        }
                    }
                }
            }

            return batches;
        }

        public async Task<UploadBatchViewModel> GetBatchAsync(long batchId, long setterUserId)
        {
            const string sql = BatchSelect + @"
WHERE b.batch_id = @batchId
  AND (@setterUserId = 0 OR b.setter_user_id = @setterUserId);";

            UploadBatchViewModel batch = null;

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@batchId", batchId);
                    command.Parameters.AddWithValue("@setterUserId", setterUserId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            batch = ReadBatch(reader);
                        }
                    }
                }

                if (batch != null)
                {
                    batch.Items = await ReadItemsAsync(connection, batchId);

                    using (var command = new NpgsqlCommand("SELECT file_data FROM question_upload_batch WHERE batch_id = @batchId;", connection))
                    {
                        command.Parameters.AddWithValue("@batchId", batchId);
                        var stored = await command.ExecuteScalarAsync();
                        if (stored != null && stored != DBNull.Value)
                        {
                            batch.FileData = Convert.FromBase64String(EncryptionEnhanced.Decrypt((string)stored));
                        }
                    }
                }
            }

            return batch;
        }

        public async Task<Dictionary<long, string>> GetQuestionTextsAsync(IList<long> questionIds)
        {
            var result = new Dictionary<long, string>();
            if (questionIds == null || questionIds.Count == 0)
            {
                return result;
            }

            const string sql = @"
SELECT question_id, question_text
FROM question
WHERE question_id = ANY(@ids);";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@ids", questionIds.ToArray());

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result[reader.GetInt64(0)] = reader.IsDBNull(1) ? "" : EncryptionEnhanced.Decrypt(reader.GetString(1));
                        }
                    }
                }
            }

            return result;
        }

        public async Task<List<KeyValuePair<long, string>>> GetUnindexedQuestionsAsync(int limit, ICollection<long> skipIds)
        {
            const string sql = @"
SELECT q.question_id, q.question_text
FROM question q
WHERE (
        NOT EXISTS (SELECT 1 FROM question_embeddings e WHERE e.question_id = q.question_id)
     OR NOT EXISTS (SELECT 1 FROM question_math_profile p
                    WHERE p.question_id = q.question_id AND p.profile_version >= 1)
      )
  AND q.question_id <> ALL(@skipIds)
ORDER BY q.question_id
LIMIT @limit;";

            var result = new List<KeyValuePair<long, string>>();

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@skipIds", (skipIds ?? new List<long>()).ToArray());
                    command.Parameters.AddWithValue("@limit", limit);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            result.Add(new KeyValuePair<long, string>(
                                reader.GetInt64(0),
                                reader.IsDBNull(1) ? "" : EncryptionEnhanced.Decrypt(reader.GetString(1))));
                        }
                    }
                }
            }

            return result;
        }

        public async Task ResetStuckBatchesAsync()
        {
            // A batch left in CHECKING / SAVING by an application restart is checked again.
            // Items that already have a result keep it; saved items are never saved twice.
            const string sql = @"
UPDATE question_upload_batch
SET status = 'PENDING'
WHERE status IN ('CHECKING', 'SAVING');";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task<UploadBatchViewModel> ClaimNextPendingBatchAsync()
        {
            const string sql = @"
UPDATE question_upload_batch
SET status = 'CHECKING',
    started_at = COALESCE(started_at, CURRENT_TIMESTAMP)
WHERE batch_id =
(
    SELECT batch_id
    FROM question_upload_batch
    WHERE status = 'PENDING'
    ORDER BY batch_id
    FOR UPDATE SKIP LOCKED
    LIMIT 1
)
RETURNING batch_id, setter_user_id;";

            long batchId;
            long setterUserId;

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                using (var reader = await command.ExecuteReaderAsync())
                {
                    if (!await reader.ReadAsync())
                    {
                        return null;
                    }

                    batchId = reader.GetInt64(0);
                    setterUserId = reader.GetInt64(1);
                }
            }

            return await GetBatchAsync(batchId, setterUserId);
        }

        public async Task SaveItemResultAsync(long itemId, string checkStatus, decimal topScore, List<DuplicateMatchModel> matches)
        {
            const string sql = @"
UPDATE question_upload_item
SET check_status = @checkStatus,
    top_score = @topScore,
    matches_json = @matchesJson
WHERE item_id = @itemId;";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@itemId", itemId);
                    command.Parameters.AddWithValue("@checkStatus", checkStatus);
                    command.Parameters.AddWithValue("@topScore", topScore);
                    command.Parameters.AddWithValue("@matchesJson", EncryptionEnhanced.Encrypt(JsonConvert.SerializeObject(matches ?? new List<DuplicateMatchModel>())));
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task UpdateProgressAsync(long batchId, int processedQuestions, int duplicateCount)
        {
            const string sql = @"
UPDATE question_upload_batch
SET processed_questions = @processed,
    duplicate_count = @duplicates
WHERE batch_id = @batchId;";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@batchId", batchId);
                    command.Parameters.AddWithValue("@processed", processedQuestions);
                    command.Parameters.AddWithValue("@duplicates", duplicateCount);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task SetItemSavedQuestionAsync(long itemId, long questionId)
        {
            const string sql = @"
UPDATE question_upload_item
SET saved_question_id = @questionId
WHERE item_id = @itemId;";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@itemId", itemId);
                    command.Parameters.AddWithValue("@questionId", questionId);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        public async Task CompleteBatchAsync(long batchId, string status, string message)
        {
            // PENDING / CHECKING / SAVING are not final (PENDING also hands a batch back to the
            // worker when the Python API is down), so only terminal statuses get a completion time.
            const string sql = @"
UPDATE question_upload_batch
SET status = @status,
    message = @message,
    completed_at = CASE WHEN @status IN ('PENDING', 'CHECKING', 'SAVING') THEN NULL ELSE CURRENT_TIMESTAMP END
WHERE batch_id = @batchId;";

            using (var connection = new NpgsqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                using (var command = new NpgsqlCommand(sql, connection))
                {
                    command.Parameters.AddWithValue("@batchId", batchId);
                    command.Parameters.AddWithValue("@status", status);
                    command.Parameters.AddWithValue("@message", (object)message ?? DBNull.Value);
                    await command.ExecuteNonQueryAsync();
                }
            }
        }

        private const string BatchSelect = @"
SELECT b.batch_id, b.assignment_id, b.setter_user_id, b.file_name, b.file_type, b.header_json,
       b.status, b.total_questions, b.processed_questions, b.duplicate_count, b.message,
       b.uploaded_at, b.completed_at
FROM question_upload_batch b
";

        private static UploadBatchViewModel ReadBatch(NpgsqlDataReader reader)
        {
            return new UploadBatchViewModel
            {
                BatchId = reader.GetInt64(0),
                AssignmentId = reader.GetInt64(1),
                SetterUserId = reader.GetInt64(2),
                FileName = reader.GetString(3),
                FileType = reader.GetString(4),
                HeaderRow = JsonConvert.DeserializeObject<string[]>(reader.GetString(5)) ?? new string[0],
                Status = reader.GetString(6),
                TotalQuestions = reader.GetInt32(7),
                ProcessedQuestions = reader.GetInt32(8),
                DuplicateCount = reader.GetInt32(9),
                Message = reader.IsDBNull(10) ? null : reader.GetString(10),
                UploadedAt = reader.GetDateTime(11),
                CompletedAt = reader.IsDBNull(12) ? (DateTime?)null : reader.GetDateTime(12)
            };
        }

        private static async Task<List<UploadBatchItemModel>> ReadItemsAsync(NpgsqlConnection connection, long batchId)
        {
            const string sql = @"
SELECT item_id, source_row, payload, row_cells, check_status, top_score, matches_json, saved_question_id
FROM question_upload_item
WHERE batch_id = @batchId
ORDER BY source_row, item_id;";

            var items = new List<UploadBatchItemModel>();

            using (var command = new NpgsqlCommand(sql, connection))
            {
                command.Parameters.AddWithValue("@batchId", batchId);

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var item = new UploadBatchItemModel
                        {
                            ItemId = reader.GetInt64(0),
                            RowNumber = reader.GetInt32(1),
                            Question = JsonConvert.DeserializeObject<CreateQuestionViewModel>(EncryptionEnhanced.Decrypt(reader.GetString(2))),
                            RowCells = JsonConvert.DeserializeObject<string[]>(EncryptionEnhanced.Decrypt(reader.GetString(3))) ?? new string[0],
                            CheckStatus = reader.GetString(4),
                            TopScore = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5),
                            SavedQuestionId = reader.IsDBNull(7) ? (long?)null : reader.GetInt64(7)
                        };

                        if (!reader.IsDBNull(6))
                        {
                            item.Matches = JsonConvert.DeserializeObject<List<DuplicateMatchModel>>(EncryptionEnhanced.Decrypt(reader.GetString(6)))
                                           ?? new List<DuplicateMatchModel>();
                        }

                        items.Add(item);
                    }
                }
            }

            return items;
        }
    }
}
