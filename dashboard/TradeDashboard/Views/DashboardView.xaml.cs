using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

using ScottPlot;
using SPColor = ScottPlot.Color;
using SPColors = ScottPlot.Colors;

using TradeDashboard.Models;
using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class DashboardView : UserControl
{
    private List<EquityPoint> m_EquityData = [];

    private FrameworkElement? m_ActiveChartElement;
    private Point m_LastPanPoint;
    private bool m_DidPan;

    #region 构造函数

    public DashboardView()
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

        EquityPlot.UserInputProcessor.IsEnabled = false;
    }

    #endregion

    #region 私有接口 - 数据绑定

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

        m_EquityData = vm.EquityPoints;

        EquityPlaceholder.Visibility = Visibility.Hidden;

        var plot = EquityPlot.Plot;
        plot.Clear();
        StylePlot(plot);

        var xs = m_EquityData.Select((_, i) => (double)i).ToArray();
        var ys = m_EquityData.Select(pt => pt.Equity).ToArray();
        var scatter = plot.Add.Scatter(xs, ys);
        scatter.Color = SPColor.FromHex("#89b4fa");
        scatter.LineWidth = 2;
        scatter.MarkerSize = 0;

        plot.Axes.AutoScale();
        EquityPlot.Refresh();

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

    private void RefreshAxisLabels()
    {
        UpdateXAxisDateLabels(EquityPlot, m_EquityData.Select(p => p.Date).ToList());
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

    private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (m_EquityData.Count == 0) return;

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
        var count = m_EquityData.Count;
        if (newRight - newLeft > count - 1 + 2 * buf)
        {
            newLeft = -buf;
            newRight = count - 1 + buf;
        }

        EquityPlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        RefreshAxisLabels();
        EquityPlot.Refresh();
        e.Handled = true;
    }

    private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (m_EquityData.Count == 0) return;

        EquityTooltip.Visibility = Visibility.Collapsed;

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

        if (m_ActiveChartElement is null && sender is FrameworkElement fe && fe == EquityPlot)
        {
            UpdateEquityTooltip(e);
        }

        if (m_ActiveChartElement is null || sender != m_ActiveChartElement) return;
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (m_EquityData.Count == 0) return;

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
        var count = m_EquityData.Count;
        if (newLeft < -buf) { newLeft = -buf; newRight = newLeft + range; }
        if (newRight > count - 1 + buf) { newRight = count - 1 + buf; newLeft = newRight - range; }

        EquityPlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        RefreshAxisLabels();
        EquityPlot.Refresh();

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
