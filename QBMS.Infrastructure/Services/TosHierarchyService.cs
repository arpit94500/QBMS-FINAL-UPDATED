using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class TosHierarchyService :
        ITosHierarchyService
    {
        private readonly ITosHierarchyRepository
            _repository;

        public TosHierarchyService(
            ITosHierarchyRepository repository)
        {
            _repository = repository;
        }

        public Task<List<DropdownItem>>
            GetExamsAsync()
        {
            return _repository.GetExamsAsync();
        }

        public Task<List<DropdownItem>>
            GetPapersAsync(long examId)
        {
            return _repository.GetPapersAsync(
                examId);
        }

        public Task<List<DropdownItem>>
            GetSectionsAsync(long paperId)
        {
            return _repository.GetSectionsAsync(
                paperId);
        }

        public Task<List<DropdownItem>>
            GetSubsectionsAsync(long sectionId)
        {
            return _repository.GetSubsectionsAsync(
                sectionId);
        }

        public Task<List<DropdownItem>>
            GetTopicsAsync(long subsectionId)
        {
            return _repository.GetTopicsAsync(
                subsectionId);
        }
    }
}