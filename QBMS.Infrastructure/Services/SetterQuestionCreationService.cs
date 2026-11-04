using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using QBMS.Application.Interfaces;
using QBMS.Application.Models;

namespace QBMS.Infrastructure.Services
{
   public class SetterQuestionCreationService : ISetterQuestionCreationService
    {
        private readonly ISetterQuestionCreationRepository _repository;

        public SetterQuestionCreationService( ISetterQuestionCreationRepository repository)
        {
            _repository = repository;
        }


        public async Task<CreateQuestionPageViewModel> GetCreatePageAsync( long assignmentId,long setterUserId, long? questionId,bool forceNew)
        {
            var assignment = await _repository.GetAssignmentAsync(assignmentId,setterUserId);
            if (assignment == null)
            {
                return null;
            }

            var topics = await _repository.GetTopicsAsync(assignmentId,setterUserId);
            var quota = await _repository.GetQuotaAsync(assignmentId,setterUserId);
            var model = new CreateQuestionPageViewModel
                {
                    AssignmentId =assignmentId,
                    Topics =topics,
                    QuestionTypes = new List<string>
                        {
                            "OBJ",
                            "SUBJECTIVE"
                        },

                    Difficulties = new List<string>
                        {
                            "EASY",
                            "MEDIUM",
                            "HARD"
                        },
                     Quota = quota
                };


            CreateQuestionViewModel question = null;
            bool resumed = false;


            /* =====================================================
               EXPLICIT QUESTION
               ===================================================== */

            if (!forceNew && questionId.HasValue && questionId.Value > 0)
            {
                question = await _repository.GetQuestionAsync(assignmentId,setterUserId, questionId.Value);
            }


            /* =====================================================
               RESUME LAST DRAFT
               ===================================================== */

            if (!forceNew && question == null)
            {
                var resumeId =await _repository.GetResumeQuestionIdAsync(assignmentId, setterUserId);
                if (resumeId.HasValue)
                {
                    question = await _repository.GetQuestionAsync(assignmentId, setterUserId,resumeId.Value);
                    if (question != null)
                    {
                        resumed = true;
                    }
                }
            }


            /* =====================================================
               NEW QUESTION
               ===================================================== */

            if (question == null)
            {
                question = new CreateQuestionViewModel
                {
                    AssignmentID = assignmentId,
                    MainExamId = assignment.MainExamId,
                    PaperId = assignment.PaperId ?? 0,
                    SectionId = assignment.SectionId ?? 0,
                    SubsectionId = assignment.SubsectionId,
                    TopicId = assignment.TopicId ?? 0
                };
                model.IsNewQuestion = true;

                // Topic and difficulty come from the TOS quota: the first topic (in TOS order) that still
                // needs questions, and its first difficulty with quota left (EASY, MEDIUM, HARD).
                bool slotApplied = false;
                var topicQuotas = await _repository.GetTopicQuotaInfoAsync(assignmentId, setterUserId);
                if (topics != null && topicQuotas != null)
                {
                    foreach (var tq in topicQuotas)
                    {
                        string slotDifficulty = null;
                        if (tq.EasyUsed < Required(tq.EasyQuota, assignment.MultiplicationFactor)) slotDifficulty = "EASY";
                        else if (tq.MediumUsed < Required(tq.MediumQuota, assignment.MultiplicationFactor)) slotDifficulty = "MEDIUM";
                        else if (tq.HardUsed < Required(tq.HardQuota, assignment.MultiplicationFactor)) slotDifficulty = "HARD";
                        if (slotDifficulty == null) continue;

                        var slotTopic = topics.Find(x => x.TopicId == tq.TopicId);
                        if (slotTopic == null) continue;

                        question.MainExamId = slotTopic.MainExamId;
                        question.PaperId = slotTopic.PaperId;
                        question.SectionId = slotTopic.SectionId;
                        question.SubsectionId = slotTopic.SubsectionId;
                        question.TopicId = slotTopic.TopicId;
                        question.ExamName = slotTopic.ExamName;
                        question.PaperName = slotTopic.PaperName;
                        question.SectionName = slotTopic.SectionName;
                        question.SubsectionName = slotTopic.SubsectionName;
                        question.TopicName = slotTopic.TopicName;
                        question.DifficultyLevel = slotDifficulty;
                        slotApplied = true;
                        break;
                    }
                }

                if (!slotApplied && topics!=null && topics.Count>0)
                {
                    if(!assignment.TopicId.HasValue || assignment.TopicId.Value==0)
                    {
                        var firstTopic = topics[0];
                        question.MainExamId = firstTopic.MainExamId;
                        question.PaperId = firstTopic.PaperId;
                        question.SectionId = firstTopic.SectionId;
                        question.SubsectionId = firstTopic.SubsectionId;
                        question.TopicId = firstTopic.TopicId;
                        question.ExamName = firstTopic.ExamName;
                        question.PaperName = firstTopic.PaperName;
                        question.SectionName = firstTopic.SectionName;
                        question.SubsectionName = firstTopic.SubsectionName;
                        question.TopicName = firstTopic.TopicName;
                    }
                    else
                    {
                        var assignedTopic = topics.Find(x => x.TopicId == assignment.TopicId.Value);
                        if (assignedTopic != null)
                        {
                            question.MainExamId = assignedTopic.MainExamId;
                            question.PaperId = assignedTopic.PaperId;
                            question.SectionId = assignedTopic.SectionId;
                            question.SubsectionId = assignedTopic.SubsectionId;
                            question.TopicId = assignedTopic.TopicId;
                            question.ExamName = assignedTopic.ExamName;
                            question.PaperName = assignedTopic.PaperName;
                            question.SectionName = assignedTopic.SectionName;
                            question.SubsectionName = assignedTopic.SubsectionName;
                            question.TopicName = assignedTopic.TopicName;
                        }
                    }
                }
            }
            else
            {
                model.IsNewQuestion =false;
            }
            question.AssignmentID =assignmentId;
            // New question: pre-select the question type of the TOS topic.
            if (string.IsNullOrWhiteSpace(question.QuestionType))
            {
                var typeTopic = topics == null ? null : topics.Find(x => x.TopicId == question.TopicId);
                question.QuestionType = typeTopic != null && !string.IsNullOrWhiteSpace(typeTopic.TopicType) ? typeTopic.TopicType : "OBJ";
            }
            FillTopicNames(question, topics);
            model.Question = question;
            model.IsResumedQuestion = resumed;
            model.TopicQuota = await BuildTopicQuotaAsync(assignmentId, setterUserId, question.TopicId, assignment.MultiplicationFactor);


            /* =====================================================
               NAVIGATION
               ===================================================== */

            long currentQuestionId = question.QuestionId;
            if (currentQuestionId > 0)
            {
                model.PreviousQuestionId = await _repository .GetPreviousQuestionIdAsync( assignmentId,setterUserId,currentQuestionId);
                model.NextQuestionId = await _repository.GetNextQuestionIdAsync(assignmentId, setterUserId, currentQuestionId);
            }
            else
            {
                model.PreviousQuestionId = await _repository.GetPreviousQuestionIdAsync(assignmentId, setterUserId, long.MaxValue);
            }
            model.CanGoPrevious = model.PreviousQuestionId.HasValue;
            model.CanGoNext = model.NextQuestionId.HasValue ||model.Quota.TotalRemaining > 0;
            return model;
        }


