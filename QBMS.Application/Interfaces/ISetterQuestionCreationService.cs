using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using QBMS.Application.Models;


namespace QBMS.Application.Interfaces
{
   public interface ISetterQuestionCreationService
    {
        Task<CreateQuestionPageViewModel> GetCreatePageAsync( long assignmentId, long setterUserId, long? questionId, bool forceNew);

        Task<SaveQuestionResult> SaveQuestionAsync(long assignmentId,long setterUserId, CreateQuestionViewModel question);

        Task PreparePostModelAsync(CreateQuestionPageViewModel model,long setterUserId);

    }
}
