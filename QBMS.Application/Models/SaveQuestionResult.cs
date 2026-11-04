using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class SaveQuestionResult
    {
        public bool Success { get; set; }

        public long QuestionId { get; set; }

        public string ErrorMessage { get; set; }

        public QuestionQuotaViewModel Quota { get; set; }

    }
}
