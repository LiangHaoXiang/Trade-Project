using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    #region 私有变量

    private readonly IDataService m_DataService;
    private readonly ITradingService m_TradingService;

    #endregion

    #region 公有属性

    public List<EquityPoint> EquityPoints { get; private set; } = [];

    #endregion

    #region 构造函数

    public DashboardViewModel(IDataService dataService, ITradingService tradingService)
    {
        m_DataService = dataService;
        m_TradingService = tradingService;
    }

    #endregion

    #region 数据源切换

    public const string BacktestMode = "回测交易";
    public const string SimMode = "模拟盘交易";

    [ObservableProperty] private string _tradeMode = BacktestMode;

    public bool IsBacktestMode => TradeMode == BacktestMode;

    partial void OnTradeModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsBacktestMode));
        _ = RefreshAsync();
    }

    #endregion

    #region 公有接口

    [RelayCommand]
    public async Task RefreshAsync()
    {
        InteractionLogService.Write("总览", "刷新总览数据");
        IsLoading = true;
        try
        {
            var summary = await m_DataService.GetPortfolioSummaryAsync();
            TotalValue = summary.TotalValue;
            TotalPnl = summary.TotalPnl;
            PnlPercent = summary.PnlPercent;
            TradeCount = summary.TradeCount;
            MaxDrawdown = summary.MaxDrawdown;

            var latest = await m_DataService.GetLatestBacktestResultAsync();
            if (latest != null)
            {
                var equity = await m_DataService.GetEquityCurveAsync(latest.Id);
                EquityPoints = equity.ToList();

                EquityDataChanged?.Invoke(this, EventArgs.Empty);
            }

            if (IsBacktestMode)
            {
                await LoadBacktestRecentTradesAsync();
            }
            else
            {
                LoadSimRecentTrades();
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region 回测交易

    private async Task LoadBacktestRecentTradesAsync()
    {
        var latest = await m_DataService.GetLatestBacktestResultAsync();
        if (latest != null)
        {
            var trades = await m_DataService.GetTradesAsync(latest.Id);
            RecentTrades = new ObservableCollection<Trade>(trades.TakeLast(20));
        }
        RecentSimOrders = [];
    }

    [ObservableProperty] private ObservableCollection<Trade> _recentTrades = [];

    #endregion

    #region 模拟盘交易

    private void LoadSimRecentTrades()
    {
        var orders = m_TradingService.GetOrders();
        RecentSimOrders = new ObservableCollection<SimOrder>(orders.TakeLast(20));
        RecentTrades = [];
    }

    [ObservableProperty] private ObservableCollection<SimOrder> _recentSimOrders = [];

    #endregion

    #region 私有接口

    [ObservableProperty] private double _totalValue;
    [ObservableProperty] private double _totalPnl;
    [ObservableProperty] private double _pnlPercent;
    [ObservableProperty] private int _tradeCount;
    [ObservableProperty] private double _maxDrawdown;
    [ObservableProperty] private bool _isLoading;

    [RelayCommand]
    private void ClearRecentTrades()
    {
        RecentTrades = [];
        RecentSimOrders = [];
        InteractionLogService.Write("总览", "已清空近期交易记录");
    }

    #endregion

    #region 事件

    public event EventHandler? EquityDataChanged;

    #endregion
}
