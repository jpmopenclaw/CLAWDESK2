using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace CLAWDESK.ViewModels
{
    public class CronJob
    {
        public string Name { get; set; } = string.Empty;
        public string Schedule { get; set; } = string.Empty;
        public string Command { get; set; } = string.Empty;
        public bool IsEnabled { get; set; } = true;
    }

    public class CronViewModel : ViewModelBase
    {
        public ObservableCollection<CronJob> Jobs { get; } = new ObservableCollection<CronJob>();

        public ICommand AddJobCommand { get; }

        public CronViewModel()
        {
            Jobs.Add(new CronJob { Name = "Morning Greeting", Schedule = "0 8 * * *", Command = "Send daily greeting", IsEnabled = true });
            Jobs.Add(new CronJob { Name = "Weekly Report", Schedule = "0 17 * * 5", Command = "Generate weekly report", IsEnabled = false });

            AddJobCommand = new RelayCommand(_ => AddNewJob());
        }

        private void AddNewJob()
        {
            Jobs.Add(new CronJob { Name = "New Task", Schedule = "* * * * *", Command = "Do something", IsEnabled = true });
        }
    }
}
