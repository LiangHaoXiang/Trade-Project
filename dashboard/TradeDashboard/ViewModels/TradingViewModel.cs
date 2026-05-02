using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class TradingViewModel : ObservableObject
{
    #region 私有变量

    private readonly ITradingService m_TradingService;
    private readonly IDataService m_DataService;

    #endregion

    #region 构造函数

    public TradingViewModel(ITradingService tradingService, IDataService dataService)
    {
        m_TradingService = tradingService;
        m_DataService = dataService;
    }

    #endregion

    #region 公有接口

    public async Task LoadDataAsync()
    {
        await RefreshAsync();
    }

    [RelayCommand]
    public void Refresh()
    {
        var account = m_TradingService.GetAccount();
        AccountCash = account.Cash;
        AccountTotalAssets = account.TotalAssets;
        AccountMarketValue = account.MarketValue;
        AccountPnl = account.TotalPnl;
        AccountPnlPct = account.TotalPnlPct;
        AccountPositionCount = account.PositionCount;

        Positions = new ObservableCollection<SimPosition>(m_TradingService.GetPositions());
        TodayOrders = new ObservableCollection<SimOrder>(m_TradingService.GetTodayOrders());

        // 可用股票列表
        var symbols = m_DataService.GetAvailableSymbolsAsync().Result;
        AvailableSymbols = new ObservableCollection<string>(symbols);
    }

    public async Task RefreshAsync()
    {
        await Task.Run(Refresh);
    }

    #endregion

    #region 私有接口 - 下单参数

    [ObservableProperty] private ObservableCollection<string> _availableSymbols = [];
    [ObservableProperty] private string _orderSymbol = "000001";
    [ObservableProperty] private string _orderDirection = "BUY";
    [ObservableProperty] private double _orderPrice = 10.00;
    [ObservableProperty] private int _orderVolume = 100;
    [ObservableProperty] private string _orderMessage = "";
    [ObservableProperty] private bool _isOrderBusy;

    #endregion

    #region 私有接口 - 账户概览

    [ObservableProperty] private double _accountCash;
    [ObservableProperty] private double _accountTotalAssets;
    [ObservableProperty] private double _accountMarketValue;
    [ObservableProperty] private double _accountPnl;
    [ObservableProperty] private double _accountPnlPct;
    [ObservableProperty] private int _accountPositionCount;

    #endregion

    #region 私有接口 - 数据列表

    [ObservableProperty] private ObservableCollection<SimPosition> _positions = [];
    [ObservableProperty] private ObservableCollection<SimOrder> _todayOrders = [];
    [ObservableProperty] private SimPosition? _selectedPosition;

    #endregion

    partial void OnSelectedPositionChanged(SimPosition? value)
    {
        if (value is not null)
        {
            OrderSymbol = value.Symbol;
            OrderDirection = "SELL";
            OrderPrice = value.CurrentPrice;
            OrderVolume = value.AvailableVolume;
        }
    }

    [RelayCommand]
    private void SubmitOrder()
    {
        IsOrderBusy = true;
        try
        {
            SimOrder result;
            if (OrderDirection == "BUY")
            {
                result = m_TradingService.Buy(OrderSymbol, OrderPrice, OrderVolume, "手动下单");
            }
            else
            {
                result = m_TradingService.Sell(OrderSymbol, OrderPrice, OrderVolume, "手动下单");
            }

            if (result.Status == "filled")
            {
                var dirText = result.Direction == "BUY" ? "买入" : "卖出";
                OrderMessage = $"{dirText} {result.Symbol} {result.Volume}@{result.Price:N2} 成交";
            }
            else
            {
                OrderMessage = $"委托被拒: {result.Reason}";
            }

            Refresh();
        }
        catch (Exception ex)
        {
            OrderMessage = $"下单异常: {ex.Message}";
        }
        finally
        {
            IsOrderBusy = false;
        }
    }

    [RelayCommand]
    private void ResetAccount()
    {
        m_TradingService.Reset();
        OrderMessage = "模拟账户已重置";
        Refresh();
    }

    [RelayCommand]
    private void FillMaxVolume()
    {
        if (OrderDirection == "BUY" && AccountCash > 0 && OrderPrice > 0)
        {
            var maxVol = (int)(AccountCash * 0.95 / OrderPrice / 100) * 100;
            OrderVolume = Math.Max(maxVol, 100);
        }
        else if (OrderDirection == "SELL" && SelectedPosition is not null)
        {
            OrderVolume = SelectedPosition.AvailableVolume;
        }
    }
}
