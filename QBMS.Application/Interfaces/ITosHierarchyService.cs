using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Models;

namespace QBMS.Application.Interfaces
{
    public interface ITosHierarchyService
    {
        Task<List<DropdownItem>> GetExamsAsync();

        Task<List<DropdownItem>> GetPapersAsync(
            long examId);

        Task<List<DropdownItem>> GetSectionsAsync(
            long paperId);

        Task<List<DropdownItem>> GetSubsectionsAsync(
            long sectionId);

        Task<List<DropdownItem>> GetTopicsAsync(
            long subsectionId);
    }
}