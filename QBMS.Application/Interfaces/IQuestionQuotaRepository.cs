using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace QBMS.Application.Interfaces
{
    public interface IQuestionQuotaRepository
    {
        Task<QuestionQuotaViewModel> GetQuotaAsync(long assignmentId, long setterUserId);
    }
}
