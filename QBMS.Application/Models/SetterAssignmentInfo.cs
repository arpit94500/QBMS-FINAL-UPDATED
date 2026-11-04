using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
   public class SetterAssignmentInfo
    {
        public long AssignmentId { get; set; }

        public string AssignmentLevel { get; set; }

        public long MainExamId { get; set; }

        public long? PaperId { get; set; }

        public long? SectionId { get; set; }

        public long? SubsectionId { get; set; }

        public long? TopicId { get; set; }

        public decimal MultiplicationFactor { get; set; }

    }
}
