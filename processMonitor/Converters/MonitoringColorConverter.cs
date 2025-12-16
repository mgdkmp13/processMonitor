using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace processMonitor.Converters
{
    public class MonitoringColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMonitoring)
            {
                return isMonitoring 
                    ? new SolidColorBrush(Color.FromRgb(231, 76, 60))  // Red for pause
                    : new SolidColorBrush(Color.FromRgb(39, 174, 96)); // Green for start
            }
            return new SolidColorBrush(Color.FromRgb(39, 174, 96));
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
