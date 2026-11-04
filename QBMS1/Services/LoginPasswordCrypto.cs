using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace QBMS1.Services
{
    /// <summary>
    /// Lets the login page send the password encrypted instead of as plain text.
    ///
    /// How it works:
    ///  1. The server creates one RSA-2048 key pair when the application starts (the private key never leaves the server).
    ///  2. Each time the login page is shown it receives the PUBLIC key and a one-time challenge (nonce).
    ///  3. The browser encrypts {"p": password, "n": nonce} with RSA-OAEP (SHA-256) and posts only the encrypted text.
    ///  4. The server decrypts it, checks that the nonce was issued by it and not used before (so a captured
    ///     login request cannot be replayed), and then validates the password exactly as before.
    ///
    /// Note: this protects the password field in the request body. It does not replace HTTPS.
    /// </summary>
    public interface ILoginPasswordCrypto
    {
        /// <summary>Public key as Base64 SubjectPublicKeyInfo (what WebCrypto "spki" import expects).</summary>
        string PublicKeyBase64 { get; }

        /// <summary>Creates a one-time challenge for a login page.</summary>
        string IssueChallenge();

        /// <summary>Decrypts the posted value. Returns false if it is invalid, expired or was already used.</summary>
        bool TryDecrypt(string encryptedBase64, out string password);
    }

    public sealed class LoginPasswordCrypto : ILoginPasswordCrypto, IDisposable
    {
        private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(10);
        private const string CachePrefix = "login-challenge:";

        private readonly RSA _rsa;
        private readonly IMemoryCache _cache;
        private readonly string _publicKey;

        public LoginPasswordCrypto(IMemoryCache cache)
        {
            _cache = cache;
            _rsa = RSA.Create(2048);
            _publicKey = Convert.ToBase64String(_rsa.ExportSubjectPublicKeyInfo());
        }

        public string PublicKeyBase64
        {
            get { return _publicKey; }
        }

        public string IssueChallenge()
        {
            var bytes = new byte[24];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(bytes);
            }

            var nonce = Convert.ToBase64String(bytes);
            _cache.Set(CachePrefix + nonce, true, ChallengeLifetime);
            return nonce;
        }

        public bool TryDecrypt(string encryptedBase64, out string password)
        {
            password = null;

            if (string.IsNullOrWhiteSpace(encryptedBase64))
            {
                return false;
            }

            try
            {
                var cipher = Convert.FromBase64String(encryptedBase64);
                var plain = _rsa.Decrypt(cipher, RSAEncryptionPadding.OaepSHA256);

                string nonce;
                string value;

                using (var doc = JsonDocument.Parse(Encoding.UTF8.GetString(plain)))
                {
                    nonce = doc.RootElement.GetProperty("n").GetString();
                    value = doc.RootElement.GetProperty("p").GetString();
                }

                if (string.IsNullOrEmpty(nonce) || string.IsNullOrEmpty(value))
                {
                    return false;
                }

                var key = CachePrefix + nonce;

                // A challenge can be used once only.
                if (!_cache.TryGetValue(key, out object _))
                {
                    return false;
                }

                _cache.Remove(key);
                password = value;
                return true;
            }
            catch (Exception)
            {
                // bad Base64, wrong key (e.g. the application restarted), malformed JSON ...
                return false;
            }
        }

        public void Dispose()
        {
            _rsa.Dispose();
        }
    }
}
