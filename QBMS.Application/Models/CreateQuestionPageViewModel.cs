using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class CreateQuestionPageViewModel
    {
        public long AssignmentId { get; set; }

        public List<CreateQuestionViewModel> Topics { get; set; }

        public CreateQuestionViewModel Question { get; set; }

        public List<string> QuestionTypes { get; set; }

        public List<string> Difficulties { get; set; }
        public QuestionQuotaViewModel Quota{ get; set; }

        // Quota of the topic that is currently selected (TOS count x multiplication factor, rounded up)
        public QuestionQuotaViewModel TopicQuota { get; set; }

        public long? PreviousQuestionId { get; set; }      //an

        public long? NextQuestionId { get; set; }       //an

        public bool CanGoPrevious { get; set; }  //an

        public bool CanGoNext { get; set; }   //an

        public bool IsNewQuestion { get; set; }   //an

        public bool IsResumedQuestion { get; set; }  //an

        public string NavigationAction { get; set; }   //an
        public int CurrentCreationSequence { get; set; }

        public string CurrentDifficulty { get; set; }

        public string CurrentQuestionType { get; set; }
        // Existing questions that are at or above the duplicate limit (shown when a save is blocked).
        public List<DuplicateMatchModel> DuplicateMatches { get; set; } = new List<DuplicateMatchModel>();
        // True when the Python duplicate API could not be reached while saving.
        public bool DuplicateCheckUnavailable { get; set; }



    }
}

