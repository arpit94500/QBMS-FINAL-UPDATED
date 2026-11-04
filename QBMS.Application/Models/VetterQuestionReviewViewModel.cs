using System;

namespace QBMS.Application.Models
{
    public class VetterQuestionReviewViewModel
    {
        public long QuestionId { get; set; }

        public long AssignmentId { get; set; }

        public long MainExamId { get; set; }
        public long PaperId { get; set; }
        public long SectionId { get; set; }
        public long? SubsectionId { get; set; }
        public long TopicId { get; set; }

        public string ExamName { get; set; }
        public string PaperName { get; set; }
        public string SectionName { get; set; }
        public string SubsectionName { get; set; }
        public string TopicName { get; set; }

        public string QuestionType { get; set; }
        public string DifficultyLevel { get; set; }

        public string QuestionText { get; set; }

        public string Option1 { get; set; }
        public string Option2 { get; set; }
        public string Option3 { get; set; }
        public string Option4 { get; set; }
        public string CorrectAnswer { get; set; }

        public string Status { get; set; }

        public string LatestRemarks { get; set; }

        // Vetter's current remarks
        public string Remarks { get; set; }

        public long? PreviousQuestionId { get; set; }
        public long? NextQuestionId { get; set; }

        public bool CanGoPrevious { get; set; }
        public bool CanGoNext { get; set; }

        public bool IsResumedQuestion { get; set; }
    }
}
