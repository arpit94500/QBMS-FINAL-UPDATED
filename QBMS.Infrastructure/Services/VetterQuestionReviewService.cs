using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
    public class VetterQuestionReviewService
        : IVetterQuestionReviewService
    {
        private readonly IVetterQuestionReviewRepository
            _repository;

        public VetterQuestionReviewService(
            IVetterQuestionReviewRepository repository)
        {
            _repository = repository;
        }

        public async Task<VetterQuestionReviewViewModel>
            GetReviewQuestionAsync(
                long vetterUserId,
                long? questionId)
        {
            long? targetQuestionId = questionId;

            bool resumed = false;

            // No ID means opening review module.
            // First try resume.
            if (!targetQuestionId.HasValue)
            {
                targetQuestionId =
                    await _repository
                        .GetResumeQuestionIdAsync(
                            vetterUserId);

                if (targetQuestionId.HasValue)
                {
                    resumed = true;
                }
            }

            // No saved progress -> first pending.
            if (!targetQuestionId.HasValue)
            {
                var firstQuestion =
                    await _repository
                        .GetFirstQuestionAsync(
                            vetterUserId);

                if (firstQuestion == null)
                    return null;

                targetQuestionId =
                    firstQuestion.QuestionId;
            }

            var question =
                await _repository.GetQuestionAsync(
                    vetterUserId,
                    targetQuestionId.Value);

            // Saved question may have become invalid
            // between queries.
            if (question == null)
            {
                var firstQuestion =
                    await _repository
                        .GetFirstQuestionAsync(
                            vetterUserId);

                if (firstQuestion == null)
                    return null;

                question = firstQuestion;
            }

            question.PreviousQuestionId =
                await _repository
                    .GetPreviousQuestionIdAsync(
                        vetterUserId,
                        question.QuestionId);

            question.NextQuestionId =
                await _repository
                    .GetNextQuestionIdAsync(
                        vetterUserId,
                        question.QuestionId);

            question.CanGoPrevious =
                question.PreviousQuestionId.HasValue;

            question.CanGoNext =
                question.NextQuestionId.HasValue;

            question.IsResumedQuestion =
                resumed;

            // Save current location.
            await _repository.SaveProgressAsync(
                vetterUserId,
                question.QuestionId);

            return question;
        }

        public async Task<bool>
            SaveProgressAsync(
                long vetterUserId,
                long questionId)
        {
            await _repository.SaveProgressAsync(
                vetterUserId,
                questionId);

            return true;
        }

        public async Task<bool>
            ApproveQuestionAsync(
                long vetterUserId,
                long questionId,
                string remarks)
        {
            return await _repository
                .ApproveQuestionAsync(
                    vetterUserId,
                    questionId,
                    remarks);
        }

        public async Task<bool>
            RejectQuestionAsync(
                long vetterUserId,
                long questionId,
                string remarks)
        {
            return await _repository
                .RejectQuestionAsync(
                    vetterUserId,
                    questionId,
                    remarks);
        }

        public Task<bool> RejectedQuestionAsync(long vetterUserId, long questionId, string remarks)
        {
            throw new System.NotImplementedException();
        }
        public async Task<bool> SaveQuestionReviewAsync(long vetterUserId, long questionId, string questionText, string option1, string option2, string option3,
            string option4, string correctAnswer, string remarks, string action)
        {
            return await _repository.SaveQuestionReviewAsync(vetterUserId,questionId,questionText,option1,option2,option3,option4,correctAnswer,remarks,action);
        }
        public async Task<long?> GetPreviousQuestionIdAsync(long vetterUserId,long questionId)
        {
            return await _repository.GetPreviousQuestionIdAsync(vetterUserId, questionId);
        }
        public async Task<long?> GetNextQuestionIdAsync(long vetterUserId,long questionId)
        {
            return await _repository.GetNextQuestionIdAsync(vetterUserId, questionId);
        }

        //public async Task<bool> SaveProgressAsync(long vetterUserId, long questionId)
        //{
        //    return await _repository.SaveProgressAsync(vetterUserId, questionId);
            
        //}
    }
}
