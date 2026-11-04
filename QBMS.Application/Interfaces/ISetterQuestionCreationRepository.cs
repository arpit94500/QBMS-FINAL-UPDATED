using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using QBMS.Application.Models;


namespace QBMS.Application.Interfaces
{
    public interface ISetterQuestionCreationRepository
    {
        Task<SetterAssignmentInfo> GetAssignmentAsync( 
            long assignmentId,
            long setterUserId);

        Task<List<CreateQuestionViewModel>> GetTopicsAsync(
            long assignmentId,
            long setterUserId);

        Task<CreateQuestionViewModel> GetQuestionAsync(
            long assignmentId,
            long setterUserId,
            long questionId);

        Task<long?> GetResumeQuestionIdAsync(
            long assignmentId,
            long setterUserId);

        Task<long?> GetPreviousQuestionIdAsync(
            long assignmentId,
            long setterUserId,
            long questionId);

        Task<long?> GetNextQuestionIdAsync(
            long assignmentId,
            long setterUserId,
            long questionId);

        Task<QuestionQuotaViewModel> GetQuotaAsync(
            long assignmentId,
            long setterUserId);

        Task<SaveQuestionResult> SaveQuestionAsync(
            long assignmentId,
            long setterUserId,
            CreateQuestionViewModel question);
       
        Task<List<TopicQuotaInfo>> GetTopicQuotaInfoAsync(
        long assignmentId,
        long setterUserId);

        Task<QuestionCreationSlot> GetNextCreationSlotAsync(
                long assignmentId,
                long setterUserId);

        Task<CreateQuestionViewModel> GetQuestionBySequenceAsync(
                long assignmentId,
                long setterUserId,
                int sequence);

        Task<int> GetNextCreationSequenceAsync(
                long assignmentId,
                long setterUserId);


    }
}
