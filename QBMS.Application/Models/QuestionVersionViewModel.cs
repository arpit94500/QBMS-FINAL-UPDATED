using System;

namespace QBMS.Application.Models
{
    public class QuestionVersionViewModel
    {
        public long VersionId { get; set; }

        public long QuestionId { get; set; }

        public int VersionNo { get; set; }

        public string QuestionText { get; set; }

        public string QuestionType { get; set; }

        public string DifficultyLevel { get; set; }

        public long ChangedBy { get; set; }

        public string ChangeReason { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
