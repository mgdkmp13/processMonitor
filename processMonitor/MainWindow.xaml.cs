using System.Windows;
using processMonitor.ViewModels;

namespace processMonitor
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        private void ProccessListView_Loaded(object sender, RoutedEventArgs e)
        {

        }
    }
}