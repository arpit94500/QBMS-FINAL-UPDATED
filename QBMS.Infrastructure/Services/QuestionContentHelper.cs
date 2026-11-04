using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;

namespace QBMS.Infrastructure.Services
{
    /// <summary>
    /// Questions are stored as HTML (rich text, optional embedded data-URI pictures).
    /// The duplicate check works on plain text plus the pictures, so split them here.
    /// </summary>
    public static class QuestionContentHelper
    {
        private static readonly Regex ImageRegex = new Regex(
            "<img[^>]*?src\\s*=\\s*[\"']data:image/[a-zA-Z0-9.+-]+;base64,([A-Za-z0-9+/=\\s]+)[\"'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex BreakRegex = new Regex("<br\\s*/?>|</p>|</div>|</li>", RegexOptions.IgnoreCase);
        private static readonly Regex TagRegex = new Regex("<[^>]+>", RegexOptions.Singleline);
        private static readonly Regex SpaceRegex = new Regex("\\s+");

        // Returns the plain text; base64 pictures (max 3) are added to images.
        public static string Extract(string html, List<string> images)
        {
            if (string.IsNullOrEmpty(html)) return "";

            foreach (Match m in ImageRegex.Matches(html))
            {
                if (images != null && images.Count < 3)
                {
                    images.Add(Regex.Replace(m.Groups[1].Value, "\\s", ""));
                }
            }

            var text = ImageRegex.Replace(html, " ");
            text = BreakRegex.Replace(text, " ");
            text = TagRegex.Replace(text, " ");
            text = WebUtility.HtmlDecode(text);
            return SpaceRegex.Replace(text, " ").Trim();
        }

        public static string ToPreview(string html, int maxLength)
        {
            var text = Extract(html, null);
            return text.Length <= maxLength ? text : text.Substring(0, maxLength) + "...";
        }
    }
}
