namespace QBMS.Application.Models
{
    public class UploadedQuestionModel
    {
        public int RowNumber { get; set; }

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

        public string QuestionText { get; set; }

        // Question as HTML (pictures and LaTeX equations) for the validation table; built by the server only.
        public string QuestionHtml { get; set; }

        public string QuestionType { get; set; }

        public string DifficultyLevel { get; set; }

        public bool IsValid { get; set; }

        public string ErrorMessage { get; set; }
    }
}
