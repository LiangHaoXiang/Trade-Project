using System.Windows;
using System.Windows.Controls;

using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class TradeHistoryView : UserControl
{
    #region 构造函数

    public TradeHistoryView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    #endregion

    #region 私有接口

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (DataContext is TradeHistoryViewModel vm)
        {
            vm.PropertyChanged += (s, args) =>
            {
                if (args.PropertyName == nameof(TradeHistoryViewModel.IsBacktestMode))
                {
                    UpdateModeButtons(vm.IsBacktestMode);
                }
            };
            UpdateModeButtons(vm.IsBacktestMode);
        }
    }

    private void UpdateModeButtons(bool isBacktest)
    {
        if (BtnBacktest == null || BtnSim == null) return;

        BtnBacktest.Style = (Style)FindResource(isBacktest ? "PrimaryButtonStyle" : "ActionButtonStyle");
        BtnSim.Style = (Style)FindResource(isBacktest ? "ActionButtonStyle" : "PrimaryButtonStyle");
    }

    private void OnBacktestModeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is TradeHistoryViewModel vm)
        {
            vm.CurrentMode = TradeHistoryViewModel.BacktestMode;
        }
    }

    private void OnSimModeClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is TradeHistoryViewModel vm)
        {
            vm.CurrentMode = TradeHistoryViewModel.SimMode;
        }
    }

    private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
    }

    #endregion
}
