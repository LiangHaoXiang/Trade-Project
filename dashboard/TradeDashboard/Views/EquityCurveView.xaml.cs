using System;
using System.Windows;
using System.Windows.Controls;

using ScottPlot;

using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class EquityCurveView : UserControl
{
    #region 构造函数

    public EquityCurveView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    #endregion

    #region 私有接口

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is EquityCurveViewModel oldVm)
        {
            oldVm.DataChanged -= OnDataChanged;
        }
        if (e.NewValue is EquityCurveViewModel newVm)
        {
            newVm.DataChanged += OnDataChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not EquityCurveViewModel vm)
        {
            return;
        }
        vm.DataChanged -= OnDataChanged;
        vm.DataChanged += OnDataChanged;
    }

    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (DataContext is not EquityCurveViewModel vm)
        {
            return;
        }

        // Equity curve
        if (vm.EquityPoints.Count > 0)
        {
            EquityPlaceholder.Visibility = Visibility.Hidden;
            var eqPlot = EquityPlot.Plot;
            eqPlot.Clear();
            StylePlot(eqPlot);

            var values = vm.EquityPoints.Select(pt => pt.Equity).ToArray();
            var sig = eqPlot.Add.Signal(values, 1);
            sig.Color = Color.FromHex("#89b4fa");
            sig.LineWidth = 2;

            eqPlot.Axes.AutoScale();
            EquityPlot.Refresh();
        }

        // Drawdown
        if (vm.DrawdownPoints.Count > 0)
        {
            DrawdownPlaceholder.Visibility = Visibility.Hidden;
            var ddPlot = DrawdownPlot.Plot;
            ddPlot.Clear();
            StylePlot(ddPlot);

            var xs = Enumerable.Range(0, vm.DrawdownPoints.Count).Select(i => (double)i).ToArray();
            var scatter = ddPlot.Add.Scatter(xs, vm.DrawdownPoints.ToArray());
            scatter.Color = Color.FromHex("#ef5350");
            scatter.LineWidth = 1;
            scatter.FillY = true;
            scatter.FillYColor = Color.FromHex("#ef535040");
            scatter.MarkerSize = 0;

            ddPlot.Axes.AutoScale();
            DrawdownPlot.Refresh();
        }
    }

    #endregion

    #region 图表渲染

    private static void StylePlot(ScottPlot.Plot plot)
    {
        plot.FigureBackground.Color = Color.FromHex("#1a1a2e");
        plot.Axes.Color(Color.FromHex("#1a1a2e"));
        plot.Axes.Left.TickLabelStyle.ForeColor = Colors.LightGray;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = Colors.LightGray;
        plot.Axes.Frame(false);
        plot.HideGrid();
    }

    #endregion
}
