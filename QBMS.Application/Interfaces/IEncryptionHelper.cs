using QBMS.Application.Models;
using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace QBMS.Application.Interfaces
{
    public interface IEncryptionHelper
    {
        string EncryptField(string fieldValue);
        string DecryptField(string fieldValue);
        Dictionary<string, string> EncryptFields(Dictionary<string,string> fieldsToEncrypt);
        Dictionary<string, string> DecryptFields(Dictionary<string, string> fieldsToEncrypt);
        DataTable EncryptDataTable(DataTable dt, List<string> columnsToEncrypt);
        DataTable DecryptDataTable(DataTable dt, List<string> columnsToDecrypt);
        string EncryptQuestionData(dynamic questionObject);
        dynamic DecryptQuestionData(string encryptedData);
        EncryptionMetadata GetEncryptionMetadata();

    }
 
}
