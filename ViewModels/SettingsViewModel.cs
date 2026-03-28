using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using CLAWDESK.Models;
using CLAWDESK.Services;

namespace CLAWDESK.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly ConfigService _configService;
        private readonly OpenClawService _openClawService;
        
        private string _selectedProvider = "OpenClaw (Auto)";
        private string _apiKey = string.Empty;
        private string _defaultModel = "MiniMax-M2.5";
        private string _gatewayUrl = "http://localhost:18789";
        private string _testResult = string.Empty;
        private bool _isTesting;
        private bool _showApiKey;
        private bool _hasOpenClawConfig;
        private bool _isUsingOpenClawConfig;
        private List<string> _availableModels = new List<string>();
        private List<string> _providers = new List<string> { "OpenClaw (Auto)", "OpenAI", "Anthropic", "Google", "MiniMax", "Azure OpenAI" };

        public string SelectedProvider
        {
            get => _selectedProvider;
            set
            {
                if (SetProperty(ref _selectedProvider, value))
                {
                    OnProviderChanged();
                }
            }
        }

        public string ApiKey
        {
            get => _apiKey;
            set => SetProperty(ref _apiKey, value);
        }

        public string DefaultModel
        {
            get => _defaultModel;
            set => SetProperty(ref _defaultModel, value);
        }

        public string GatewayUrl
        {
            get => _gatewayUrl;
            set => SetProperty(ref _gatewayUrl, value);
        }

        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }

        public bool IsTesting
        {
            get => _isTesting;
            set => SetProperty(ref _isTesting, value);
        }

        public bool ShowApiKey
        {
            get => _showApiKey;
            set => SetProperty(ref _showApiKey, value);
        }

        public bool HasOpenClawConfig
        {
            get => _hasOpenClawConfig;
            set => SetProperty(ref _hasOpenClawConfig, value);
        }

        public bool IsUsingOpenClawConfig
        {
            get => _isUsingOpenClawConfig;
            set => SetProperty(ref _isUsingOpenClawConfig, value);
        }

        public List<string> AvailableModels
        {
            get => _availableModels;
            set => SetProperty(ref _availableModels, value);
        }

        public List<string> Providers
        {
            get => _providers;
            set => SetProperty(ref _providers, value);
        }

        public ICommand SaveCommand { get; }
        public ICommand TestCommand { get; }
        public ICommand ToggleShowApiKeyCommand { get; }

        public SettingsViewModel(ConfigService configService, OpenClawService openClawService)
        {
            _configService = configService;
            _openClawService = openClawService;

            SaveCommand = new RelayCommand(async _ => await SaveSettingsAsync());
            TestCommand = new RelayCommand(async _ => await TestConnectionAsync(), _ => !IsTesting);
            ToggleShowApiKeyCommand = new RelayCommand(_ => ShowApiKey = !ShowApiKey);

            // 檢查是否有 OpenClaw 設定
            HasOpenClawConfig = _configService.HasOpenClawConfig();
            IsUsingOpenClawConfig = _configService.IsUsingOpenClawConfig;
            
            // 載入可用模型
            LoadAvailableModels();
            
            LoadSettings();
        }

        private void LoadAvailableModels()
        {
            var models = _configService.GetAvailableModels();
            if (models.Count > 0)
            {
                AvailableModels = models;
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Loaded {models.Count} models from OpenClaw");
            }
            else
            {
                // 如果沒有從 OpenClaw 取得模型，使用預設列表
                AvailableModels = new List<string> { "MiniMax-M2.5", "MiniMax-M2.5-Lightning", "MiniMax-M2.7" };
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Using default models");
            }
        }

        private void OnProviderChanged()
        {
            // 當 Provider 改變時，更新模型列表
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Provider changed to: {SelectedProvider}");
            
            if (SelectedProvider == "OpenClaw (Auto)")
            {
                // 使用 OpenClaw 的模型列表
                LoadAvailableModels();
            }
            else if (SelectedProvider == "MiniMax")
            {
                AvailableModels = new List<string> { "MiniMax-M2.5", "MiniMax-M2.5-Lightning", "MiniMax-M2.7" };
            }
            else if (SelectedProvider == "OpenAI")
            {
                AvailableModels = new List<string> { "gpt-4o", "gpt-4o-mini", "gpt-4-turbo" };
            }
            else if (SelectedProvider == "Anthropic")
            {
                AvailableModels = new List<string> { "claude-sonnet-4-20250514", "claude-opus-4-20250514", "claude-3-opus" };
            }
            else if (SelectedProvider == "Google")
            {
                AvailableModels = new List<string> { "gemini-2.0-flash", "gemini-1.5-pro" };
            }
            else if (SelectedProvider == "Azure OpenAI")
            {
                AvailableModels = new List<string> { "gpt-4", "gpt-35-turbo" };
            }
            
            // 觸發 UI 更新
            OnPropertyChanged(nameof(AvailableModels));
        }

        private void LoadSettings()
        {
            var config = _configService.GetConfig();
            SelectedProvider = config.Provider;
            ApiKey = config.ApiKey;
            DefaultModel = config.DefaultModel;
            GatewayUrl = config.GatewayUrl;
            
            System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Settings loaded - Provider: {SelectedProvider}, Model: {DefaultModel}");
        }

        private async Task SaveSettingsAsync()
        {
            var config = new ModelConfig
            {
                Provider = SelectedProvider,
                ApiKey = ApiKey,
                DefaultModel = DefaultModel,
                GatewayUrl = GatewayUrl
            };

            await _configService.SaveAsync(config);
            TestResult = "✅ 設定已儲存！";
        }

        private async Task TestConnectionAsync()
        {
            IsTesting = true;
            TestResult = "測試中...";

            try
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Testing connection with - Provider: {SelectedProvider}, Model: {DefaultModel}, GatewayUrl: {GatewayUrl}");
                var success = await _openClawService.TestConnectionAsync();
                TestResult = success ? "✅ 連線成功！" : "❌ 連線失敗";
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Connection test result: {(success ? "success" : "failed")}");
            }
            catch (Exception ex)
            {
                TestResult = $"❌ 錯誤: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[SettingsViewModel] Connection error: {ex.Message}");
            }
            finally
            {
                IsTesting = false;
            }
        }
    }
}
