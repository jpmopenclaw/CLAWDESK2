using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CLAWDESK.Models;
using CLAWDESK.Services;

namespace CLAWDESK.ViewModels
{
    public class CommandsViewModel : ViewModelBase
    {
        private readonly OpenClawService _openClawService;
        private string _commandInput = string.Empty;
        private string _output = string.Empty;
        private bool _isExecuting;

        public ObservableCollection<QuickCommand> QuickCommands { get; } = new();

        public string CommandInput
        {
            get => _commandInput;
            set => SetProperty(ref _commandInput, value);
        }

        public string Output
        {
            get => _output;
            set => SetProperty(ref _output, value);
        }

        public bool IsExecuting
        {
            get => _isExecuting;
            set => SetProperty(ref _isExecuting, value);
        }

        public ICommand ExecuteCommand { get; }
        public ICommand RunQuickCommandCommand { get; }

        public CommandsViewModel(OpenClawService openClawService)
        {
            _openClawService = openClawService;

            ExecuteCommand = new RelayCommand(async _ => await ExecuteAsync(), _ => !IsExecuting && !string.IsNullOrWhiteSpace(CommandInput));
            RunQuickCommandCommand = new RelayCommand(async p => await RunQuickCommandAsync(p?.ToString()));

            // 初始化快速指令
            QuickCommands.Add(new QuickCommand { Name = "/status", Description = "查看狀態" });
            QuickCommands.Add(new QuickCommand { Name = "/help", Description = "幫助" });
            QuickCommands.Add(new QuickCommand { Name = "/memory", Description = "查看記憶" });
            QuickCommands.Add(new QuickCommand { Name = "/models", Description = "列出可用模型" });
        }

        private async Task ExecuteAsync()
        {
            if (string.IsNullOrWhiteSpace(CommandInput)) return;

            var command = CommandInput.Trim();
            CommandInput = string.Empty;
            IsExecuting = true;

            try
            {
                var response = await _openClawService.SendMessageAsync(command);
                Output = response;
            }
            catch (Exception ex)
            {
                Output = $"[錯誤] {ex.Message}";
            }
            finally
            {
                IsExecuting = false;
            }
        }

        private async Task RunQuickCommandAsync(string? command)
        {
            if (string.IsNullOrEmpty(command)) return;
            
            CommandInput = command;
            await ExecuteAsync();
        }
    }

    public class QuickCommand
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
