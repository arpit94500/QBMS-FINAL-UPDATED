using System.Collections.Generic;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class VetterService : IVetterService
    {
        private readonly IVetterRepository
            _repository;

        public VetterService(
            IVetterRepository repository)
        {
            _repository = repository;
        }

        public Task<List<VetterQuestionViewModel>>
            GetQuestionsAsync(
                long vetterUserId)
        {
            return _repository.GetQuestionsAsync(
                vetterUserId);
        }

        public Task<List<VetterExamSummaryViewModel>>
            GetPendingExamSummaryAsync(
                long vetterUserId)
        {
            return _repository.GetPendingExamSummaryAsync(
                vetterUserId);
        }
    }
}
