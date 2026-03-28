using System.Windows.Input;
using CLAWDESK.Services;

namespace CLAWDESK.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private ViewModelBase _currentViewModel;
        private int _selectedIndex;

        public ChatViewModel ChatViewModel { get; }
        public SettingsViewModel SettingsViewModel { get; }
        public FilesViewModel FilesViewModel { get; }
        public CommandsViewModel CommandsViewModel { get; }

        public ViewModelBase CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (SetProperty(ref _selectedIndex, value))
                {
                    NavigateToIndex(value);
                }
            }
        }

        public ICommand NavigateCommand { get; }

        public MainViewModel(ConfigService configService, OpenClawService openClawService, FileService fileService)
        {
            ChatViewModel = new ChatViewModel(openClawService);
            SettingsViewModel = new SettingsViewModel(configService, openClawService);
            FilesViewModel = new FilesViewModel(fileService);
            CommandsViewModel = new CommandsViewModel(openClawService);

            _currentViewModel = ChatViewModel;
            NavigateCommand = new RelayCommand(p => NavigateToIndex(System.Convert.ToInt32(p)));
        }

        private void NavigateToIndex(int index)
        {
            CurrentViewModel = index switch
            {
                0 => ChatViewModel,
                1 => SettingsViewModel,
                2 => FilesViewModel,
                3 => CommandsViewModel,
                _ => ChatViewModel
            };
        }
    }
}
