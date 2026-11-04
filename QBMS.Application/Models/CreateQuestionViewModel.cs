using System;
using System.Collections.Generic;
using System.Text;


namespace QBMS.Application.Models
{
    public class CreateQuestionViewModel
    {
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
        public long QuestionId { get; set; }

        public string DifficultyLevel { get; set; }

        public string QuestionType { get; set; }//an
        // Question type of the TOS topic (OBJ or SUBJECTIVE); used to pre-select the type for a new question.
        public string TopicType { get; set; }
        public long AssignmentID { get; set; } //an
        public string Status { get; set; } //an
        public string LatestRemarks { get; set; }//an

        public string QuestionText { get; set; }
        public string Option1 { get; set; }
        public string Option2 { get; set; }
        public string Option3 { get; set; }
        public string Option4 { get; set; }
        public string CorrectAnswer { get; set; }
        public int CreationSequence { get; set; }
        public List<DropdownItem> Difficulties { get; set; }
    }
}
