using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IReportRepository
    {
        // One row per active assignment (optionally for one exam) with target and status counts.
        Task<List<AssignmentProgressRow>> GetAssignmentProgressAsync(long? examId);

        // One row per active assignment and paper it covers (optionally for one exam): paper-wise progress.
        Task<List<PaperProgressRow>> GetPaperProgressAsync(long? examId);
    }
}
