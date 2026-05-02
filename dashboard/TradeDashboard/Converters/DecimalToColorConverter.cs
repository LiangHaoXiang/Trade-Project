using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TradeDashboard.Converters;

public class DecimalToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var d = value switch
        {
            double v => v,
            decimal v => (double)v,
            float v => v,
            int v => v,
            _ => 0
        };
        return d switch
        {
            > 0 => new SolidColorBrush(Color.FromRgb(0xef, 0x53, 0x50)),
            < 0 => new SolidColorBrush(Color.FromRgb(0x26, 0xa6, 0x9a)),
            _ => new SolidColorBrush(Color.FromRgb(0xc0, 0xca, 0x33))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
