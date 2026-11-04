using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IUploadBatchRepository
    {
        Task<long> CreateBatchAsync(
            long assignmentId,
            long setterUserId,
            string fileName,
            string fileType,
            string[] headerRow,
            List<UploadBatchItemModel> items,
            byte[] fileData = null);

        // Questions staged in batches that are still PENDING / CHECKING / SAVING.
        // They count against the setter's quota so two uploads cannot over-use it.
        Task<(int easy, int medium, int hard)> GetReservedCountsAsync(
            long assignmentId,
            long setterUserId,
            long excludeBatchId);

        Task<List<UploadBatchViewModel>> GetBatchesAsync(long setterUserId);

        Task<UploadBatchViewModel> GetBatchAsync(long batchId, long setterUserId);

        // Text of existing questions (decrypted, as stored) by id - used to show the matches.
        Task<Dictionary<long, string>> GetQuestionTextsAsync(IList<long> questionIds);

        // Saved questions that have no row in question_embeddings yet (decrypted text).
        Task<List<KeyValuePair<long, string>>> GetUnindexedQuestionsAsync(int limit, ICollection<long> skipIds);

        // Worker side
        Task ResetStuckBatchesAsync();

        Task<UploadBatchViewModel> ClaimNextPendingBatchAsync();

        Task SaveItemResultAsync(long itemId, string checkStatus, decimal topScore, List<DuplicateMatchModel> matches);

        Task UpdateProgressAsync(long batchId, int processedQuestions, int duplicateCount);

        Task SetItemSavedQuestionAsync(long itemId, long questionId);

        Task CompleteBatchAsync(long batchId, string status, string message);
    }
}
