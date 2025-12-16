using System.Diagnostics;
using System.Windows;

namespace processMonitor.Views
{
    public partial class PriorityDialog : Window
    {
        public ProcessPriorityClass SelectedPriority { get; set; }

        public PriorityDialog()
        {
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e)
        {
            if (PriorityComboBox.SelectedItem != null)
            {
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please select a priority.", "Warning", 
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
