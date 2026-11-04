using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class TopicQuotaInfo
    {
        public long TopicId { get; set; }

        public long MainExamId { get; set; }

        public long PaperId { get; set; }

        public long SectionId { get; set; }

        public long? SubsectionId { get; set; }

        public string ExamName { get; set; }

        public string PaperName { get; set; }

        public string SectionName { get; set; }

        public string SubsectionName { get; set; }

        public string TopicName { get; set; }

        public string QuestionType { get; set; }

        public int EasyQuota { get; set; }

        public int MediumQuota { get; set; }

        public int HardQuota { get; set; }

        public int EasyUsed { get; set; }

        public int MediumUsed { get; set; }

        public int HardUsed { get; set; }
    }

}
