using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class TosViewModel
    {
        public long TopicId { get; set; }
        public string TopicName { get; set; }
        public string TopicType { get; set; }
        public int TopicEasyQuestion { get; set; }
        public int TopicEasyMarks { get; set; }
        public int TopicMediumQuestion { get; set; }
        public int TopicMediumMarks { get; set; }
        public int TopicHardQuestion { get; set; }
        public int TopicHardMarks { get; set; }
        public long ExamId { get; set; }
        public string ExamName { get; set; }
        public long PaperId { get; set; }
        public string PaperName { get; set; }
        public long SectionId { get; set; }
        public string SectionName { get; set; }
        public long? SubsectionId { get; set; }
        public string SubsectionName { get; set; }

        public int IsActive { get; set; }
        public int TosStatus { get; set; }
        public int TotalQuestions
        {
            get
            {
                return TopicEasyQuestion
                     + TopicMediumQuestion
                     + TopicHardQuestion;
            }
        }

        public decimal TotalMarks
        {
            get
            {
                return TopicEasyMarks
                     + TopicMediumMarks
                     + TopicHardMarks;
            }
        }

        
    }

}
