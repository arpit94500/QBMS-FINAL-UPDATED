using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IQuestionQuotaService
    {
        Task<QuestionQuotaViewModel> GetQuotaAsync(
            long assignmentId,
            long setterUserId);
    }
}
