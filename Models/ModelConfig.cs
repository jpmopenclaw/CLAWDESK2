using System;

namespace CLAWDESK.Models
{
    public class ModelConfig
    {
        public string Provider { get; set; } = "MiniMax";
        public string ApiKey { get; set; } = string.Empty;
        public string DefaultModel { get; set; } = "MiniMax-M2.5";
        public string GatewayUrl { get; set; } = "http://localhost:18789";
        public DateTime LastUpdated { get; set; } = DateTime.Now;
    }
}
