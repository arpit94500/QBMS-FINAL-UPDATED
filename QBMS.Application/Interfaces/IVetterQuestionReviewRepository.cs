using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IVetterQuestionReviewRepository
    {
        Task<VetterQuestionReviewViewModel>
            GetFirstQuestionAsync(long vetterUserId);

        Task<long?>
            GetFirstQuestionIdByExamAsync(
                long vetterUserId,
                long examId);

        Task<VetterQuestionReviewViewModel>
            GetQuestionAsync(
                long vetterUserId,
                long questionId);

        Task<long?>
            GetPreviousQuestionIdAsync(
                long vetterUserId,
                long questionId);

        Task<long?>
            GetNextQuestionIdAsync(
                long vetterUserId,
                long questionId);

        Task<long?>
            GetResumeQuestionIdAsync(
                long vetterUserId);

        Task <bool>SaveProgressAsync(
            long vetterUserId,
            long questionId);


        Task<bool> SaveQuestionReviewAsync(long vetterUserId,long questionId,string questionText,string option1,string option2,string option3,
            string option4, string correctAnswer,string remarks,string action);
        // Not required
        Task<bool>
            ApproveQuestionAsync(
                long vetterUserId,
                long questionId,
                string remarks);

        Task<bool>
            RejectQuestionAsync(
                long vetterUserId,
                long questionId,
                string remarks);
    }
}
