using System;
using System.Security.Cryptography;
using System.Text;

namespace QBMS.Infrastructure.Services
{
    /// <summary>
    /// PBKDF2 password hashing. Stored format:
    ///   PBKDF2$SHA256$&lt;iterations&gt;$&lt;saltBase64&gt;$&lt;hashBase64&gt;
    /// Values that do not start with "PBKDF2$" are treated as legacy plain-text
    /// passwords (how the app stored them before) so existing users can still
    /// log in; they are re-hashed automatically on their next successful login.
    /// </summary>
    public static class PasswordHashHelper
    {
        private const string Prefix = "PBKDF2";
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int Iterations = 210000;

        public static string Hash(string password)
        {
            if (password == null)
            {
                throw new ArgumentNullException(nameof(password));
            }

            var salt = new byte[SaltSize];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            var key = Derive(password, salt, Iterations);

            return Prefix + "$SHA256$" + Iterations + "$"
                + Convert.ToBase64String(salt) + "$"
                + Convert.ToBase64String(key);
        }

        /// <summary>
        /// Verifies a password against a stored value (hashed or legacy plain text).
        /// needsUpgrade is true when the stored value is legacy plain text.
        /// </summary>
        public static bool Verify(string password, string stored, out bool needsUpgrade)
        {
            needsUpgrade = false;

            if (password == null || string.IsNullOrEmpty(stored))
            {
                return false;
            }

            if (!stored.StartsWith(Prefix + "$", StringComparison.Ordinal))
            {
                // Legacy plain-text value.
                var match = CryptographicOperations.FixedTimeEquals(
                    Encoding.UTF8.GetBytes(password),
                    Encoding.UTF8.GetBytes(stored));

                needsUpgrade = match;
                return match;
            }

            var parts = stored.Split('$');
            if (parts.Length != 5 || parts[1] != "SHA256")
            {
                return false;
            }

            if (!int.TryParse(parts[2], out var iterations) || iterations < 1)
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[3]);
                expected = Convert.FromBase64String(parts[4]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Derive(password, salt, iterations, expected.Length);

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        private static byte[] Derive(string password, byte[] salt, int iterations, int keySize = KeySize)
        {
            using (var pbkdf2 = new Rfc2898DeriveBytes(
                password, salt, iterations, HashAlgorithmName.SHA256))
            {
                return pbkdf2.GetBytes(keySize);
            }
        }
    }
}
