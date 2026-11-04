using QBMS.Application.Models;
using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface IVetterQuestionReviewService
    {
        Task<VetterQuestionReviewViewModel> GetReviewQuestionAsync(long vetterUserId, long? questionId);
        Task<long?> GetPreviousQuestionIdAsync(long vetterUserId, long questionId);
        Task<long?> GetNextQuestionIdAsync(long vetterUserId, long questionId);
        Task<bool> SaveQuestionReviewAsync(long vetterUserId, long questionId, string questionText, string option1, string option2, string option3,
            string option4,string correctAnswer, string remarks, string action);
        Task <bool>SaveProgressAsync(long vetterUserId, long questionId);
        Task<bool> ApproveQuestionAsync(long vetterUserId,long questionId,string remarks);
        Task<bool> RejectedQuestionAsync(long vetterUserId,long questionId,string remarks);
    }
}
