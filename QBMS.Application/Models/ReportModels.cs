using System;
using System.Collections.Generic;

namespace QBMS.Application.Models
{
    // One row per assignment: the raw numbers every report is built from.
    public class AssignmentProgressRow
    {
        public long AssignmentId { get; set; }
        public long SetterUserId { get; set; }
        public string SetterName { get; set; }
        public long? VetterUserId { get; set; }
        public string VetterName { get; set; }
        public long ExamId { get; set; }
        public string ExamName { get; set; }
        public int Target { get; set; }          // from the TOS: ceil(sum x factor) per difficulty
        public int Created { get; set; }         // questions created, capped per difficulty at the target
        public int Draft { get; set; }
        public int Submitted { get; set; }       // SUBMITTED_TO_VETTER (waiting for the Vetter)
        public int VetterApproved { get; set; }  // VETTER_APPROVED (waiting for the Admin)
        public int Rejected { get; set; }        // VETTER_REJECTED + ADMIN_REJECTED (back with the Setter)
        public int Completed { get; set; }       // COMPLETED (final Admin approval)
    }

    // One row per assignment AND paper it covers (used by the paper-wise report).
    public class PaperProgressRow : AssignmentProgressRow
    {
        public long PaperId { get; set; }
        public string PaperName { get; set; }
    }

    public class ProgressSummary
    {
        public long Key { get; set; }
        public string Name { get; set; }
        public int Assignments { get; set; }
        public int Target { get; set; }
        public int Created { get; set; }
        public int Draft { get; set; }
        public int Submitted { get; set; }
        public int VetterApproved { get; set; }
        public int Rejected { get; set; }
        public int Completed { get; set; }

        public int Remaining { get { return Math.Max(0, Target - Created); } }
        public int CreatedPercent { get { return Target <= 0 ? 0 : (int)Math.Min(100, Created * 100L / Target); } }
        public int CompletedPercent { get { return Target <= 0 ? 0 : (int)Math.Min(100, Completed * 100L / Target); } }
        // Vetter view of the same numbers
        public int PendingReview { get { return Submitted; } }
        public int Reviewed { get { return VetterApproved + Completed + Rejected; } }
    }

    public class ReportsViewModel
    {
        public long? ExamId { get; set; }
        public List<DropdownItem> Exams { get; set; } = new List<DropdownItem>();
        public List<ProgressSummary> Setters { get; set; } = new List<ProgressSummary>();
        public List<ProgressSummary> Vetters { get; set; } = new List<ProgressSummary>();
        public List<ProgressSummary> PaperWise { get; set; } = new List<ProgressSummary>();
        public ProgressSummary Total { get; set; } = new ProgressSummary { Name = "All" };
    }
}
