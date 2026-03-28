using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CLAWDESK.Models;

namespace CLAWDESK.Services
{
    public class ConfigService
    {
        private readonly string _configPath;
        private ModelConfig _config;
        private bool _isUsingOpenClawConfig;

        public ConfigService()
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var clawdeskDir = Path.Combine(appData, "CLAWDESK");
            Directory.CreateDirectory(clawdeskDir);
            _configPath = Path.Combine(clawdeskDir, "config.json");
            _config = Load();
        }

        public ModelConfig GetConfig() => _config;
        
        // 是否使用 OpenClaw 自動設定
        public bool IsUsingOpenClawConfig => _isUsingOpenClawConfig;
        
        // 檢查是否從 OpenClaw 自動讀取設定（有設定才回傳 true）
        public bool HasOpenClawConfig()
        {
            var openClawPath = GetOpenClawConfigPath();
            if (string.IsNullOrEmpty(openClawPath))
                return false;

            try
            {
                if (!File.Exists(openClawPath))
                    return false;

                var json = File.ReadAllText(openClawPath);
                var openClawData = JsonSerializer.Deserialize<OpenClawConfig>(json);
                
                // 只要有 agents.defaults.model.primary 就視為有 OpenClaw 設定
                return openClawData?.Agents?.Defaults?.Model?.Primary != null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] HasOpenClawConfig error: {ex.Message}");
                return false;
            }
        }
        
        // 取得 OpenClaw 設定檔路徑（支援 WSL）
        private string? GetOpenClawConfigPath()
        {
            // 先嘗試 WSL 路徑
            var wslPath = @"\\wsl$\Ubuntu\home\ian2\.openclaw\openclaw.json";
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Checking WSL path: {wslPath}, exists: {File.Exists(wslPath)}");
            if (File.Exists(wslPath))
                return wslPath;
            
            // 嘗試其他 WSL 發行版
            foreach (var distro in new[] { "Ubuntu-20.04", "Ubuntu-22.04", "Debian" })
            {
                var altPath = $@"\\{distro}\home\ian2\.openclaw\openclaw.json";
                if (File.Exists(altPath))
                    return altPath;
            }
            
            // 回退到本地 Windows 路徑
            var localPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".openclaw",
                "openclaw.json");
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Checking local path: {localPath}, exists: {File.Exists(localPath)}");
            return localPath;
        }
        
        // 取得可用的模型清單（從 OpenClaw）
        public List<string> GetAvailableModels()
        {
            var models = new List<string>();
            
            try
            {
                var openClawPath = GetOpenClawConfigPath();
                if (string.IsNullOrEmpty(openClawPath) || !File.Exists(openClawPath))
                    return models;

                var json = File.ReadAllText(openClawPath);
                var openClawData = JsonSerializer.Deserialize<OpenClawConfigFull>(json);

                if (openClawData?.Models?.Providers != null)
                {
                    foreach (var provider in openClawData.Models.Providers)
                    {
                        if (provider.Value?.Models != null)
                        {
                            foreach (var model in provider.Value.Models)
                            {
                                if (!string.IsNullOrEmpty(model.Id))
                                {
                                    models.Add(model.Id);
                                }
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略錯誤
            }

            return models.Distinct().ToList();
        }

        public async Task SaveAsync(ModelConfig config)
        {
            config.LastUpdated = DateTime.Now;
            _config = config;
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_configPath, json);
        }

        private ModelConfig Load()
        {
            // 嘗試從 OpenClaw 設定讀取
            System.Diagnostics.Debug.WriteLine("[ConfigService] Loading config...");
            var openClawConfig = LoadFromOpenClaw();
            System.Diagnostics.Debug.WriteLine($"[ConfigService] LoadFromOpenClaw result: {(openClawConfig != null ? "success" : "null")}");
            
            // 讀取本地 CLAWDESK 設定（作為備用）
            var localConfig = new ModelConfig();
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    localConfig = JsonSerializer.Deserialize<ModelConfig>(json) ?? new ModelConfig();
                    System.Diagnostics.Debug.WriteLine($"[ConfigService] Local config loaded: Provider={localConfig.Provider}");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService] Error loading local config: {ex.Message}");
                }
            }
            
            // 合併設定：OpenClaw 設定優先，本地設定作為備用
            if (openClawConfig != null)
            {
                _isUsingOpenClawConfig = true;
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Using OpenClaw config - Provider: {openClawConfig.Provider}, Model: {openClawConfig.DefaultModel}");
                return new ModelConfig
                {
                    Provider = openClawConfig.Provider,
                    ApiKey = openClawConfig.ApiKey,
                    DefaultModel = openClawConfig.DefaultModel,
                    GatewayUrl = openClawConfig.GatewayUrl,
                    LastUpdated = DateTime.Now
                };
            }
            
            // 沒有 OpenClaw 設定，回退使用本地設定
            _isUsingOpenClawConfig = false;
            System.Diagnostics.Debug.WriteLine($"[ConfigService] Using local config - Provider: {localConfig.Provider}, Model: {localConfig.DefaultModel}");
            return localConfig;
        }

        private ModelConfig? LoadFromOpenClaw()
        {
            try
            {
                var openClawPath = GetOpenClawConfigPath();
                if (string.IsNullOrEmpty(openClawPath) || !File.Exists(openClawPath))
                {
                    System.Diagnostics.Debug.WriteLine($"[ConfigService] OpenClaw config not found at: {openClawPath}");
                    return null;
                }

                var json = File.ReadAllText(openClawPath);
                var openClawData = JsonSerializer.Deserialize<OpenClawConfig>(json);

                if (openClawData == null)
                {
                    System.Diagnostics.Debug.WriteLine("[ConfigService] Failed to deserialize OpenClaw config");
                    return null;
                }

                // 從 OpenClaw 取得設定（各欄位獨立檢查）
                var model = openClawData.Agents?.Defaults?.Model?.Primary;
                var gatewayUrl = openClawData.Gateway?.Port > 0 
                    ? $"http://localhost:{openClawData.Gateway.Port}" 
                    : "http://localhost:18789";
                var gatewayToken = openClawData.Gateway?.Auth?.Token ?? string.Empty;

                System.Diagnostics.Debug.WriteLine($"[ConfigService] Loaded from OpenClaw - Model: {model}, Token: {gatewayToken.Substring(0, 8)}...");

                return new ModelConfig
                {
                    Provider = "OpenClaw (Auto)",
                    ApiKey = gatewayToken,
                    DefaultModel = model ?? "MiniMax-M2.5",
                    GatewayUrl = gatewayUrl,
                    LastUpdated = DateTime.Now
                };
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Error loading OpenClaw config: {ex.Message}");
                return null;
            }
        }

        public string GetWorkspacePath()
        {
            // 先嘗試 WSL 路徑
            var wslPath = @"\\wsl$\Ubuntu\home\ian2\.openclaw\workspace";
            if (Directory.Exists(wslPath))
                return wslPath;
            
            // 回退到本地 Windows 路徑
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".openclaw", "workspace");
        }
    }

    // OpenClaw 設定檔結構
    internal class OpenClawConfig
    {
        [JsonPropertyName("gateway")]
        public GatewayConfig? Gateway { get; set; }

        [JsonPropertyName("agents")]
        public AgentsConfig? Agents { get; set; }
    }

    internal class GatewayConfig
    {
        [JsonPropertyName("port")]
        public int Port { get; set; }

        [JsonPropertyName("auth")]
        public GatewayAuthConfig? Auth { get; set; }
    }

    internal class GatewayAuthConfig
    {
        [JsonPropertyName("token")]
        public string? Token { get; set; }
    }

    internal class AgentsConfig
    {
        [JsonPropertyName("defaults")]
        public DefaultsConfig? Defaults { get; set; }
    }

    internal class DefaultsConfig
    {
        [JsonPropertyName("model")]
        public ModelConfigData? Model { get; set; }
    }

    internal class ModelConfigData
    {
        [JsonPropertyName("primary")]
        public string? Primary { get; set; }
    }
    
    // 完整結構 for 取得模型清單
    internal class OpenClawConfigFull
    {
        [JsonPropertyName("models")]
        public ModelsConfig? Models { get; set; }
    }
    
    internal class ModelsConfig
    {
        [JsonPropertyName("providers")]
        public Dictionary<string, ProviderConfig?>? Providers { get; set; }
    }
    
    internal class ProviderConfig
    {
        [JsonPropertyName("models")]
        public List<ModelItem>? Models { get; set; }
    }
    
    internal class ModelItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
    }
}
