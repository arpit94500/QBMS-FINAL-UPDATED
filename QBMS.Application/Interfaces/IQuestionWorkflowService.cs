using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface IQuestionWorkflowService
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
