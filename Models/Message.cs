using System;

namespace CLAWDESK.Models
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Content { get; set; } = string.Empty;
        public bool IsUser { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
