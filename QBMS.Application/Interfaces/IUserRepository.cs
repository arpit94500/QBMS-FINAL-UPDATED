using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface IUserRepository
    {
        Task<List<DropdownItem>> GetSettersAsync();
        Task<List<DropdownItem>> GetVetterAsync();
    }
}
