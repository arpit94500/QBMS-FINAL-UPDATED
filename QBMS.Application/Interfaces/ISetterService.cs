using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface ISetterService
    {
        Task<List<SetterAssignmentViewModel>>
            GetAssignmentsAsync(long setterUserId);

        Task<List<CreateQuestionViewModel>>
            GetTopicsAsync(
                long assignmentId,
                long setterUserId);

        Task<long> CreateQuestionAsync(
            CreateQuestionViewModel model,
            long setterUserId);
        Task<List<SetterQuestionViewModel>>
    GetQuestionsAsync(long setterUserId);
        Task<EditQuestionViewModel>
    GetQuestionForEditAsync(
        long questionId,
        long setterUserId);

        Task<bool>
            UpdateQuestionAsync(
                EditQuestionViewModel model,
                long setterUserId);

        Task<List<QuestionVersionViewModel>>
            GetQuestionVersionsAsync(
                long questionId,
                long setterUserId);

    }
}
