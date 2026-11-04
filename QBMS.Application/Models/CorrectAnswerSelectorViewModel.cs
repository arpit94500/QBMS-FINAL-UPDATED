namespace QBMS.Application.Models
{
    public class CorrectAnswerSelectorViewModel
    {
        // 0 = nothing selected, 1-4 = Option 1-4
        public int SelectedOption { get; set; }

        // Existing stored answer; kept so an old answer is not lost
        // if the user does not pick an option (e.g. Vetter REJECT).
        public string LegacyContent { get; set; }

        // id of the (hidden) element the editor scripts read the answer from
        public string SyncEditorId { get; set; }

        // id of the hidden input that is posted to the controller
        public string HiddenFieldId { get; set; }

        // comma separated ids of the Option 1..4 editors
        public string OptionEditorIds { get; set; }
    }
}
