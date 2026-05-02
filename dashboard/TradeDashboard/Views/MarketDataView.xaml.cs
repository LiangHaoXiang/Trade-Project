using System.ComponentModel;
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

public partial class MarketDataView : UserControl
{
    private List<DailyBar> m_Bars = [];

    private int m_DragSourceIndex = -1;
    private bool m_IsDragging;
    private Point m_DragStartPoint;

    // Pan state
    private FrameworkElement? m_ActiveChartElement;
    private Point m_LastPanPoint;
    private bool m_DidPan;

    #region 构造函数

    public MarketDataView()
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

        // Hook up mouse events on both plots
        PricePlot.PreviewMouseWheel += OnChartMouseWheel;
        PricePlot.PreviewMouseLeftButtonDown += OnChartMouseDown;
        PricePlot.PreviewMouseMove += OnChartMouseMove;
        PricePlot.PreviewMouseLeftButtonUp += OnChartMouseUp;
        PricePlot.MouseLeave += OnChartMouseLeave;

        VolumePlot.PreviewMouseWheel += OnChartMouseWheel;
        VolumePlot.PreviewMouseLeftButtonDown += OnChartMouseDown;
        VolumePlot.PreviewMouseMove += OnChartMouseMove;
        VolumePlot.PreviewMouseLeftButtonUp += OnChartMouseUp;
        VolumePlot.MouseLeave += OnChartMouseLeave;

        // Disable built-in ScottPlot interactions
        PricePlot.UserInputProcessor.IsEnabled = false;
        VolumePlot.UserInputProcessor.IsEnabled = false;

