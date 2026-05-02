using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows.Data;

using TradeDashboard.Models;

namespace TradeDashboard.Converters;

public class FavoriteButtonTextConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] is not ObservableCollection<StockInfo> favs || values[1] is not string symbol)
        {
            return "+";
        }

        return favs.Any(f => f.Symbol == symbol) ? "－" : "＋";
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
