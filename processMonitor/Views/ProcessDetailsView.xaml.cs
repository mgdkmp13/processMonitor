using System.Windows;
using System.Windows.Controls;

namespace processMonitor.Views
{
    public partial class ProcessDetailsView : UserControl
    {
        public ProcessDetailsView()
        {
            InitializeComponent();
        }

        private void ChangePriority_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is Models.ProcessInfo processInfo)
            {
                var dialog = new PriorityDialog
                {
                    Owner = Window.GetWindow(this),
                    SelectedPriority = processInfo.Priority
                };

                if (dialog.ShowDialog() == true)
                {
                    var mainWindow = Application.Current.MainWindow;
                    var viewModel = mainWindow?.DataContext as ViewModels.MainViewModel;
                    
                    if (viewModel != null)
                    {
                        var parameters = new object[] { processInfo, dialog.SelectedPriority };
                        
                        if (viewModel.ChangePriorityCommand.CanExecute(parameters))
                        {
                            viewModel.ChangePriorityCommand.Execute(parameters);
                        }
                    }
                }
            }
        }
    }
}
