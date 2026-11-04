using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface IQuestionWorkflowRepository
    {
        Task<bool> SubmitToVetterAsync(
            long questionId,
            long setterUserId);

        Task<bool> ReviewQuestionAsync(
            long questionId,
            long vetterUserId,
            string decision,
            string remarks);
    }
}
