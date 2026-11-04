using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;


namespace QBMS.Infrastructure.Services
{
    public class EncryptionEnhanced
    {
        private const string ENCRYPTION_KEY = "MAKV2SPBNI99212";
        private static readonly byte[] ENCRYPTION_SALT = new byte[] { 0x49, 0x76, 0x61, 0x6e, 0x20, 0x4d, 0x65, 0x64, 0x76, 0x65, 0x64, 0x65, 0x76 };

        public static string Encrypt(string clearText)
        {
            if (string.IsNullOrEmpty(clearText))
                return "";
            try {
                byte[] clearBytes = Encoding.Unicode.GetBytes(clearText.Trim());
                using (Aes encryptor =Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(ENCRYPTION_KEY, ENCRYPTION_SALT);
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms=new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                            cs.Close();
                        }
                        return Convert.ToBase64String(ms.ToArray()).Trim();
                    }
                }
            } catch (Exception ex) {
                throw new Exception($"Encryption Failed : {ex.Message}", ex);
            }
        }
        public static string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return "";
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText.Replace(" ", "+"));
                using (Aes encryptor=Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(ENCRYPTION_KEY, ENCRYPTION_SALT);
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        return Encoding.Unicode.GetString(ms.ToArray());
                    }

                }
            }catch(Exception ex)
            {
                throw new Exception($"Decryption Failed : {ex.Message}", ex);
            }
        }
        public static string EncryptWithCustomKey(string clearText,string key)
        {
            if (string.IsNullOrEmpty(clearText) || string.IsNullOrEmpty(key))
                return "";
            try
            {
                byte[] clearBytes = Encoding.Unicode.GetBytes(clearText.Trim());
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(key, ENCRYPTION_SALT);
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateEncryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(clearBytes, 0, clearBytes.Length);
                            cs.Close();
                        }
                        return Convert.ToBase64String(ms.ToArray());
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Encryption Failed with custom key : {ex.Message}", ex);
            }
        }

        public static string DecryptWithCustomKey(string cipherText, string key)
        {
            if (string.IsNullOrEmpty(cipherText))
                return "";
            try
            {
                byte[] cipherBytes = Convert.FromBase64String(cipherText.Replace(" ", "+"));
                using (Aes encryptor = Aes.Create())
                {
                    Rfc2898DeriveBytes pdb = new Rfc2898DeriveBytes(key, ENCRYPTION_SALT);
                    encryptor.Key = pdb.GetBytes(32);
                    encryptor.IV = pdb.GetBytes(16);
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (CryptoStream cs = new CryptoStream(ms, encryptor.CreateDecryptor(), CryptoStreamMode.Write))
                        {
                            cs.Write(cipherBytes, 0, cipherBytes.Length);
                            cs.Close();
                        }
                        return Encoding.Unicode.GetString(ms.ToArray());
                    }

                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Decryption Failed with custom key : {ex.Message}", ex);
            }
        }
    }
}
