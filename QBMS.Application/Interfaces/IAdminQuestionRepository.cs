using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IAdminQuestionRepository
    {
        Task<List<AdminQuestionReviewViewModel>>
            GetPendingQuestionsAsync(long? examId, long? paperId);

        Task<bool> ReviewQuestionAsync(
            long questionId,
            long adminUserId,
            string decision,
            string remarks);
    }
}
