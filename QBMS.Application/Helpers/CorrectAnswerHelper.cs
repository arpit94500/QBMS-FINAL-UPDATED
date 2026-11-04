using System;
using System.Text.RegularExpressions;

namespace QBMS.Application.Helpers
{
    public static class CorrectAnswerHelper
    {
        // Returns 1-4 when the stored correct answer is the same as
        // Option 1-4 (or is stored as "Option N" / "N"); 0 when it
        // cannot be matched (new question, or an old free-text answer).
        public static int GetSelectedOption(
            string correctAnswer,
            string option1,
            string option2,
            string option3,
            string option4)
        {
            if (string.IsNullOrWhiteSpace(correctAnswer))
            {
                return 0;
            }

            var label = Regex.Match(
                correctAnswer.Trim(),
                @"^(?:option\s*)?([1-4])$",
                RegexOptions.IgnoreCase);

            if (label.Success)
            {
                return int.Parse(label.Groups[1].Value);
            }

            var target = Normalize(correctAnswer);

            var options = new[] { option1, option2, option3, option4 };

            for (int i = 0; i < options.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(options[i]) &&
                    Normalize(options[i]) == target)
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static string Normalize(string html)
        {
            if (html == null)
            {
                return string.Empty;
            }

            return Regex.Replace(
                html.Replace("\u200B", string.Empty)
                    .Replace("&nbsp;", " "),
                @"\s+",
                " ").Trim();
        }
    }
}
