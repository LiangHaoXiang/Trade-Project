using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TradeDashboard.Converters;

public class DirectionToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var direction = value?.ToString()?.ToUpperInvariant();
        return direction switch
        {
            "BUY" => new SolidColorBrush(Color.FromRgb(0xef, 0x53, 0x50)),
            "SELL" => new SolidColorBrush(Color.FromRgb(0x26, 0xa6, 0x9a)),
            _ => new SolidColorBrush(Color.FromRgb(0xc0, 0xca, 0x33))
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
