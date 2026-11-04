using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface IHrcdfService
    {
        /// <summary>Looks up an employee in HRCDF by personnel number and returns the name.</summary>
        Task<HrcdfLookupResult> GetEmployeeAsync(string personnelNo);
    }
}
