using System.Globalization;
using System.Windows.Data;

namespace TradeDashboard.Converters;

public class StatusToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var status = value?.ToString()?.ToLowerInvariant();
        return status switch
        {
            "filled" => "已成交",
            "rejected" => "已拒绝",
            "pending" => "待成交",
            "cancelled" => "已撤单",
            _ => value?.ToString() ?? ""
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
