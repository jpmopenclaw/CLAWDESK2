using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CLAWDESK.Models;
using CLAWDESK.Services;
using CLAWDESK.ViewModels;

namespace CLAWDESK.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel? _viewModel;
        private readonly ConfigService _configService;
        private readonly OpenClawService _openClawService;
        private readonly FileService _fileService;

        public MainWindow() : this(null) { }

        public MainWindow(MainViewModel? viewModel)
        {
            InitializeComponent();

            // 初始化服務
            _configService = new ConfigService();
            _openClawService = new OpenClawService(_configService);
            _fileService = new FileService(_configService);

            // 初始化 ViewModel
            _viewModel = viewModel ?? new MainViewModel(_configService, _openClawService, _fileService);
            DataContext = _viewModel;

            // 載入設定
            LoadSettings();

            // 初始化檔案列表
            _ = LoadFilesAsync();

            // 等 XAML 載入完成後再處理導航
            Loaded += MainWindow_Loaded;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // 確保一開始顯示 ChatView
            ShowView(ChatView);
        }

        private void LoadSettings()
        {
            var config = _configService.GetConfig();
            var isUsingOpenClaw = _configService.IsUsingOpenClawConfig;
            
            System.Diagnostics.Debug.WriteLine($"[MainWindow] LoadSettings - Provider: {config.Provider}, UsingOpenClaw: {isUsingOpenClaw}");

            // Provider - 根據是否使用 OpenClaw 設定來選擇
            ProviderCombo.Items.Clear();
            if (isUsingOpenClaw)
            {
                ProviderCombo.Items.Add(new ComboBoxItem { Content = "OpenClaw (Auto)" });
            }
            ProviderCombo.Items.Add(new ComboBoxItem { Content = "OpenAI" });
            ProviderCombo.Items.Add(new ComboBoxItem { Content = "Anthropic" });
            ProviderCombo.Items.Add(new ComboBoxItem { Content = "Google" });
            ProviderCombo.Items.Add(new ComboBoxItem { Content = "MiniMax" });
            ProviderCombo.Items.Add(new ComboBoxItem { Content = "Azure OpenAI" });
            
            // 選取 Provider
            foreach (ComboBoxItem item in ProviderCombo.Items)
            {
                if (item.Content.ToString() == config.Provider)
                {
                    ProviderCombo.SelectedItem = item;
                    break;
                }
            }
            // 如果沒有匹配的 Provider，預設選第一個
            if (ProviderCombo.SelectedItem == null)
            {
                ProviderCombo.SelectedIndex = 0;
            }

            ApiTokenBox.Password = config.ApiKey;
            GatewayUrlBox.Text = config.GatewayUrl;

            // Model - 從 OpenClaw 讀取可用模型
            ModelCombo.Items.Clear();
            var models = _configService.GetAvailableModels();
            if (models.Count > 0)
            {
                foreach (var model in models)
                {
                    ModelCombo.Items.Add(new ComboBoxItem { Content = model });
                }
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Loaded {models.Count} models");
            }
            else
            {
                // 如果沒有模型，使用預設列表
                ModelCombo.Items.Add(new ComboBoxItem { Content = "MiniMax-M2.5" });
                ModelCombo.Items.Add(new ComboBoxItem { Content = "MiniMax-M2.5-Lightning" });
                ModelCombo.Items.Add(new ComboBoxItem { Content = "MiniMax-M2.7" });
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Using default models");
            }
            
            // 選取目前設定的模型
            foreach (ComboBoxItem item in ModelCombo.Items)
            {
                if (item.Content.ToString() == config.DefaultModel)
                {
                    ModelCombo.SelectedItem = item;
                    break;
                }
            }
            // 如果沒有匹配的模型，預設選第一個
            if (ModelCombo.SelectedItem == null && ModelCombo.Items.Count > 0)
            {
                ModelCombo.SelectedIndex = 0;
            }
        }

        private async System.Threading.Tasks.Task LoadFilesAsync()
        {
            try
            {
                var files = await _fileService.GetFilesAsync();
                FileList.ItemsSource = files;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"載入檔案失敗: {ex.Message}", "錯誤", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        #region 導航

        private void NavChat_Checked(object sender, RoutedEventArgs e)
        {
            ShowView(ChatView);
        }

        private void NavSettings_Checked(object sender, RoutedEventArgs e)
        {
            ShowView(SettingsView);
        }

        private void NavFiles_Checked(object sender, RoutedEventArgs e)
        {
            ShowView(FilesView);
        }

                private void NavCron_Checked(object sender, RoutedEventArgs e)
        {
            ShowView(CronView);
        }

        private void NavCommands_Checked(object sender, RoutedEventArgs e)
        {
            ShowView(CommandsView);
        }

        private void ShowView(UIElement viewToShow)
        {
            if (viewToShow == null) return;

            ChatView.Visibility = Visibility.Collapsed;
            CronView.Visibility = Visibility.Collapsed;
            SettingsView.Visibility = Visibility.Collapsed;
            FilesView.Visibility = Visibility.Collapsed;
            CommandsView.Visibility = Visibility.Collapsed;

            viewToShow.Visibility = Visibility.Visible;
        }

        #endregion

        #region 對話

        private void ChatInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
            {
                e.Handled = true;
                SendMessage();
            }
        }

        private void SendButton_Click(object sender, RoutedEventArgs e)
        {
            SendMessage();
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            MessagesList.ItemsSource = null;
            MessagesList.Items.Clear();
            StatusText.Text = "🧹 對話已清除";
        }

        private async void SendMessage()
        {
            var message = ChatInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            ChatInput.Text = string.Empty;

            // 加入使用者訊息
            var userMsg = new Message { Content = message, IsUser = true };
            MessagesList.Items.Add(userMsg);

            StatusText.Text = "🤔 處理中...";

            try
            {
                var response = await _openClawService.SendMessageAsync(message);

                // 加入 AI 回覆
                var aiMsg = new Message { Content = response, IsUser = false };
                MessagesList.Items.Add(aiMsg);

                // 自動捲動到底部
                var scrollViewer = GetScrollViewer(MessagesList);
                if (scrollViewer != null)
                {
                    scrollViewer.ScrollToEnd();
                }
            }
            catch (Exception ex)
            {
                var errorMsg = new Message { Content = $"[錯誤] {ex.Message}", IsUser = false };
                MessagesList.Items.Add(errorMsg);
            }

            StatusText.Text = "🦞 就緒";
        }

        private ScrollViewer? GetScrollViewer(DependencyObject obj)
        {
            if (obj is ScrollViewer sv) return sv;

            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                var result = GetScrollViewer(child);
                if (result != null) return result;
            }
            return null;
        }

        #endregion

        #region 設定

        private void ToggleApiToken_Click(object sender, RoutedEventArgs e)
        {
            // PasswordBox 不支援直接切換可見性，這裡用一個簡單的方式處理
            // 實際應用中可以改用 TextBox 配合 PasswordChar
            MessageBox.Show("API Key 以密碼方式儲存，請直接輸入", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private async void SaveSettings_Click(object sender, RoutedEventArgs e)
        {
            var selectedModel = (ModelCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "";
            
            var config = new ModelConfig
            {
                Provider = (ProviderCombo.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "OpenAI",
                ApiKey = ApiTokenBox.Password,
                DefaultModel = selectedModel,
                GatewayUrl = GatewayUrlBox.Text
            };

            await _configService.SaveAsync(config);
            TestResultText.Text = "✅ 設定已儲存！";
        }

        private async void TestConnection_Click(object sender, RoutedEventArgs e)
        {
            TestResultText.Text = "🔄 測試中...";

            try
            {
                var success = await _openClawService.TestConnectionAsync();
                TestResultText.Text = success ? "✅ 連線成功！" : "❌ 連線失敗";
            }
            catch (Exception ex)
            {
                TestResultText.Text = $"❌ 錯誤: {ex.Message}";
            }
        }

        #endregion

        #region 檔案

        private async void GoBack_Click(object sender, RoutedEventArgs e)
        {
            var currentPath = _configService.GetWorkspacePath();
            var parent = System.IO.Directory.GetParent(currentPath);
            if (parent != null)
            {
                var files = await _fileService.GetFilesAsync(parent.FullName);
                FileList.ItemsSource = files;
            }
        }

        private async void RefreshFiles_Click(object sender, RoutedEventArgs e)
        {
            await LoadFilesAsync();
        }

        private async void FileList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (FileList.SelectedItem is FileItem file)
            {
                if (file.IsDirectory)
                {
                    var files = await _fileService.GetFilesAsync(file.FullPath);
                    FileList.ItemsSource = files;
                }
                else
                {
                    var content = await _fileService.ReadFileContentAsync(file.FullPath);
                    FileContentBox.Text = content;
                }
            }
        }

        #endregion

        #region 指令

        private void CommandInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                ExecuteCommand();
            }
        }

        private void QuickCommand_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                CommandInputBox.Text = button.Content.ToString() ?? string.Empty;
                ExecuteCommand();
            }
        }

        private async void ExecuteCommand()
        {
            var command = CommandInputBox.Text.Trim();
            if (string.IsNullOrEmpty(command)) return;

            CommandInputBox.Text = string.Empty;
            CommandOutput.Text = "🔄 執行中...";

            try
            {
                var response = await _openClawService.SendMessageAsync(command);
                CommandOutput.Text = response;
            }
            catch (Exception ex)
            {
                CommandOutput.Text = $"[錯誤] {ex.Message}";
            }
        }

        #endregion

        #region 主題切換

        private bool _isDarkMode = false;

        private void ThemeToggle_Click(object sender, RoutedEventArgs e)
        {
            _isDarkMode = ThemeToggle.IsChecked == true;
            ApplyTheme(_isDarkMode);
        }

        private void ApplyTheme(bool isDark)
        {
            if (isDark)
            {
                // === Dark Mode (OLED) ===
                var darkBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(18, 18, 18));
                var darkCardBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 30, 30));
                var darkText = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                var darkTextSecondary = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(200, 200, 200));
                var darkBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(50, 50, 50));
                
                // 整個 Window
                this.Background = darkBg;
                ContentArea.Background = darkBg;
                
                // 側邊欄
                var sidebar = (Border)((Grid)ContentArea.Children[0]).Children[0];
                sidebar.Background = darkCardBg;
                
                UpdateNavButtonsForTheme(darkTextSecondary);
                UpdateMessagesForTheme();
                
                // 輸入框
                ChatInput.Background = darkCardBg;
                ChatInput.Foreground = darkText;
                ChatInput.BorderBrush = darkBorder;
                
                // 設定頁面
                SettingsView.Background = darkBg;
                CronView.Background = darkBg;
                
                // 檔案頁面
                FilesView.Background = darkBg;
                if (FileList != null) FileList.Background = darkCardBg;
                if (FileContentBox != null) { FileContentBox.Background = darkCardBg; FileContentBox.Foreground = darkText; }
                
                // 指令頁面
                CommandsView.Background = darkBg;
                if (CommandInputBox != null) { CommandInputBox.Background = darkCardBg; CommandInputBox.Foreground = darkText; CommandInputBox.BorderBrush = darkBorder; }
                if (CommandOutput != null) { CommandOutput.Background = darkCardBg; CommandOutput.Foreground = darkText; }
                
                // 主題切換
                LightThemeIcon.Opacity = 0.5;
                DarkThemeIcon.Opacity = 1.0;
                
                // 狀態列
                StatusText.Foreground = darkTextSecondary;
            }
            else
            {
                // === Light Mode ===
                var lightBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(248, 250, 252));
                var whiteBg = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                var lightText = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(30, 41, 59));
                var lightTextSecondary = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 116, 139));
                var lightBorder = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(226, 232, 240));
                
                this.Background = lightBg;
                ContentArea.Background = lightBg;
                
                var sidebar = (Border)((Grid)ContentArea.Children[0]).Children[0];
                sidebar.Background = whiteBg;
                
                UpdateNavButtonsForTheme(lightTextSecondary);
                UpdateMessagesForTheme();
                
                ChatInput.Background = whiteBg;
                ChatInput.Foreground = lightText;
                ChatInput.BorderBrush = lightBorder;
                
                SettingsView.Background = lightBg;
                CronView.Background = lightBg;
                FilesView.Background = lightBg;
                if (FileList != null) FileList.Background = whiteBg;
                if (FileContentBox != null) { FileContentBox.Background = whiteBg; FileContentBox.Foreground = lightText; }
                
                CommandsView.Background = lightBg;
                if (CommandInputBox != null) { CommandInputBox.Background = whiteBg; CommandInputBox.Foreground = lightText; CommandInputBox.BorderBrush = lightBorder; }
                if (CommandOutput != null) { CommandOutput.Background = whiteBg; CommandOutput.Foreground = lightText; }
                
                LightThemeIcon.Opacity = 1.0;
                DarkThemeIcon.Opacity = 0.5;
                
                StatusText.Foreground = lightTextSecondary;
            }
        }

        private void UpdateNavButtonsForTheme(System.Windows.Media.SolidColorBrush textColor)
        {
            var navStack = (StackPanel)NavChat.Parent;
            foreach (var child in navStack.Children)
            {
                if (child is RadioButton rb)
                {
                    rb.Foreground = textColor;
                }
            }
        }

        private void UpdateMessagesForTheme()
        {
            var messages = MessagesList.ItemsSource;
            MessagesList.ItemsSource = null;
            MessagesList.ItemsSource = messages;
        }

        #endregion
    }
}
