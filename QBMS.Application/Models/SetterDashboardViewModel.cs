using System.Collections.Generic;

namespace QBMS.Application.Models
{
    public class SetterDashboardViewModel
    {
        public List<SetterAssignmentViewModel>
            Assignments
        { get; set; }

        public List<SetterQuestionViewModel>
            Questions
        { get; set; }

        public List<SetterAssignmentViewModel>
            CompletedAssignments
        { get; set; }
        // Word / Excel uploads and their duplicate-check progress
        public List<UploadBatchViewModel>
            UploadBatches
        { get; set; }

        public SetterDashboardViewModel()
        {
            UploadBatches = new List<UploadBatchViewModel>();
            Assignments = new List<SetterAssignmentViewModel>();
            Questions = new List<SetterQuestionViewModel>();
            CompletedAssignments = new List<SetterAssignmentViewModel>();
        }
    }
}
