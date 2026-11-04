using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class SetterService : ISetterService
    {
        private readonly ISetterRepository _repository;

        public SetterService(
            ISetterRepository repository)
        {
            _repository = repository;
        }

        public Task<List<SetterAssignmentViewModel>>
            GetAssignmentsAsync(
                long setterUserId)
        {
            return _repository.GetAssignmentsAsync(
                setterUserId);
        }

        public Task<List<CreateQuestionViewModel>>
            GetTopicsAsync(
                long assignmentId,
                long setterUserId)
        {
            return _repository.GetTopicsAsync(
                assignmentId,
                setterUserId);
        }

        public Task<long> CreateQuestionAsync(
            CreateQuestionViewModel model,
            long setterUserId)
        {
            return _repository.CreateQuestionAsync(
                model,
                setterUserId);
        }
        public Task<List<SetterQuestionViewModel>>
    GetQuestionsAsync(long setterUserId)
        {
            return _repository.GetQuestionsAsync(
                setterUserId);
        }
        public Task<EditQuestionViewModel>
    GetQuestionForEditAsync(
        long questionId,
        long setterUserId)
        {
            return _repository.GetQuestionForEditAsync(
                questionId,
                setterUserId);
        }

        public Task<bool>
            UpdateQuestionAsync(
                EditQuestionViewModel model,
                long setterUserId)
        {
            return _repository.UpdateQuestionAsync(
                model,
                setterUserId);
        }

        public Task<List<QuestionVersionViewModel>>
            GetQuestionVersionsAsync(
                long questionId,
                long setterUserId)
        {
            return _repository.GetQuestionVersionsAsync(
                questionId,
                setterUserId);
        }


    }
}
