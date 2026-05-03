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

    #endregion

    #region 公有属性

    public List<EquityPoint> EquityPoints { get; private set; } = [];

    #endregion

    #region 构造函数

    public DashboardViewModel(IDataService dataService)
    {
        m_DataService = dataService;
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
                var trades = await m_DataService.GetTradesAsync(latest.Id);
                RecentTrades = new ObservableCollection<Trade>(trades.TakeLast(20));

                var equity = await m_DataService.GetEquityCurveAsync(latest.Id);
                EquityPoints = equity.ToList();

                EquityDataChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private double _totalValue;
    [ObservableProperty] private double _totalPnl;
    [ObservableProperty] private double _pnlPercent;
    [ObservableProperty] private int _tradeCount;
    [ObservableProperty] private double _maxDrawdown;
    [ObservableProperty] private ObservableCollection<Trade> _recentTrades = [];
    [ObservableProperty] private bool _isLoading;

    #endregion

    #region 事件

    public event EventHandler? EquityDataChanged;

    #endregion
}
