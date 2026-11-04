using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface ITosRepository
    {
        Task<List<TosViewModel>> GetAllAsync();
    }
}

