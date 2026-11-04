using QBMS.Application.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface ITosSyncRepository
    {
        Task<List<TosSyncModel>> GetFromInoesAsync();
        Task<int> SyncToQbmsAsync(List<TosSyncModel> tosList);

    }
}
