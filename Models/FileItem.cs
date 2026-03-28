using System;

namespace CLAWDESK.Models
{
    public class FileItem
    {
        public string Name { get; set; } = string.Empty;
        public string FullPath { get; set; } = string.Empty;
        public bool IsDirectory { get; set; }
        public DateTime LastModified { get; set; }
    }
}
