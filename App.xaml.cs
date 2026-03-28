using System;
using System.Windows;
using CLAWDESK.Services;
using CLAWDESK.ViewModels;

namespace CLAWDESK
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 初始化服務
            var configService = new ConfigService();
            var openClawService = new OpenClawService(configService);
            var fileService = new FileService(configService);

            // 建立 MainViewModel
            var mainViewModel = new MainViewModel(configService, openClawService, fileService);

            // 建立主視窗並傳入 ViewModel
            var mainWindow = new Views.MainWindow(mainViewModel);
            mainWindow.Show();
        }
    }
}