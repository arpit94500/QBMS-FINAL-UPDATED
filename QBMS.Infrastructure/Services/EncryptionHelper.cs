using QBMS.Application.Interfaces;
using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Data;
using Newtonsoft.Json;
using System.Linq;

namespace QBMS.Infrastructure.Services
{
    public class EncryptionHelper: IEncryptionHelper
    {
        private const int ENCRYPTION_VERSION = 1;
        private const string ALGORITHM = "AES-256";
        private const int KEY_LENGTH = 256;

        public DataTable DecryptDataTable(DataTable dt, List<string> columnsToDecrypt)
        {
            if (dt == null || dt.Rows.Count == 0 || columnsToDecrypt == null)
                return dt;
            DataTable decryptedDt = dt.Copy();
            foreach (DataRow row in decryptedDt.Rows)
            {
                foreach (string columnName in columnsToDecrypt)
                {
                    if (decryptedDt.Columns.Contains(columnName) && row[columnName] != null)
                    {
                        row[columnName] = DecryptField(row[columnName].ToString());
                    }
                }

            }
            return decryptedDt;
        }

        public string DecryptField(string fieldValue)
        {
            if (String.IsNullOrEmpty(fieldValue))
                return fieldValue;
            try
            {
                return EncryptionEnhanced.Decrypt(fieldValue);
            }
            catch (Exception ex)
            {
                throw new Exception($"Field decryption failed : {ex.Message}", ex);
            }
        }

        public Dictionary<string, string> DecryptFields(Dictionary<string, string> fieldsToEncrypt)
        {
            if (fieldsToEncrypt == null || fieldsToEncrypt.Count == 0)
                return fieldsToEncrypt;
            var encryptedFields = new Dictionary<string, string>();
            foreach (var f in fieldsToEncrypt)
            {
                encryptedFields[f.Key] = EncryptField(f.Value);
            }
            return encryptedFields;
        }

        public dynamic DecryptQuestionData(string encryptedData)
        {
            if (string.IsNullOrEmpty(encryptedData))
                throw new ArgumentNullException(nameof(encryptedData));
            try
            {
                var encryptedPackage = JsonConvert.DeserializeObject<dynamic>(encryptedData);
                string data = encryptedPackage["Data"].ToString();
                string storedChecksum = encryptedPackage["Checksum"].ToString();
                if (!ValidateChecksum(data, storedChecksum))
                    throw new Exception("Checksum Validation Failed");
                string decryptedJson = DecryptField(data);
                return JsonConvert.DeserializeObject<dynamic>(decryptedJson);
            }
            catch (Exception ex) {
                throw new Exception($"Question Data decryption failed: { ex.Message }", ex);
            }
        }

        public DataTable EncryptDataTable(DataTable dt, List<string> columnsToEncrypt)
        {
            if (dt == null || dt.Rows.Count == 0 || columnsToEncrypt==null)
                return dt;
            DataTable encryptedDt = dt.Copy();
            foreach (DataRow row in encryptedDt.Rows)
            {
                foreach(string columnName in columnsToEncrypt)
                {
                    if(encryptedDt.Columns.Contains(columnName) && row[columnName]!= null)
                    {
                        row[columnName] = EncryptField(row[columnName].ToString());
                    }
                }
                
            }
            return encryptedDt;
        }

        public string EncryptField(string fieldValue)
        {
            if (String.IsNullOrEmpty(fieldValue))
                return fieldValue;
            try
            {
                return EncryptionEnhanced.Encrypt(fieldValue);
            }
            catch(Exception ex)
            {
                throw new Exception($"Field encryption failed : {ex.Message}", ex);
            }
        }

        public Dictionary<string, string> EncryptFields(Dictionary<string, string> fieldsToEncrypt)
        {
            if (fieldsToEncrypt==null || fieldsToEncrypt.Count==0)
                return fieldsToEncrypt;
            var encryptedFields = new Dictionary<string, string>();
            foreach (var f in fieldsToEncrypt)
            {
                encryptedFields[f.Key] = EncryptField(f.Value);
            }
            return encryptedFields;
        }

        public string EncryptQuestionData(dynamic questionObject)
        {
            if (questionObject == null)
                throw new ArgumentNullException(nameof(questionObject));
            try
            {
                string jsonData = JsonConvert.SerializeObject(questionObject);
                string encryptedData = EncryptField(jsonData);
                var encryptedPackage = new
                {
                    Version = ENCRYPTION_VERSION,
                    Algorithm = ALGORITHM,
                    KeyLength = KEY_LENGTH,
                    EncryptedAt = DateTime.UtcNow,
                    Data = encryptedData,
                    Checksum = GenerateChecksum(encryptedData)
                };
                return JsonConvert.SerializeObject(encryptedPackage);
            }catch(Exception ex)
            {
                throw new Exception($"Question data encryption failed : {ex.Message}", ex);
            }
        }

        public EncryptionMetadata GetEncryptionMetadata()
        {
            return new EncryptionMetadata
            {
                Version = ENCRYPTION_VERSION,
                Algorithm = ALGORITHM,
                KeyLength = KEY_LENGTH,
                EncryptedAt = DateTime.UtcNow
            };
        }
        private string GenerateChecksum(string data)
        {
            using (var sha256=System.Security.Cryptography.SHA256.Create())
            {
                var hashedValue = sha256.ComputeHash(System.Text.Encoding.Unicode.GetBytes(data));
                return Convert.ToBase64String(hashedValue);
            }
        }
        private bool ValidateChecksum(string data,string storedChecksum)
        {
            string calculateChecksum = GenerateChecksum(data);
            return calculateChecksum == storedChecksum;
        }
    }
}
