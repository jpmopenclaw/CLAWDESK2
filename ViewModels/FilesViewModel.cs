using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using CLAWDESK.Models;
using CLAWDESK.Services;

namespace CLAWDESK.ViewModels
{
    public class FilesViewModel : ViewModelBase
    {
        private readonly FileService _fileService;
        private string _currentPath = string.Empty;
        private string _fileContent = string.Empty;
        private FileItem? _selectedFile;
        private bool _isLoading;

        public ObservableCollection<FileItem> Files { get; } = new();

        public string CurrentPath
        {
            get => _currentPath;
            set => SetProperty(ref _currentPath, value);
        }

        public string FileContent
        {
            get => _fileContent;
            set => SetProperty(ref _fileContent, value);
        }

        public FileItem? SelectedFile
        {
            get => _selectedFile;
            set
            {
                if (SetProperty(ref _selectedFile, value) && value != null && !value.IsDirectory)
                {
                    _ = LoadFileContentAsync(value.FullPath);
                }
            }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public ICommand RefreshCommand { get; }
        public ICommand GoBackCommand { get; }
        public ICommand NavigateCommand { get; }

        public FilesViewModel(FileService fileService)
        {
            _fileService = fileService;
            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            GoBackCommand = new RelayCommand(async _ => await GoBackAsync(), _ => !string.IsNullOrEmpty(CurrentPath));
            NavigateCommand = new RelayCommand(async p => await NavigateAsync(p?.ToString()));

            _ = RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            IsLoading = true;
            try
            {
                var files = await _fileService.GetFilesAsync(CurrentPath);
                Files.Clear();
                foreach (var file in files)
                {
                    Files.Add(file);
                }

                if (string.IsNullOrEmpty(CurrentPath))
                {
                    CurrentPath = _fileService.GetWorkspacePath();
                }
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadFileContentAsync(string path)
        {
            FileContent = await _fileService.ReadFileContentAsync(path);
        }

        private async Task GoBackAsync()
        {
            if (string.IsNullOrEmpty(CurrentPath)) return;
            
            var parent = System.IO.Directory.GetParent(CurrentPath);
            if (parent != null)
            {
                CurrentPath = parent.FullName;
                await RefreshAsync();
            }
        }

        private async Task NavigateAsync(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            
            CurrentPath = path;
            await RefreshAsync();
        }
    }
}