        FavoriteListBox.PreviewMouseLeftButtonDown += OnFavoritePreviewMouseDown;
        FavoriteListBox.PreviewMouseMove += OnFavoritePreviewMouseMove;
        FavoriteListBox.DragOver += OnFavoriteDragOver;
        FavoriteListBox.Drop += OnFavoriteDrop;
        FavoriteListBox.DragLeave += OnFavoriteDragLeave;
    }

    #endregion

    #region 私有接口

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MarketDataViewModel oldVm)
        {
            oldVm.DataChanged -= OnDataChanged;
        }
        if (e.NewValue is MarketDataViewModel newVm)
        {
            newVm.DataChanged += OnDataChanged;
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is not MarketDataViewModel vm)
        {
            return;
        }
        vm.DataChanged -= OnDataChanged;
        vm.DataChanged += OnDataChanged;
        _ = vm.InitializeAsync();
    }

    #region 自选股拖拽排序

    private void OnFavoritePreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        m_DragSourceIndex = -1;
        m_IsDragging = false;

        var hitResult = VisualTreeHelper.HitTest(FavoriteListBox, e.GetPosition(FavoriteListBox));
        if (hitResult == null) return;

        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)hitResult.VisualHit);
        if (listBoxItem == null) return;

        m_DragSourceIndex = FavoriteListBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        m_DragStartPoint = e.GetPosition(FavoriteListBox);
    }

    private void OnFavoritePreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (m_DragSourceIndex < 0 || e.LeftButton != MouseButtonState.Pressed) return;
        if (m_IsDragging) return;

        var currentPos = e.GetPosition(FavoriteListBox);
        var delta = currentPos - m_DragStartPoint;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        m_IsDragging = true;

        if (DataContext is not MarketDataViewModel vm) return;
        if (m_DragSourceIndex >= vm.FavoriteStocks.Count) return;

        var stock = vm.FavoriteStocks[m_DragSourceIndex];
        var data = new DataObject(DataFormats.StringFormat, stock.Symbol);
        DragDrop.DoDragDrop(FavoriteListBox, data, DragDropEffects.Move);

        m_DragSourceIndex = -1;
        m_IsDragging = false;
    }

    private void OnFavoriteDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.StringFormat)) return;
        e.Effects = DragDropEffects.Move;
        e.Handled = true;

        ClearDragInsertIndicators();

        var hitResult = VisualTreeHelper.HitTest(FavoriteListBox, e.GetPosition(FavoriteListBox));
        if (hitResult == null) return;

        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)hitResult.VisualHit);
        if (listBoxItem == null) return;

        var itemCenter = listBoxItem.TranslatePoint(new Point(listBoxItem.ActualWidth / 2, listBoxItem.ActualHeight / 2), FavoriteListBox);
        var mousePos = e.GetPosition(FavoriteListBox);

        var topInsert = FindChild<Border>(listBoxItem, "TopInsert");
        var bottomInsert = FindChild<Border>(listBoxItem, "BottomInsert");

        if (mousePos.Y < itemCenter.Y)
        {
            if (topInsert != null) topInsert.Visibility = Visibility.Visible;
        }
        else
        {
            if (bottomInsert != null) bottomInsert.Visibility = Visibility.Visible;
        }
    }

    private void OnFavoriteDrop(object sender, DragEventArgs e)
    {
        ClearDragInsertIndicators();

        if (DataContext is not MarketDataViewModel vm) return;
        if (!e.Data.GetDataPresent(DataFormats.StringFormat)) return;

        var targetIndex = GetListBoxItemIndex(FavoriteListBox, e.GetPosition(FavoriteListBox));
        if (targetIndex < 0)
        {
            m_DragSourceIndex = -1;
            m_IsDragging = false;
            return;
        }

        var hitResult = VisualTreeHelper.HitTest(FavoriteListBox, e.GetPosition(FavoriteListBox));
        if (hitResult != null)
        {
            var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)hitResult.VisualHit);
            if (listBoxItem != null)
            {
                var itemCenter = listBoxItem.TranslatePoint(new Point(listBoxItem.ActualWidth / 2, listBoxItem.ActualHeight / 2), FavoriteListBox);
                var mousePos = e.GetPosition(FavoriteListBox);
                if (mousePos.Y >= itemCenter.Y)
                {
                    targetIndex++;
                }
            }
        }

        var fromIndex = m_DragSourceIndex;
        m_DragSourceIndex = -1;
        m_IsDragging = false;

        if (fromIndex < 0 || fromIndex == targetIndex) return;
        if (targetIndex > vm.FavoriteStocks.Count) targetIndex = vm.FavoriteStocks.Count;

        _ = vm.MoveFavoriteAsync(fromIndex, targetIndex);
    }

    private void OnFavoriteDragLeave(object sender, DragEventArgs e)
    {
        ClearDragInsertIndicators();
        m_DragSourceIndex = -1;
        m_IsDragging = false;
    }

    private void ClearDragInsertIndicators()
    {
        for (int i = 0; i < FavoriteListBox.Items.Count; i++)
        {
            var container = FavoriteListBox.ItemContainerGenerator.ContainerFromIndex(i) as ListBoxItem;
            if (container == null) continue;
            var topInsert = FindChild<Border>(container, "TopInsert");
            var bottomInsert = FindChild<Border>(container, "BottomInsert");
            if (topInsert != null) topInsert.Visibility = Visibility.Collapsed;
            if (bottomInsert != null) bottomInsert.Visibility = Visibility.Collapsed;
        }
    }

    private static int GetListBoxItemIndex(ListBox listBox, Point dropPosition)
    {
        var hitResult = VisualTreeHelper.HitTest(listBox, dropPosition);
        if (hitResult == null) return -1;

        var listBoxItem = FindAncestor<ListBoxItem>((DependencyObject)hitResult.VisualHit);
        if (listBoxItem != null)
        {
            return listBox.ItemContainerGenerator.IndexFromContainer(listBoxItem);
        }
        return -1;
    }

    private static T? FindAncestor<T>(DependencyObject current) where T : DependencyObject
    {
        while (current != null)
        {
            if (current is T result) return result;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private static T? FindChild<T>(DependencyObject parent, string name) where T : FrameworkElement
    {
        if (parent == null) return null;
        int childrenCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < childrenCount; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T element && element.Name == name) return element;
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }

    #endregion

    private void OnDataChanged(object? sender, EventArgs e)
    {
        if (DataContext is not MarketDataViewModel vm)
        {
            return;
        }
        var oldCount = m_Bars.Count;
        m_Bars = vm.DataGridData.ToList();
        var prepended = m_Bars.Count - oldCount;

        if (prepended > 0 && oldCount > 0)
        {
            // Save current viewport before re-render
            var limits = PricePlot.Plot.Axes.GetLimits();
            var visibleRange = limits.Right - limits.Left;
            var visibleRight = limits.Right;

            RenderCharts();

            // Shift viewport right by prepended count to maintain visual position
            PricePlot.Plot.Axes.SetLimitsX(visibleRight - visibleRange + prepended, visibleRight + prepended);
            SyncVolumeXAxis();
            UpdateYAxisTicks();
            UpdateXAxisDateLabels();

            PricePlot.Refresh();
            VolumePlot.Refresh();
        }
        else
        {
            RenderCharts();
        }
    }

    #endregion

    #region 图表渲染

    private void RenderCharts()
    {
        if (m_Bars.Count == 0)
        {
            return;
        }
        var vm = (DataContext as MarketDataViewModel)!;

        // ── Price chart ──
        var pricePlot = PricePlot.Plot;
        pricePlot.Clear();

        pricePlot.FigureBackground.Color = SPColor.FromHex("#1a1a2e");
        pricePlot.Axes.Color(SPColor.FromHex("#1a1a2e"));
        pricePlot.Axes.Left.TickLabelStyle.ForeColor = SPColors.LightGray;
        pricePlot.Axes.Left.TickLabelStyle.ForeColor = SPColors.LightGray;
        pricePlot.Axes.Bottom.TickLabelStyle.ForeColor = SPColors.LightGray;
        pricePlot.Axes.Frame(false);
        pricePlot.HideGrid();

        // Candlesticks
        var ohlcs = m_Bars.Select(b => new OHLC(b.Open, b.High, b.Low, b.Close)).ToList();

        var candles = pricePlot.Add.Candlestick(ohlcs);
        candles.RisingColor = SPColor.FromHex("#ef5350");
        candles.FallingColor = SPColor.FromHex("#26a69a");
        candles.Sequential = true;

        // Moving averages
        if (vm.ShowMA5)
        {
            AddMA(pricePlot, 5, SPColor.FromHex("#f9e2af"));
        }
        if (vm.ShowMA20)
        {
            AddMA(pricePlot, 20, SPColor.FromHex("#89b4fa"));
        }

        // Fit to data
        pricePlot.Axes.AutoScale();

        // ── Volume chart ──
        var volPlot = VolumePlot.Plot;
        volPlot.Clear();

        volPlot.FigureBackground.Color = SPColor.FromHex("#1a1a2e");
        volPlot.Axes.Color(SPColor.FromHex("#1a1a2e"));
        volPlot.Axes.Left.TickLabelStyle.ForeColor = SPColors.LightGray;
        volPlot.Axes.Bottom.TickLabelStyle.ForeColor = SPColors.LightGray;
        volPlot.Axes.Frame(false);
        volPlot.HideGrid();

        var volumes = m_Bars.Select(b => b.Volume).ToArray();
        var barPositions = Enumerable.Range(0, m_Bars.Count).Select(i => (double)i).ToArray();
        var bars = volPlot.Add.Bars(barPositions, volumes);
        bars.Color = SPColor.FromHex("#45475a");

        volPlot.Axes.AutoScale();

        // Sync X axis of volume to price
        SyncVolumeXAxis();
        UpdateYAxisTicks();
        UpdateXAxisDateLabels();

        PricePlot.Refresh();
        VolumePlot.Refresh();
    }

    private void AddMA(Plot plot, int period, ScottPlot.Color color)
    {
        if (m_Bars.Count < period)
        {
            return;
        }
        var closes = m_Bars.Select(b => b.Close).ToList();
        var maXs = new List<double>();
        var maYs = new List<double>();
        for (int i = period - 1; i < closes.Count; i++)
        {
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += closes[j];
            }
            maXs.Add(i);
            maYs.Add(sum / period);
        }
        var scatter = plot.Add.Scatter(maXs, maYs);
        scatter.Color = color;
        scatter.LineWidth = 1.5f;
        scatter.MarkerSize = 0;
    }

    #endregion

    #region 轴更新

    private void UpdateYAxisTicks()
    {
        if (m_Bars.Count == 0)
        {
            return;
        }

        var limits = PricePlot.Plot.Axes.GetLimits();
        var startIdx = Math.Max(0, (int)Math.Floor(limits.Left));
        var endIdx = Math.Min(m_Bars.Count - 1, (int)Math.Ceiling(limits.Right));
        if (startIdx > endIdx)
        {
            return;
        }

        double low = double.MaxValue, high = double.MinValue;
        for (int i = startIdx; i <= endIdx; i++)
        {
            if (m_Bars[i].Low < low)
            {
                low = m_Bars[i].Low;
            }
            if (m_Bars[i].High > high)
            {
                high = m_Bars[i].High;
            }
        }

        var vm = DataContext as MarketDataViewModel;
        IncludeMA(startIdx, endIdx, 5, vm?.ShowMA5 == true, ref low, ref high);
        IncludeMA(startIdx, endIdx, 20, vm?.ShowMA20 == true, ref low, ref high);

        if (low == double.MaxValue)
        {
            return;
        }

        var mid = (low + high) / 2;
        var span = Math.Max(high - low, 0.01);
        var padLow = span * 0.05;
        var padHigh = span * 0.05;

        PricePlot.Plot.Axes.SetLimitsY(low - padLow, high + padHigh);
        PricePlot.Plot.Axes.Left.SetTicks(
            new[] { low, mid, high },
            new[] { low.ToString("F2"), mid.ToString("F2"), high.ToString("F2") });
        PricePlot.Plot.Axes.Left.TickLabelStyle.ForeColor = SPColors.LightGray;
    }

    private void UpdateXAxisDateLabels()
    {
        if (m_Bars.Count == 0)
        {
            return;
        }
        var limits = PricePlot.Plot.Axes.GetLimits();
        var startIdx = Math.Max(0, (int)Math.Floor(limits.Left));
        var endIdx = Math.Min(m_Bars.Count - 1, (int)Math.Ceiling(limits.Right));
        var range = endIdx - startIdx;

        // Choose label step based on visible range
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
            if (i % step == 0 && i >= 0 && i < m_Bars.Count)
            {
                positions.Add(i);
                var year = m_Bars[i].Date.Year;
                if (year != lastYear)
                {
                    labels.Add(m_Bars[i].Date.ToString("yy/MM-dd"));
                    lastYear = year;
                }
                else
                {
                    labels.Add(m_Bars[i].Date.ToString("MM-dd"));
                }
            }
        }

        if (positions.Count > 0)
        {
            PricePlot.Plot.Axes.Bottom.SetTicks(positions.ToArray(), labels.ToArray());
            PricePlot.Plot.Axes.Bottom.TickLabelStyle.ForeColor = SPColors.LightGray;
        }
    }

    private void IncludeMA(int startIdx, int endIdx, int period, bool enabled, ref double low, ref double high)
    {
        if (!enabled || m_Bars.Count < period)
        {
            return;
        }
        for (int i = Math.Max(startIdx, period - 1); i <= endIdx; i++)
        {
            double sum = 0;
            for (int j = i - period + 1; j <= i; j++)
            {
                sum += m_Bars[j].Close;
            }
            var ma = sum / period;
            if (ma < low)
            {
                low = ma;
            }
            if (ma > high)
            {
                high = ma;
            }
        }
    }

    private void SyncVolumeXAxis()
    {
        var priceLimits = PricePlot.Plot.Axes.GetLimits();
        VolumePlot.Plot.Axes.SetLimitsX(priceLimits.Left, priceLimits.Right);
    }

    #endregion

    #region 鼠标交互

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
    }

    private void OnChartMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (m_Bars.Count == 0)
        {
            return;
        }

        var wpfPlot = (ScottPlot.WPF.WpfPlot)sender;
        var pos = e.GetPosition(wpfPlot);

        var limits = PricePlot.Plot.Axes.GetLimits();
        var range = limits.Right - limits.Left;
        var factor = e.Delta > 0 ? 0.8 : 1.25;
        var newRange = range * factor;

        // Pivot at mouse X
        var mouseRatio = pos.X / wpfPlot.ActualWidth;
        var pivotX = limits.Left + range * mouseRatio;

        var newLeft = pivotX - newRange * mouseRatio;
        var newRight = pivotX + newRange * (1 - mouseRatio);

        // Clamp to data range with buffer
        var buf = newRange * 0.5;
        if (newRight - newLeft > m_Bars.Count - 1 + 2 * buf)
        {
            newLeft = -buf;
            newRight = m_Bars.Count - 1 + buf;
        }

        PricePlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        SyncVolumeXAxis();
        UpdateYAxisTicks();
        UpdateXAxisDateLabels();

        PricePlot.Refresh();
        VolumePlot.Refresh();
        e.Handled = true;
    }

    private void OnChartMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (m_Bars.Count == 0)
        {
            return;
        }
        CandleTooltip.Visibility = Visibility.Collapsed;
        m_ActiveChartElement = (FrameworkElement)sender;
        m_LastPanPoint = e.GetPosition(m_ActiveChartElement);
        m_DidPan = false;
        m_ActiveChartElement.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeWE;
        e.Handled = true;
    }

    private void OnChartMouseMove(object sender, MouseEventArgs e)
    {
        if (DataContext is not MarketDataViewModel vm)
        {
            return;
        }

        // Tooltip when not panning
        if (m_ActiveChartElement is null && sender is FrameworkElement fe && m_Bars.Count > 0)
        {
            if (fe == PricePlot)
            {
                UpdateTooltip(fe, e);
            }
            else if (fe == VolumePlot)
            {
                UpdateVolumeTooltip(fe, e);
            }
        }

        // Pan logic
        if (m_ActiveChartElement is null || sender != m_ActiveChartElement)
        {
            return;
        }
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }
        if (m_Bars.Count == 0)
        {
            return;
        }

        var pos = e.GetPosition(m_ActiveChartElement);
        var pixelDelta = pos.X - m_LastPanPoint.X;
        if (Math.Abs(pixelDelta) < 0.5)
        {
            return;
        }

        var limits = PricePlot.Plot.Axes.GetLimits();
        var range = limits.Right - limits.Left;
        var dataPerPixel = range / Math.Max(m_ActiveChartElement.ActualWidth, 1);
        var delta = pixelDelta * dataPerPixel;

        var newLeft = limits.Left - delta;
        var newRight = limits.Right - delta;

        // Clamp
        var buf = range * 0.5;
        if (newLeft < -buf) { newLeft = -buf; newRight = newLeft + range; }
        if (newRight > m_Bars.Count - 1 + buf) { newRight = m_Bars.Count - 1 + buf; newLeft = newRight - range; }

        PricePlot.Plot.Axes.SetLimitsX(newLeft, newRight);
        SyncVolumeXAxis();
        UpdateYAxisTicks();
        UpdateXAxisDateLabels();

        PricePlot.Refresh();
        VolumePlot.Refresh();

        // Trigger pagination when approaching left edge
        if (newLeft < 20 && DataContext is MarketDataViewModel vm2 && vm2.HasMoreData && !vm2.IsLoadingMore)
        {
            _ = vm2.LoadMoreHistoryAsync();
        }

        m_LastPanPoint = pos;
        m_DidPan = true;
        Mouse.OverrideCursor = Cursors.SizeWE;
        e.Handled = true;
    }

    private void OnChartMouseUp(object sender, MouseButtonEventArgs e)
    {
        var didPan = m_DidPan;
        EndPan();
        if (didPan)
        {
            e.Handled = true;
        }
    }

    private void OnChartMouseLeave(object sender, MouseEventArgs e)
    {
        if (sender == PricePlot)
        {
            CandleTooltip.Visibility = Visibility.Collapsed;
        }
        if (sender == VolumePlot)
        {
            VolumeTooltip.Visibility = Visibility.Collapsed;
        }
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

    private void UpdateTooltip(FrameworkElement chartElement, MouseEventArgs e)
    {
        var wpfPlot = PricePlot;
        var pos = e.GetPosition(wpfPlot);

        // Convert pixel to data coordinate
        var limits = PricePlot.Plot.Axes.GetLimits();
        var dataX = limits.Left + (limits.Right - limits.Left) * (pos.X / wpfPlot.ActualWidth);
        var index = (int)Math.Round(dataX);

        if (index < 0 || index >= m_Bars.Count)
        {
            CandleTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var bar = m_Bars[index];
        var prevClose = index > 0 ? m_Bars[index - 1].Close : bar.Open;
        var change = bar.Close - prevClose;
        var changePct = prevClose != 0 ? change / prevClose * 100 : 0;

        TipDate.Text = bar.Date.ToString("yyyy-MM-dd");
        TipOpen.Text = $"{bar.Open:F2}";
        TipHigh.Text = $"{bar.High:F2}";
        TipLow.Text = $"{bar.Low:F2}";
        TipClose.Text = $"{bar.Close:F2}";
        TipVolume.Text = $"{bar.Volume:N0}";
        TipChange.Text = $"{change:+0.00;-0.00;0.00} ({changePct:+0.00;-0.00;0.00}%)";
        TipChange.Foreground = change >= 0
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xef, 0x53, 0x50))
            : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x26, 0xa6, 0x9a));

        CandleTooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tipW = CandleTooltip.DesiredSize.Width;
        var tipH = CandleTooltip.DesiredSize.Height;

        var halfW = chartElement.ActualWidth / 2;
        var halfH = chartElement.ActualHeight / 2;

        double x = pos.X < halfW ? pos.X + 15 : pos.X - tipW - 15;
        double y = pos.Y < halfH ? pos.Y + 15 : pos.Y - tipH - 15;

        var maxX = Math.Max(2, chartElement.ActualWidth - tipW - 2);
        var maxY = Math.Max(2, chartElement.ActualHeight - tipH - 2);
        x = Math.Clamp(x, 2, maxX);
        y = Math.Clamp(y, 2, maxY);

        Canvas.SetLeft(CandleTooltip, x);
        Canvas.SetTop(CandleTooltip, y);
        CandleTooltip.Visibility = Visibility.Visible;
    }

    private void UpdateVolumeTooltip(FrameworkElement chartElement, MouseEventArgs e)
    {
        var wpfPlot = VolumePlot;
        var pos = e.GetPosition(wpfPlot);

        var limits = PricePlot.Plot.Axes.GetLimits();
        var dataX = limits.Left + (limits.Right - limits.Left) * (pos.X / wpfPlot.ActualWidth);
        var index = (int)Math.Round(dataX);

        if (index < 0 || index >= m_Bars.Count)
        {
            VolumeTooltip.Visibility = Visibility.Collapsed;
            return;
        }

        var bar = m_Bars[index];

        VolTipDate.Text = bar.Date.ToString("yyyy-MM-dd");
        VolTipVolume.Text = FormatVolume(bar.Volume);
        VolTipAmount.Text = FormatAmount(bar.Amount);

        VolumeTooltip.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var tipW = VolumeTooltip.DesiredSize.Width;
        var tipH = VolumeTooltip.DesiredSize.Height;

        var halfW = chartElement.ActualWidth / 2;
        var halfH = chartElement.ActualHeight / 2;

        double x = pos.X < halfW ? pos.X + 15 : pos.X - tipW - 15;
        double y = pos.Y < halfH ? pos.Y + 15 : pos.Y - tipH - 15;

        var maxX = Math.Max(2, chartElement.ActualWidth - tipW - 2);
        var maxY = Math.Max(2, chartElement.ActualHeight - tipH - 2);
        x = Math.Clamp(x, 2, maxX);
        y = Math.Clamp(y, 2, maxY);

        Canvas.SetLeft(VolumeTooltip, x);
        Canvas.SetTop(VolumeTooltip, y);
        VolumeTooltip.Visibility = Visibility.Visible;
    }

    private static string FormatVolume(double volume)
    {
        if (volume >= 1_0000_0000)
        {
            return $"{volume / 1_0000_0000:F2} 亿股";
        }
        if (volume >= 1_0000)
        {
            return $"{volume / 1_0000:F2} 万股";
        }
        return $"{volume:N0} 股";
    }

    private static string FormatAmount(double amount)
    {
        if (amount >= 1_0000_0000)
        {
            return $"{amount / 1_0000_0000:F2} 亿元";
        }
        if (amount >= 1_0000)
        {
            return $"{amount / 1_0000:F2} 万元";
        }
        return $"{amount:N0} 元";
    }

    #endregion
}
