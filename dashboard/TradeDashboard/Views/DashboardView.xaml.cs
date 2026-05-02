using System.Windows;
using System.Windows.Controls;

using ScottPlot;

using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class DashboardView : UserControl
{
    #region 构造函数

    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    #endregion

    #region 私有接口

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is DashboardViewModel oldVm)
        {
            oldVm.EquityDataChanged -= OnEquityDataChanged;
        }
        if (e.NewValue is DashboardViewModel newVm)
        {
            newVm.EquityDataChanged += OnEquityDataChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }
        vm.EquityDataChanged -= OnEquityDataChanged;
        vm.EquityDataChanged += OnEquityDataChanged;
    }

    private void OnEquityDataChanged(object? sender, EventArgs e)
    {
        if (DataContext is not DashboardViewModel vm)
        {
            return;
        }
        if (vm.EquityPoints.Count == 0)
        {
            return;
        }

        MiniChartPlaceholder.Visibility = Visibility.Hidden;

        var plot = MiniEquityPlot.Plot;
        plot.Clear();

        plot.FigureBackground.Color = Colors.Transparent;
        plot.Axes.Color(Colors.Transparent);
        plot.Axes.Frame(false);
        plot.HideGrid();

        var values = vm.EquityPoints.Select(pt => pt.Equity).ToArray();
        var sig = plot.Add.Signal(values, 1);
        sig.Color = Color.FromHex("#89b4fa");
        sig.LineWidth = 2;

        plot.Axes.AutoScale();
        plot.HideGrid();
        MiniEquityPlot.Refresh();
    }

    #endregion
}
