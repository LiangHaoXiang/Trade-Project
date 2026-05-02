using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Input;

using TradeDashboard.Models;

namespace TradeDashboard.Views;

public partial class NewsView : UserControl
{
    public NewsView()
    {
        InitializeComponent();
    }

    private void OnTitleClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock tb || tb.DataContext is not NewsItem item || !item.HasUrl)
        {
            return;
        }

        OpenInBrowser(item.Url);
    }

    private void OnLinkClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not TextBlock tb || tb.DataContext is not NewsItem item || !item.HasUrl)
        {
            return;
        }

        OpenInBrowser(item.Url);
    }

    private static void OpenInBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true,
            });
        }
        catch
        {
        }
    }
}
