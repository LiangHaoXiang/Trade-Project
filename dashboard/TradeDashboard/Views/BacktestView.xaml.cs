using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

using ScottPlot;
using SPColor = ScottPlot.Color;
using SPColors = ScottPlot.Colors;

using TradeDashboard.Models;
using TradeDashboard.ViewModels;

namespace TradeDashboard.Views;

public partial class BacktestView : UserControl
{
    private List<EquityPoint> m_EquityData = [];

    #region 构造函数

    public BacktestView()
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

        EquityPlot.MouseMove += OnEquityPlotMouseMove;
        EquityPlot.MouseLeave += OnEquityPlotMouseLeave;

        SymbolComboBox.AddHandler(TextBoxBase.TextChangedEvent, new TextChangedEventHandler(OnSymbolTextChanged));
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

    private void OnSymbolTextChanged(object sender, TextChangedEventArgs e)
    {
        if (DataContext is not BacktestViewModel vm) return;
        if (SymbolComboBox.IsDropDownOpen) return;

        var text = SymbolComboBox.Text?.Trim() ?? "";
        if (text.Contains(' '))
        {
            text = text.Split(' ', 2)[0].Trim();
        }
        vm.Parameters.Symbol = text;
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

        m_EquityData = equity;

        var plot = EquityPlot.Plot;
        plot.Clear();

        plot.FigureBackground.Color = SPColor.FromHex("#1a1a2e");
        plot.Axes.Color(SPColor.FromHex("#1a1a2e"));
        plot.Axes.Left.TickLabelStyle.ForeColor = SPColors.LightGray;
        plot.Axes.Bottom.TickLabelStyle.ForeColor = SPColors.LightGray;
        plot.Axes.Frame(false);
        plot.HideGrid();

        var xs = equity.Select((_, i) => (double)i).ToArray();
        var ys = equity.Select(pt => pt.Equity).ToArray();
        var scatter = plot.Add.Scatter(xs, ys);
        scatter.Color = SPColor.FromHex("#89b4fa");
        scatter.LineWidth = 2;
        scatter.MarkerSize = 0;

        plot.Axes.AutoScale();
        UpdateXAxisDateLabels();
        EquityPlot.Refresh();
    }

    #endregion

    #region 图表渲染

    private void UpdateXAxisDateLabels()
    {
        if (m_EquityData.Count == 0) return;

        var plot = EquityPlot.Plot;
        var limits = plot.Axes.GetLimits();
        var startIdx = Math.Max(0, (int)Math.Floor(limits.Left));
        var endIdx = Math.Min(m_EquityData.Count - 1, (int)Math.Ceiling(limits.Right));
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
            if (i % step == 0 && i >= 0 && i < m_EquityData.Count)
            {
                positions.Add(i);
                var year = m_EquityData[i].Date.Year;
                if (year != lastYear)
                {
                    labels.Add(m_EquityData[i].Date.ToString("yy/MM-dd"));
                    lastYear = year;
                }
                else
                {
                    labels.Add(m_EquityData[i].Date.ToString("MM-dd"));
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

    #region 鼠标交互 & Tooltip

    private void OnEquityPlotMouseMove(object sender, MouseEventArgs e)
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

        EquityTooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tipW = EquityTooltip.DesiredSize.Width;
        var tipH = EquityTooltip.DesiredSize.Height;

        var halfW = EquityPlot.ActualWidth / 2;
        var halfH = EquityPlot.ActualHeight / 2;

        double x = pos.X < halfW ? pos.X + 15 : pos.X - tipW - 15;
        double y = pos.Y < halfH ? pos.Y + 15 : pos.Y - tipH - 15;

        var maxX = Math.Max(2, EquityPlot.ActualWidth - tipW - 2);
        var maxY = Math.Max(2, EquityPlot.ActualHeight - tipH - 2);
        x = Math.Clamp(x, 2, maxX);
        y = Math.Clamp(y, 2, maxY);

        Canvas.SetLeft(EquityTooltip, x);
        Canvas.SetTop(EquityTooltip, y);
        EquityTooltip.Visibility = Visibility.Visible;
    }

    private void OnEquityPlotMouseLeave(object sender, MouseEventArgs e)
    {
        EquityTooltip.Visibility = Visibility.Collapsed;
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
