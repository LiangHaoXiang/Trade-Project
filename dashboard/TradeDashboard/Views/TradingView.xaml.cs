using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;

using TradeDashboard.Services;
using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class TradingView : UserControl
{
    public TradingView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is TradingViewModel vm)
        {
            vm.Log($"TradingView.Loaded DataContext类型正确: {vm.GetType().Name}");
        }
        else
        {
            Debug.WriteLine($"[TradingView] DataContext 类型异常: {DataContext?.GetType().Name ?? "null"}");
        }
    }

    private void OnSubmitOrderClick(object sender, RoutedEventArgs e)
    {
        InteractionLogService.Write("交易", ">>> 提交委托");
    }

    private void OnFillMaxVolumeClick(object sender, RoutedEventArgs e)
    {
        InteractionLogService.Write("交易", ">>> 全仓");
    }

    private void OnResetAccountClick(object sender, RoutedEventArgs e)
    {
        InteractionLogService.Write("交易", ">>> 重置模拟账户");
    }

    private void OnConnectLiveClick(object sender, RoutedEventArgs e)
    {
        InteractionLogService.Write("交易", ">>> 连接券商");
    }

    private void OnDisconnectLiveClick(object sender, RoutedEventArgs e)
    {
        InteractionLogService.Write("交易", ">>> 断开连接");
    }
}
