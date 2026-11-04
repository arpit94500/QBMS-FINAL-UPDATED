using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class AdminQuestionService
        : IAdminQuestionService
    {
        private readonly IAdminQuestionRepository
            _repository;

        public AdminQuestionService(
            IAdminQuestionRepository repository)
        {
            _repository = repository;
        }

        public Task<List<AdminQuestionReviewViewModel>>
            GetPendingQuestionsAsync(long? examId, long? paperId)
        {
            return _repository
                .GetPendingQuestionsAsync(examId,paperId);
        }

        public Task<bool> ReviewQuestionAsync(
            long questionId,
            long adminUserId,
            string decision,
            string remarks)
        {
            return _repository.ReviewQuestionAsync(
                questionId,
                adminUserId,
                decision,
                remarks);
        }
    }
}
