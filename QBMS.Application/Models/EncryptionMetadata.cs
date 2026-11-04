using System;
using System.Collections.Generic;
using System.Text;

namespace QBMS.Application.Models
{
    public class EncryptionMetadata
    {
        public int Version { get; set; }
        public DateTime EncryptedAt { get; set; }
        public string Algorithm { get; set; }
        public int KeyLength { get; set; }
    }
}
