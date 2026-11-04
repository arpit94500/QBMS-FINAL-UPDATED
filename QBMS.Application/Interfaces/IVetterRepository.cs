using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IVetterRepository
    {
        Task<List<VetterQuestionViewModel>>
            GetQuestionsAsync(
                long vetterUserId);

        Task<List<VetterExamSummaryViewModel>>
            GetPendingExamSummaryAsync(
                long vetterUserId);
    }
}
