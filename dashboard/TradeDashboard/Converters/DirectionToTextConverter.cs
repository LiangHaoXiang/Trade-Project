using System.Globalization;
using System.Windows.Data;

namespace TradeDashboard.Converters;

public class DirectionToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var direction = value?.ToString()?.ToUpperInvariant();
        return direction switch
        {
            "BUY" => "买入",
            "SELL" => "卖出",
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
