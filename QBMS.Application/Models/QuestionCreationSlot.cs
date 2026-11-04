using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class QuestionCreationSlot
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

        public string QuestionType { get; set; }

        public string DifficultyLevel { get; set; }

        public int CreationSequence { get; set; }
    }

}
