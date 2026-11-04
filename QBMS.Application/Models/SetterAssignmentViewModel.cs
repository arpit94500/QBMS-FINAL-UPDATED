using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class SetterAssignmentViewModel
    {
        public long AssignmentId { get; set; }

        public string AssignmentLevel { get; set; }

        public long MainExamId { get; set; }

        public long? PaperId { get; set; }

        public long? SectionId { get; set; }

        public long? SubsectionId { get; set; }

        public long? TopicId { get; set; }

        public string ExamName { get; set; }

        public string PaperName { get; set; }

        public string SectionName { get; set; }

        public string SubsectionName { get; set; }

        public string TopicName { get; set; }

        public decimal MultiplicationFactor { get; set; }

        public bool IsActive { get; set; }

        // Progress of this assignment (filled by the Setter dashboard)
        public int TotalQuota { get; set; }

        public int CreatedQuota { get; set; }

        public int ProgressPercent
        {
            get
            {
                if (TotalQuota <= 0)
                {
                    return 0;
                }

                return (int)Math.Min(100, CreatedQuota * 100L / TotalQuota);
            }
        }
    }

}
