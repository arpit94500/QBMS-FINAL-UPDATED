using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class TosAssignmentRequest
    {
        public long? ExamId { get; set; }
        public long? PaperId { get; set; }
        public long? SectionId { get; set; }
        public long? SubsectionId { get; set; }
        public long? TopicId { get; set; }
        public string AssignmentLevel { get; set; }
        public long SetterUserId { get; set; }
        public long VetterUserId { get; set; }
        public decimal MultiplicationFactor { get; set; }
    }
}
