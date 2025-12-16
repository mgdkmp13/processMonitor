using System;
using System.Globalization;
using System.Windows.Data;

namespace processMonitor.Converters
{
    public class MonitoringButtonConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isMonitoring)
            {
                return isMonitoring ? "? Pause" : "? Start";
            }
            return "? Start";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