        // TOS count x multiplication factor, rounded up (same rule as the templates and the quota).
        private static int Required(int tosCount, decimal factor)
        {
            return (int)Math.Ceiling(Math.Max(0, tosCount) * factor);
        }

        private static void FillTopicNames(CreateQuestionViewModel question, List<CreateQuestionViewModel> topics)
        {
            if (question == null || topics == null || question.TopicId <= 0)
            {
                return;
            }

            var topic = topics.Find(x => x.TopicId == question.TopicId);
            if (topic == null)
            {
                return;
            }

            question.ExamName = topic.ExamName;
            question.PaperName = topic.PaperName;
            question.SectionName = topic.SectionName;
            question.SubsectionName = topic.SubsectionName;
            question.TopicName = topic.TopicName;
            question.TopicType = topic.TopicType;
        }

        private async Task<QuestionQuotaViewModel> BuildTopicQuotaAsync(long assignmentId, long setterUserId, long topicId, decimal factor)
        {
            var infos = await _repository.GetTopicQuotaInfoAsync(assignmentId, setterUserId);
            var info = infos == null ? null : infos.Find(x => x.TopicId == topicId);
            if (info == null)
            {
                return null;
            }

            var quota = new QuestionQuotaViewModel
            {
                EasyAssigned = Required(info.EasyQuota, factor),
                MediumAssigned = Required(info.MediumQuota, factor),
                HardAssigned = Required(info.HardQuota, factor),
                EasyUsed = info.EasyUsed,
                MediumUsed = info.MediumUsed,
                HardUsed = info.HardUsed
            };

            quota.Easyremaining = Math.Max(0, quota.EasyAssigned - quota.EasyUsed);
            quota.Mediumremaining = Math.Max(0, quota.MediumAssigned - quota.MediumUsed);
            quota.Hardremaining = Math.Max(0, quota.HardAssigned - quota.HardUsed);
            return quota;
        }

