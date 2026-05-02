using System.Windows;
using System.Windows.Controls;

using ScottPlot;

using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class BacktestView : UserControl
{
    #region 构造函数

    public BacktestView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    #endregion

    #region 私有接口

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is BacktestViewModel oldVm)
        {
            oldVm.EquityDataChanged -= OnEquityDataChanged;
        }
        if (e.NewValue is BacktestViewModel newVm)
        {
            newVm.EquityDataChanged += OnEquityDataChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BacktestViewModel vm)
        {
            return;
        }
        vm.EquityDataChanged -= OnEquityDataChanged;
        vm.EquityDataChanged += OnEquityDataChanged;
    }

    private void OnEquityDataChanged(object? sender, EventArgs e)
    {
        if (DataContext is not BacktestViewModel vm)
        {
            return;
        }
        var equity = vm.Result?.EquityCurve;
        if (equity == null || equity.Count == 0)
        {
            return;
        }

        var plot = EquityPlot.Plot;
        plot.Clear();

        plot.FigureBackground.Color = Color.FromHex("#1a1a2e");
        plot.Axes.Color(Color.FromHex("#1a1a2e"));
        plot.Axes.Left.TickLabelStyle.ForeColor = Colors.LightGray;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = Colors.LightGray;
        plot.Axes.Frame(false);
        plot.HideGrid();

        var values = equity.Select(pt => pt.Equity).ToArray();
        var sig = plot.Add.Signal(values, 1);
        sig.Color = Color.FromHex("#89b4fa");
        sig.LineWidth = 2;

        plot.Axes.AutoScale();
        plot.HideGrid();
        EquityPlot.Refresh();
    }

    #endregion
}
