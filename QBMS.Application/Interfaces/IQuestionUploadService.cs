using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IQuestionUploadService
    {
        Task<QuestionUploadViewModel> ProcessFileAsync(
            QuestionUploadViewModel model,
            long setterUserId);

        Task<bool> SaveQuestionsAsync(
            QuestionUploadViewModel model,
            long setterUserId);
    }
}
