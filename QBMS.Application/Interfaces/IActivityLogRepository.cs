using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IActivityLogRepository
    {
        // Never throws: logging must not break the application.
        Task InsertAsync(ActivityLogEntry entry);

        Task<ActivityLogPage> GetPageAsync(ActivityLogQuery query);
    }
}
