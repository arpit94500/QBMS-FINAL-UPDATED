using System;
using System.Linq;

namespace QBMS.Application.Helpers
{
    public static class PasswordPolicy
    {
        public const int MinLength = 8;
        public const int MaxLength = 64;

        public const string RulesText =
            "At least 8 characters with an uppercase letter, a lowercase letter, " +
            "a digit and a special character. It must differ from the default and current password.";

        /// <summary>Returns an error message, or null when the password is acceptable.</summary>
        public static string Validate(string newPassword, string currentPassword, string defaultPassword)
        {
            if (string.IsNullOrEmpty(newPassword))
                return "New password is required.";

            if (newPassword.Length < MinLength)
                return "New password must be at least " + MinLength + " characters long.";

            if (newPassword.Length > MaxLength)
                return "New password must not exceed " + MaxLength + " characters.";

            if (!newPassword.Any(char.IsUpper))
                return "New password must contain at least one uppercase letter.";

            if (!newPassword.Any(char.IsLower))
                return "New password must contain at least one lowercase letter.";

            if (!newPassword.Any(char.IsDigit))
                return "New password must contain at least one digit.";

            if (!newPassword.Any(c => char.IsPunctuation(c) || char.IsSymbol(c)))
                return "New password must contain at least one special character.";

            if (!string.IsNullOrEmpty(defaultPassword) &&
                string.Equals(newPassword, defaultPassword, StringComparison.OrdinalIgnoreCase))
                return "New password cannot be the default password.";

            if (newPassword == currentPassword)
                return "New password must be different from the current password.";

            return null;
        }
    }
}
