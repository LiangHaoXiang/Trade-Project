using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Data;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class TradeHistoryViewModel : ObservableObject
{
    #region 私有变量

    private readonly IDataService m_DataService;
    private readonly ITradingService m_TradingService;

    #endregion

    #region 构造函数

    public TradeHistoryViewModel(IDataService dataService, ITradingService tradingService)
    {
        m_DataService = dataService;
        m_TradingService = tradingService;
        _ = LoadDataAsync();
    }

    #endregion

    #region 公有接口

    [RelayCommand]
    public async Task LoadDataAsync()
    {
        InteractionLogService.Write("交易记录", "加载数据");
        if (IsBacktestMode)
        {
            await LoadBacktestTradesAsync();
        }
        else
        {
            LoadSimTrades();
        }
    }

    #endregion

    #region 数据源切换

    public const string BacktestMode = "回测交易";
    public const string SimMode = "模拟盘交易";

    [ObservableProperty] private string _currentMode = BacktestMode;

    public bool IsBacktestMode => CurrentMode == BacktestMode;

    partial void OnCurrentModeChanged(string value)
    {
        OnPropertyChanged(nameof(IsBacktestMode));
        _ = LoadDataAsync();
    }

    #endregion

    #region 回测交易

    [ObservableProperty] private ObservableCollection<Trade> _allTrades = [];

    private async Task LoadBacktestTradesAsync()
    {
        var trades = await m_DataService.GetAllTradesAsync();
        AllTrades = new ObservableCollection<Trade>(trades);
        ApplyBacktestFilter();
    }

    [ObservableProperty] private ListCollectionView? _filteredTradesView;

    [RelayCommand]
    private void ApplyFilter()
    {
        if (IsBacktestMode)
        {
            ApplyBacktestFilter();
        }
        else
        {
            ApplySimFilter();
        }
    }

    private void ApplyBacktestFilter()
    {
        InteractionLogService.Write("交易记录", "筛选回测交易");
        var filtered = AllTrades.AsEnumerable();
        if (!string.IsNullOrEmpty(FilterSymbol))
        {
            filtered = filtered.Where(t => t.Symbol == FilterSymbol);
        }
        if (FilterStartDate.HasValue)
        {
            filtered = filtered.Where(t => t.TradeDate >= FilterStartDate.Value);
        }
        if (FilterEndDate.HasValue)
        {
            filtered = filtered.Where(t => t.TradeDate <= FilterEndDate.Value);
        }
        if (FilterDirection == "买入")
        {
            filtered = filtered.Where(t => t.Direction == "BUY");
        }
        else if (FilterDirection == "卖出")
        {
            filtered = filtered.Where(t => t.Direction == "SELL");
        }

        FilteredTradesView = new ListCollectionView(filtered.ToList());
    }

    #endregion

    #region 模拟盘交易

    [ObservableProperty] private ObservableCollection<SimOrder> _allSimOrders = [];

    private void LoadSimTrades()
    {
        var orders = m_TradingService.GetOrders();
        AllSimOrders = new ObservableCollection<SimOrder>(orders);
        ApplySimFilter();
    }

    [ObservableProperty] private ListCollectionView? _filteredSimOrdersView;

    private void ApplySimFilter()
    {
        InteractionLogService.Write("交易记录", "筛选模拟盘交易");
        var filtered = AllSimOrders.AsEnumerable();
        if (!string.IsNullOrEmpty(FilterSymbol))
        {
            filtered = filtered.Where(t => t.Symbol == FilterSymbol);
        }
        if (FilterStartDate.HasValue)
        {
            filtered = filtered.Where(t => t.OrderTime.Date >= FilterStartDate.Value.Date);
        }
        if (FilterEndDate.HasValue)
        {
            filtered = filtered.Where(t => t.OrderTime.Date <= FilterEndDate.Value.Date);
        }
        if (FilterDirection == "买入")
        {
            filtered = filtered.Where(t => t.Direction == "BUY");
        }
        else if (FilterDirection == "卖出")
        {
            filtered = filtered.Where(t => t.Direction == "SELL");
        }

        FilteredSimOrdersView = new ListCollectionView(filtered.ToList());
    }

    #endregion

    #region 筛选条件

    [ObservableProperty] private string _filterSymbol = "";
    [ObservableProperty] private DateTime? _filterStartDate;
    [ObservableProperty] private DateTime? _filterEndDate;
    [ObservableProperty] private string? _filterDirection;
    [ObservableProperty] private Trade? _selectedTrade;
    [ObservableProperty] private SimOrder? _selectedSimOrder;
    [ObservableProperty] private ObservableCollection<string> _directions = ["全部", "买入", "卖出"];

    [RelayCommand]
    private void ResetFilter()
    {
        InteractionLogService.Write("交易记录", "重置筛选");
        FilterSymbol = "";
        FilterStartDate = null;
        FilterEndDate = null;
        FilterDirection = null;
        ApplyFilter();
    }

    [RelayCommand]
    private void ClearData()
    {
        AllTrades = [];
        AllSimOrders = [];
        FilteredTradesView = null;
        FilteredSimOrdersView = null;
        InteractionLogService.Write("交易记录", "已清空交易记录");
    }

    #endregion
}
