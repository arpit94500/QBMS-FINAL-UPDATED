using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface IQuestionAssignmentService
    {
        Task<long> CreateAsync(
    TosAssignmentRequest request,
    long assignedBy);

        Task<List<TosAssignmentManagementRow>>
            GetAllForManagementAsync(string search, long? examId = null, long? paperId = null);

        Task<ReassignTosAssignmentViewModel>
            GetForReassignmentAsync(long assignmentId);

        Task ReassignAsync(
            long assignmentId,
            long newSetterUserId,
            long newVetterUserId,
            long assignedBy,
            bool transferUnfinishedQuestions);

    }
}
