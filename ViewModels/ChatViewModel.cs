using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CLAWDESK.Models;
using CLAWDESK.Services;

namespace CLAWDESK.ViewModels
{
    public class ChatViewModel : ViewModelBase
    {
        private readonly OpenClawService _openClawService;
        private string _inputText = string.Empty;
        private bool _isLoading;

        public ObservableCollection<Message> Messages { get; } = new();

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand SendCommand { get; }
        public ICommand ClearCommand { get; }

        public ChatViewModel(OpenClawService openClawService)
        {
            _openClawService = openClawService;
            SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !IsLoading && !string.IsNullOrWhiteSpace(InputText));
            ClearCommand = new RelayCommand(_ => ClearMessages());

            // 加入歡迎訊息
            Messages.Add(new Message
            {
                Content = "🦞 嗨！我是 CLAWDESK，有什麼我可以幫你的嗎？",
                IsUser = false
            });
        }

        private async Task SendMessageAsync()
        {
            if (string.IsNullOrWhiteSpace(InputText)) return;

            var userMessage = InputText.Trim();
            InputText = string.Empty;

            // 加入使用者訊息
            Messages.Add(new Message
            {
                Content = userMessage,
                IsUser = true
            });

            IsLoading = true;

            try
            {
                var response = await _openClawService.SendMessageAsync(userMessage);

                // 加入 AI 回覆
                Messages.Add(new Message
                {
                    Content = response,
                    IsUser = false
                });
            }
            catch (Exception ex)
            {
                Messages.Add(new Message
                {
                    Content = $"[錯誤] {ex.Message}",
                    IsUser = false
                });
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ClearMessages()
        {
            Messages.Clear();
            Messages.Add(new Message
            {
                Content = "🦞 對話已清除",
                IsUser = false
            });
        }
    }
}
