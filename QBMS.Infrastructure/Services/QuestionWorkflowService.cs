using System.Threading.Tasks;
using QBMS.Application.Interfaces;

namespace QBMS.Infrastructure.Services
{
    public class QuestionWorkflowService
        : IQuestionWorkflowService
    {
        private readonly IQuestionWorkflowRepository
            _repository;

        public QuestionWorkflowService(
            IQuestionWorkflowRepository repository)
        {
            _repository = repository;
        }

        public Task<bool> SubmitToVetterAsync(
            long questionId,
            long setterUserId)
        {
            return _repository.SubmitToVetterAsync(
                questionId,
                setterUserId);
        }

        public Task<bool> ReviewQuestionAsync(
            long questionId,
            long vetterUserId,
            string decision,
            string remarks)
        {
            return _repository.ReviewQuestionAsync(
                questionId,
                vetterUserId,
                decision,
                remarks);
        }
    }
}
