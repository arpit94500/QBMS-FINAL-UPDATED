using QBMS.Application.Models;

using System.Collections.Generic;
using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface ITosService
    {
        Task<List<TosViewModel>> GetAllAsync();

        Task<int> SyncAsync();
    }

}
