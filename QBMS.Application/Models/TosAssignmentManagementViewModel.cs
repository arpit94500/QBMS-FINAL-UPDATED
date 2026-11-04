using System;
using System.Collections.Generic;

namespace QBMS.Application.Models
{
    public class TosAssignmentManagementViewModel
    {
        public List<TosAssignmentManagementRow> Assignments { get; set; }
            = new List<TosAssignmentManagementRow>();

        public List<DropdownItem> Setters { get; set; }
            = new List<DropdownItem>();

        public List<DropdownItem> Vetters { get; set; }
            = new List<DropdownItem>();

        public string Search { get; set; }

        // Filters (drop-downs): exam and paper by id.
        public long? ExamId { get; set; }
        public long? PaperId { get; set; }
        public List<DropdownItem> Exams { get; set; }
            = new List<DropdownItem>();
        public List<DropdownItem> Papers { get; set; }
            = new List<DropdownItem>();
    }

    public class TosAssignmentManagementRow
    {
        public long AssignmentId { get; set; }
        public long MainExamId { get; set; }
        public long? PaperId { get; set; }
        public string AssignmentLevel { get; set; }
        public string ExamName { get; set; }
        public string PaperName { get; set; }
        public string SectionName { get; set; }
        public string SubsectionName { get; set; }
        public string TopicName { get; set; }

        public long SetterUserId { get; set; }
        public string SetterName { get; set; }

        public long? VetterUserId { get; set; }
        public string VetterName { get; set; }

        public decimal MultiplicationFactor { get; set; }

        public int TotalQuestions { get; set; }
        public int CompletedQuestions { get; set; }
        public int RemainingQuestions { get; set; }
        // Questions given the final Admin approval (status COMPLETED).
        public int ApprovedQuestions { get; set; }

        public bool IsActive { get; set; }
        public DateTime? AssignedAt { get; set; }
    }

    public class ReassignTosAssignmentViewModel
    {
        public long AssignmentId { get; set; }

        public string AssignmentLevel { get; set; }
        public string ExamName { get; set; }
        public string PaperName { get; set; }
        public string SectionName { get; set; }
        public string SubsectionName { get; set; }
        public string TopicName { get; set; }
        
        public long CurrentSetterUserId { get; set; }
        public long? CurrentVetterUserId { get; set; }

        public long NewSetterUserId { get; set; }
        public long NewVetterUserId { get; set; }

        public bool TransferUnfinishedQuestions { get; set; } = true;

        public List<DropdownItem> Setters { get; set; }
            = new List<DropdownItem>();

        public List<DropdownItem> Vetters { get; set; }
            = new List<DropdownItem>();
    }
}
