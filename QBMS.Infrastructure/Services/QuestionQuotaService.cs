using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class QuestionQuotaService
        : IQuestionQuotaService
    {
        private readonly IQuestionQuotaRepository
            _repository;

        public QuestionQuotaService(
            IQuestionQuotaRepository repository)
        {
            _repository = repository;
        }

        public Task<QuestionQuotaViewModel>
            GetQuotaAsync(
                long assignmentId,
                long setterUserId)
        {
            return _repository.GetQuotaAsync(
                assignmentId,
                setterUserId);
        }
    }
}
