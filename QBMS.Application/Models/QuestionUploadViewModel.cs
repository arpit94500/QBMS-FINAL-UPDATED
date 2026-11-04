using System.Collections.Generic;
//using Microsoft.AspNetCore.Http;

namespace QBMS.Application.Models
{
    public class QuestionUploadViewModel
    {
        public long AssignmentId { get; set; }

        //public IFormFile File { get; set; }

        public string FileName { get; set; }

        public List<UploadedQuestionModel> Questions { get; set; }

        public List<string> Errors { get; set; }

        public int TotalRows { get; set; }

        public int ValidRows { get; set; }

        public int InvalidRows { get; set; }

        public QuestionUploadViewModel()
        {
            Questions = new List<UploadedQuestionModel>();
            Errors = new List<string>();
        }
    }
}