        public async Task<SaveQuestionResult> SaveQuestionAsync( long assignmentId,long setterUserId,CreateQuestionViewModel question)
        {
            return await _repository.SaveQuestionAsync(assignmentId, setterUserId,question);
        }


        public async Task PreparePostModelAsync(CreateQuestionPageViewModel model,long setterUserId)
        {
            model.QuestionTypes = new List<string>
                {
                    "OBJ",
                    "SUBJECTIVE"
                };

            model.Difficulties =
                new List<string>
                {
                    "EASY",
                    "MEDIUM",
                    "HARD"
                };

            model.Topics = await _repository.GetTopicsAsync( model.AssignmentId,setterUserId);

            // The page shows the hierarchy as text, so the names are not posted back: look them up again.
            if (model.Question != null)
            {
                FillTopicNames(model.Question, model.Topics);

                var assignment = await _repository.GetAssignmentAsync(model.AssignmentId, setterUserId);
                if (assignment != null)
                {
                    model.TopicQuota = await BuildTopicQuotaAsync(model.AssignmentId, setterUserId, model.Question.TopicId, assignment.MultiplicationFactor);
                }
            }
            model.Quota =  await _repository.GetQuotaAsync(model.AssignmentId, setterUserId);
            long questionId =model.Question == null ? 0: model.Question.QuestionId;
            if (questionId > 0)
            {
                model.PreviousQuestionId =await _repository.GetPreviousQuestionIdAsync( model.AssignmentId,setterUserId, questionId);
                model.NextQuestionId = await _repository.GetNextQuestionIdAsync( model.AssignmentId,setterUserId, questionId);
            }
            else
            {
                model.PreviousQuestionId =await _repository.GetPreviousQuestionIdAsync(model.AssignmentId,setterUserId,long.MaxValue);
                model.NextQuestionId = null;
            }
            model.CanGoPrevious = model.PreviousQuestionId.HasValue;
            model.CanGoNext = model.NextQuestionId.HasValue  || model.Quota.TotalRemaining > 0;
        }

        //Task<CreateQuestionPageViewModel> ISetterQuestionCreationService.GetCreatePageAsync(long assignmentId, long setterUserId, long? questionId, bool forceNew)
        //{
        //    throw new NotImplementedException();
        //}

        //Task<SaveQuestionResult> ISetterQuestionCreationService.SaveQuestionAsync(long assignmentId, long setterUserId, CreateQuestionViewModel question)
        //{
        //    throw new NotImplementedException();
        //}

        //Task ISetterQuestionCreationService.PreparePostModelAsync(CreateQuestionPageViewModel model, long setterUserId)
        //{
        //    throw new NotImplementedException();
        //}
    }
}
