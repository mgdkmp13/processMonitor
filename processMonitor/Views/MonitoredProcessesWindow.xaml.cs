using System.Windows;
using processMonitor.Models;
using processMonitor.ViewModels;

namespace processMonitor.Views
{
    public partial class MonitoredProcessesWindow : Window
    {
        public MonitoredProcessesWindow()
        {
            InitializeComponent();
        }

        private void ViewChart_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && 
                button.Tag is MonitoredProcess process)
            {
                // Check if process has samples
                if (process.MemorySamples.Count == 0)
                {
                    MessageBox.Show(
                        $"No performance data available for process '{process.Name}' (PID: {process.ProcessId}).\n\nThe process needs to be tracked for some time to collect data.",
                        "No Data Available",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                // Create ViewModel with the selected process
                var viewModel = new PerformanceChartViewModel(process);

                // Create and show the chart window
                var chartWindow = new PerformanceChartWindow
                {
                    DataContext = viewModel,
                    Title = $"Performance Chart - {process.Name} (PID: {process.ProcessId})",
                    Owner = this
                };

                chartWindow.Show();
            }
        }
    }
}
