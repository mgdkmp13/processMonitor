using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace processMonitor.Views
{
    public partial class ProccessListView : UserControl
    {
        private GridViewColumnHeader? _lastHeaderClicked = null;
        private ListSortDirection _lastDirection = ListSortDirection.Ascending;

        public ProccessListView()
        {
            InitializeComponent();
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            var headerClicked = e.OriginalSource as GridViewColumnHeader;
            ListSortDirection direction;

            if (headerClicked != null && headerClicked.Role != GridViewColumnHeaderRole.Padding)
            {
                if (headerClicked != _lastHeaderClicked)
                {
                    direction = ListSortDirection.Ascending;
                }
                else
                {
                    if (_lastDirection == ListSortDirection.Ascending)
                    {
                        direction = ListSortDirection.Descending;
                    }
                    else
                    {
                        direction = ListSortDirection.Ascending;
                    }
                }

                var columnBinding = headerClicked.Tag as string;
                Sort(columnBinding, direction);

                _lastHeaderClicked = headerClicked;
                _lastDirection = direction;
            }
        }

        private void Sort(string? sortBy, ListSortDirection direction)
        {
            if (string.IsNullOrEmpty(sortBy)) return;

            var dataView = CollectionViewSource.GetDefaultView((DataContext as ViewModels.MainViewModel)?.ProcessesView?.SourceCollection);

            dataView?.SortDescriptions.Clear();
            var sd = new SortDescription(sortBy, direction);
            dataView?.SortDescriptions.Add(sd);
            dataView?.Refresh();
        }
    }
}
