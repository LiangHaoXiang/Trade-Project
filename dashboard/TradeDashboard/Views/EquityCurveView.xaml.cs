using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

using ScottPlot;
using SPColor = ScottPlot.Color;
using SPColors = ScottPlot.Colors;

using TradeDashboard.Models;
using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class EquityCurveView : UserControl
{
    private List<EquityPoint> m_EquityData = [];
    private List<double> m_DrawdownData = [];

    private FrameworkElement? m_ActiveChartElement;
    private Point m_LastPanPoint;
    private bool m_DidPan;

    #region 构造函数

    public EquityCurveView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    #endregion

    #region 公有接口

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        EquityPlot.PreviewMouseWheel += OnChartMouseWheel;
        EquityPlot.PreviewMouseLeftButtonDown += OnChartMouseDown;
        EquityPlot.PreviewMouseMove += OnChartMouseMove;
        EquityPlot.PreviewMouseLeftButtonUp += OnChartMouseUp;
        EquityPlot.MouseLeave += OnChartMouseLeave;

        DrawdownPlot.PreviewMouseWheel += OnChartMouseWheel;
        DrawdownPlot.PreviewMouseLeftButtonDown += OnChartMouseDown;
        DrawdownPlot.PreviewMouseMove += OnChartMouseMove;
        DrawdownPlot.PreviewMouseLeftButtonUp += OnChartMouseUp;
        DrawdownPlot.MouseLeave += OnChartMouseLeave;

        EquityPlot.UserInputProcessor.IsEnabled = false;
        DrawdownPlot.UserInputProcessor.IsEnabled = false;
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

        m_EquityData = vm.EquityPoints;
        m_DrawdownData = vm.DrawdownPoints;

        if (vm.EquityPoints.Count > 0)
        {
            EquityPlaceholder.Visibility = Visibility.Hidden;
            var eqPlot = EquityPlot.Plot;
            eqPlot.Clear();
            StylePlot(eqPlot);

            var xs = vm.EquityPoints.Select((_, i) => (double)i).ToArray();
            var ys = vm.EquityPoints.Select(pt => pt.Equity).ToArray();
            var scatter = eqPlot.Add.Scatter(xs, ys);
            scatter.Color = SPColor.FromHex("#89b4fa");
            scatter.LineWidth = 2;
            scatter.MarkerSize = 0;

            eqPlot.Axes.AutoScale();
            EquityPlot.Refresh();
        }

        if (vm.DrawdownPoints.Count > 0)
        {
            DrawdownPlaceholder.Visibility = Visibility.Hidden;
            var ddPlot = DrawdownPlot.Plot;
            ddPlot.Clear();
            StylePlot(ddPlot);

            var ddXs = Enumerable.Range(0, vm.DrawdownPoints.Count).Select(i => (double)i).ToArray();
            var ddScatter = ddPlot.Add.Scatter(ddXs, vm.DrawdownPoints.ToArray());
            ddScatter.Color = SPColor.FromHex("#ef5350");
            ddScatter.LineWidth = 1;
            ddScatter.FillY = true;
            ddScatter.FillYColor = SPColor.FromHex("#ef535040");
            ddScatter.MarkerSize = 0;

            ddPlot.Axes.AutoScale();
            SyncDrawdownXAxis();
            DrawdownPlot.Refresh();
        }

        RefreshAxisLabels();
    }

    #endregion

    #region 图表渲染

    private static void StylePlot(ScottPlot.Plot plot)
    {
        plot.FigureBackground.Color = SPColor.FromHex("#1a1a2e");
        plot.Axes.Color(SPColor.FromHex("#1a1a2e"));
        plot.Axes.Left.TickLabelStyle.ForeColor = SPColors.LightGray;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = SPColors.LightGray;
        plot.Axes.Frame(false);
        plot.HideGrid();
    }

    private void SyncDrawdownXAxis()
    {
        if (m_EquityData.Count == 0) return;
        var eqLimits = EquityPlot.Plot.Axes.GetLimits();
        DrawdownPlot.Plot.Axes.SetLimitsX(eqLimits.Left, eqLimits.Right);
    }

    private void RefreshAxisLabels()
    {
        UpdateXAxisDateLabels(EquityPlot, GetEquityDates());
        UpdateXAxisDateLabels(DrawdownPlot, GetDrawdownDates());
    }

    private List<DateTime> GetEquityDates()
    {
        return m_EquityData.Select(p => p.Date).ToList();
    }

    private List<DateTime> GetDrawdownDates()
    {
        var dates = m_EquityData.Select(p => p.Date).ToList();
        if (dates.Count >= m_DrawdownData.Count)
        {
            return dates.Take(m_DrawdownData.Count).ToList();
        }
        return dates;
    }

    private static void UpdateXAxisDateLabels(ScottPlot.WPF.WpfPlot wpfPlot, List<DateTime> dates)
    {
        if (dates.Count == 0) return;
        var plot = wpfPlot.Plot;
        var limits = plot.Axes.GetLimits();
        var startIdx = Math.Max(0, (int)Math.Floor(limits.Left));
        var endIdx = Math.Min(dates.Count - 1, (int)Math.Ceiling(limits.Right));
        var range = endIdx - startIdx;

        int step = range switch
        {
            <= 15 => 1,
            <= 40 => 2,
            <= 80 => 5,
            <= 200 => 10,
            <= 500 => 20,
            _ => 30
        };

        var positions = new List<double>();
        var labels = new List<string>();
        int lastYear = -1;
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (i % step == 0 && i >= 0 && i < dates.Count)
            {
                positions.Add(i);
                var year = dates[i].Year;
                if (year != lastYear)
                {
                    labels.Add(dates[i].ToString("yy/MM-dd"));
                    lastYear = year;
                }
                else
                {
                    labels.Add(dates[i].ToString("MM-dd"));
                }
            }
        }

        if (positions.Count > 0)
        {
            plot.Axes.Bottom.SetTicks(positions.ToArray(), labels.ToArray());
            plot.Axes.Bottom.TickLabelStyle.ForeColor = SPColors.LightGray;
        }
    }

    #endregion

    #region 鼠标交互：缩放 & 平移

    private int DataCount => Math.Max(m_EquityData.Count, m_DrawdownData.Count);

    private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataCount == 0) return;

        var wpfPlot = (ScottPlot.WPF.WpfPlot)sender;
        var pos = e.GetPosition(wpfPlot);

        var limits = EquityPlot.Plot.Axes.GetLimits();
        var range = limits.Right - limits.Left;
        var factor = e.Delta > 0 ? 0.8 : 1.25;
        var newRange = range * factor;

        var mouseRatio = pos.X / wpfPlot.ActualWidth;
        var pivotX = limits.Left + range * mouseRatio;

        var newLeft = pivotX - newRange * mouseRatio;
        var newRight = pivotX + newRange * (1 - mouseRatio);

        var buf = newRange * 0.5;
        var count = DataCount;
        if (newRight - newLeft > count - 1 + 2 * buf)
        {
            newLeft = -buf;
            newRight = count - 1 + buf;
        }

        EquityPlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        DrawdownPlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        RefreshAxisLabels();

        EquityPlot.Refresh();
        DrawdownPlot.Refresh();
        e.Handled = true;
    }

    private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataCount == 0) return;

        EquityTooltip.Visibility = Visibility.Collapsed;
        DrawdownTooltip.Visibility = Visibility.Collapsed;

        m_ActiveChartElement = (FrameworkElement)sender;
        m_LastPanPoint = e.GetPosition(m_ActiveChartElement);
        m_DidPan = false;
        m_ActiveChartElement.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeWE;
        e.Handled = true;
    }

    private void OnChartMouseMove(object sender, MouseEventArgs e)
    {
        if (m_EquityData.Count == 0) return;

        if (m_ActiveChartElement is null && sender is FrameworkElement fe)
        {
            if (fe == EquityPlot)
            {
                UpdateEquityTooltip(e);
            }
            else if (fe == DrawdownPlot)
            {
                UpdateDrawdownTooltip(e);
            }
        }

        if (m_ActiveChartElement is null || sender != m_ActiveChartElement) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (DataCount == 0) return;

        var pos = e.GetPosition(m_ActiveChartElement);
        var pixelDelta = pos.X - m_LastPanPoint.X;
        if (Math.Abs(pixelDelta) < 0.5) return;

        var limits = EquityPlot.Plot.Axes.GetLimits();
        var range = limits.Right - limits.Left;
        var dataPerPixel = range / Math.Max(m_ActiveChartElement.ActualWidth, 1);
        var delta = pixelDelta * dataPerPixel;

        var newLeft = limits.Left - delta;
        var newRight = limits.Right - delta;

        var buf = range * 0.5;
        var count = DataCount;
        if (newLeft < -buf) { newLeft = -buf; newRight = newLeft + range; }
        if (newRight > count - 1 + buf) { newRight = count - 1 + buf; newLeft = newRight - range; }

        EquityPlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        DrawdownPlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        RefreshAxisLabels();

        EquityPlot.Refresh();
        DrawdownPlot.Refresh();

        m_LastPanPoint = pos;
        m_DidPan = true;
        Mouse.OverrideCursor = Cursors.SizeWE;
        e.Handled = true;
    }

    private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
    {
        var didPan = m_DidPan;
        EndPan();
        if (didPan) e.Handled = true;
    }

    private void OnChartMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender == EquityPlot) EquityTooltip.Visibility = Visibility.Collapsed;
        if (sender == DrawdownPlot) DrawdownTooltip.Visibility = Visibility.Collapsed;

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            EndPan();
        }
    }

    private void EndPan()
    {
        if (m_ActiveChartElement is not null)
        {
            m_ActiveChartElement.ReleaseMouseCapture();
            m_ActiveChartElement = null;
        }
        m_DidPan = false;
        Mouse.OverrideCursor = null;
    }

    #endregion

    #region Tooltip

    private void UpdateEquityTooltip(MouseEventArgs e)
    {
        if (m_EquityData.Count == 0)
        {
            EquityTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var pos = e.GetPosition(EquityPlot);
        var limits = EquityPlot.Plot.Axes.GetLimits();
        var dataX = limits.Left + (limits.Right - limits.Left) * (pos.X / EquityPlot.ActualWidth);
        var index = (int)Math.Round(dataX);

        if (index < 0 || index >= m_EquityData.Count)
        {
            EquityTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var pt = m_EquityData[index];
        EqTipDate.Text = pt.Date.ToString("yyyy-MM-dd");
        EqTipEquity.Text = FormatEquity(pt.Equity);

        ShowTooltip(EquityTooltip, EquityPlot, pos);
    }

    private void UpdateDrawdownTooltip(MouseEventArgs e)
    {
        if (m_DrawdownData.Count == 0 || m_EquityData.Count == 0)
        {
            DrawdownTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var pos = e.GetPosition(DrawdownPlot);
        var limits = DrawdownPlot.Plot.Axes.GetLimits();
        var dataX = limits.Left + (limits.Right - limits.Left) * (pos.X / DrawdownPlot.ActualWidth);
        var index = (int)Math.Round(dataX);

        if (index < 0 || index >= m_DrawdownData.Count)
        {
            DrawdownTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var dates = GetDrawdownDates();
        var date = index < dates.Count ? dates[index] : dates[^1];
        DdTipDate.Text = date.ToString("yyyy-MM-dd");
        DdTipDrawdown.Text = $"{m_DrawdownData[index]:F2}%";

        ShowTooltip(DrawdownTooltip, DrawdownPlot, pos);
    }

    private static void ShowTooltip(Border tooltip, FrameworkElement chartElement, Point pos)
    {
        tooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tipW = tooltip.DesiredSize.Width;
        var tipH = tooltip.DesiredSize.Height;

        var halfW = chartElement.ActualWidth / 2;
        var halfH = chartElement.ActualHeight / 2;

        double x = pos.X < halfW ? pos.X + 15 : pos.X - tipW - 15;
        double y = pos.Y < halfH ? pos.Y + 15 : pos.Y - tipH - 15;

        var maxX = Math.Max(2, chartElement.ActualWidth - tipW - 2);
        var maxY = Math.Max(2, chartElement.ActualHeight - tipH - 2);
        x = Math.Clamp(x, 2, maxX);
        y = Math.Clamp(y, 2, maxY);

        Canvas.SetLeft(tooltip, x);
        Canvas.SetTop(tooltip, y);
        tooltip.Visibility = Visibility.Visible;
    }

    private static string FormatEquity(double value)
    {
        if (Math.Abs(value) >= 1_0000_0000)
        {
            return $"{value / 1_0000_0000:F2} 亿";
        }
        if (Math.Abs(value) >= 1_0000)
        {
            return $"{value / 1_0000:F2} 万";
        }
        return $"{value:N2}";
    }

    #endregion
}
