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
    private readonly ILiveTradingService? m_LiveTradingService;
    private readonly IConfigurationService m_ConfigService;

    #endregion

    #region 状态日志

    public event Action<string>? StatusLog;

    public void Log(string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        var entry = $"[交易] {timestamp} {message}";
        StatusLog?.Invoke(entry);
        InteractionLogService.Write("交易", message);
    }

    #endregion

    #region 构造函数

    public TradingViewModel(
        ITradingService tradingService,
        IDataService dataService,
        IConfigurationService configService,
        ILiveTradingService? liveTradingService = null)
    {
        m_TradingService = tradingService;
        m_DataService = dataService;
        m_ConfigService = configService;
        m_LiveTradingService = liveTradingService;

        var config = configService.Load();
        IsLiveMode = config.Broker.Type != "sim";

        Log($"TradingViewModel 构造完成 IsLiveMode={IsLiveMode}");
    }

    #endregion

    #region 公有接口

    public async Task LoadDataAsync()
    {
        Log($"LoadDataAsync 开始 IsLiveMode={IsLiveMode}");
        if (IsLiveMode)
        {
            await RefreshLiveAsync();
        }
        else
        {
            await RefreshAsync();
        }
        Log("LoadDataAsync 完成");
    }

    [RelayCommand]
    public void Refresh()
    {
        if (IsLiveMode)
        {
            _ = RefreshLiveAsync();
            return;
        }

        try
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

            _ = LoadSymbolsAsync();
        }
        catch (Exception ex)
        {
            OrderMessage = $"刷新数据异常: {ex.Message}";
        }
    }

    private async Task LoadSymbolsAsync()
    {
        try
        {
            var symbols = await m_DataService.GetAvailableSymbolsAsync();
            AvailableSymbols = new ObservableCollection<string>(symbols);
        }
        catch (Exception ex)
        {
            OrderMessage = $"加载股票列表失败: {ex.Message}";
        }
    }

    public async Task RefreshAsync()
    {
        await Task.Run(Refresh);
    }

    #endregion

    #region 私有接口 - 模式切换

    [ObservableProperty] private bool _isLiveMode;

    partial void OnIsLiveModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ModeLabel));
        OnPropertyChanged(nameof(CanReset));
        if (value && m_LiveTradingService != null)
        {
            _ = ConnectLiveAsync();
        }
    }

    public string ModeLabel => IsLiveMode ? "实盘模式" : "模拟模式";
    public bool CanReset => !IsLiveMode;

    #endregion

    #region 私有接口 - 实盘连接

    [ObservableProperty] private bool _isBrokerConnected;
    [ObservableProperty] private string _brokerStatusText = "未连接";

    [RelayCommand]
    private async Task ConnectLiveAsync()
    {
        if (m_LiveTradingService == null)
        {
            BrokerStatusText = "实盘服务未配置";
            return;
        }

        try
        {
            BrokerStatusText = "连接中...";
            var status = await m_LiveTradingService.ConnectAsync();
            IsBrokerConnected = status.Connected;
            BrokerStatusText = status.Connected ? $"已连接 ({status.BrokerType})" : "连接失败";

            if (status.Connected)
            {
                await RefreshLiveAsync();
            }
        }
        catch (Exception ex)
        {
            IsBrokerConnected = false;
            BrokerStatusText = $"连接异常: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DisconnectLiveAsync()
    {
        if (m_LiveTradingService == null) return;

        try
        {
            await m_LiveTradingService.DisconnectAsync();
            IsBrokerConnected = false;
            BrokerStatusText = "已断开";
        }
        catch (Exception ex)
        {
            BrokerStatusText = $"断开异常: {ex.Message}";
        }
    }

    private async Task RefreshLiveAsync()
    {
        if (m_LiveTradingService == null || !IsBrokerConnected) return;

        try
        {
            var account = await m_LiveTradingService.GetAccountAsync();
            AccountCash = account.Cash;
            AccountTotalAssets = account.TotalAssets;
            AccountMarketValue = account.MarketValue;
            AccountPnl = 0;
            AccountPnlPct = 0;
            AccountPositionCount = 0;

            var positions = await m_LiveTradingService.GetPositionsAsync();
            var simPositions = positions.Select(p => new SimPosition
            {
                Symbol = p.Symbol,
                Name = p.Name,
                Volume = p.Volume,
                AvailableVolume = p.AvailableVolume,
                CostPrice = p.CostPrice,
                CurrentPrice = p.CurrentPrice,
            }).ToList();

            Positions = new ObservableCollection<SimPosition>(simPositions);
            AccountPositionCount = simPositions.Count;

            var entrusts = await m_LiveTradingService.GetEntrustsAsync();
            var simOrders = entrusts.Select((e, i) => new SimOrder
            {
                Id = i + 1,
                Symbol = e.Symbol,
                Direction = e.Direction.Contains("买") ? "BUY" : "SELL",
                Price = e.Price,
                Volume = e.Volume,
                OrderTime = DateTime.Now,
                Status = e.Status.Contains("成") ? "filled" : "pending",
                Reason = e.Status,
            }).ToList();

            TodayOrders = new ObservableCollection<SimOrder>(simOrders);

            var symbols = await m_DataService.GetAvailableSymbolsAsync();
            AvailableSymbols = new ObservableCollection<string>(symbols);
        }
        catch (Exception ex)
        {
            OrderMessage = $"刷新实盘数据异常: {ex.Message}";
        }
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
    private async Task SubmitOrderAsync()
    {
        Log($"SubmitOrderAsync 触发 IsOrderBusy={IsOrderBusy} IsLiveMode={IsLiveMode}");

        if (IsOrderBusy)
        {
            Log("SubmitOrderAsync 被拦截: IsOrderBusy=true");
            return;
        }

        IsOrderBusy = true;
        Log($"下单参数: Symbol={OrderSymbol} Dir={OrderDirection} Price={OrderPrice} Vol={OrderVolume}");
        try
        {
            if (string.IsNullOrWhiteSpace(OrderSymbol))
            {
                OrderMessage = "请输入股票代码";
                Log("验证失败: 股票代码为空");
                return;
            }

            if (OrderPrice <= 0)
            {
                OrderMessage = "价格必须大于0";
                Log("验证失败: 价格<=0");
                return;
            }

            if (OrderVolume <= 0 || OrderVolume % 100 != 0)
            {
                OrderMessage = "数量必须为正数且是100的整数倍";
                Log($"验证失败: 数量={OrderVolume}");
                return;
            }

            if (IsLiveMode)
            {
                await SubmitLiveOrderAsync();
            }
            else
            {
                SubmitSimOrder();
            }
        }
        catch (Exception ex)
        {
            OrderMessage = $"下单异常: {ex.Message}";
            Log($"下单异常: {ex}");
        }
        finally
        {
            IsOrderBusy = false;
            Log("SubmitOrderAsync 结束");
        }
    }

    private void SubmitSimOrder()
    {
        try
        {
            Log($"SubmitSimOrder 开始 Buy={OrderDirection == "BUY"}");
            SimOrder result;
            if (OrderDirection == "BUY")
            {
                result = m_TradingService.Buy(OrderSymbol, OrderPrice, OrderVolume, "手动下单");
            }
            else
            {
                result = m_TradingService.Sell(OrderSymbol, OrderPrice, OrderVolume, "手动下单");
            }

            Log($"SubmitSimOrder 结果: Status={result.Status} Reason={result.Reason}");

            if (result.Status == "filled")
            {
                var dirText = result.Direction == "BUY" ? "买入" : "卖出";
                OrderMessage = $"{dirText} {result.Symbol} {result.Volume}@{result.Price:N2} 成交";
            }
            else
            {
                OrderMessage = $"委托被拒: {result.Reason}";
            }

            _ = RefreshAsync();
        }
        catch (Exception ex)
        {
            OrderMessage = $"模拟下单失败: {ex.Message}";
            Log($"SubmitSimOrder 异常: {ex}");
        }
    }

    private async Task SubmitLiveOrderAsync()
    {
        if (m_LiveTradingService == null)
        {
            OrderMessage = "实盘服务未配置";
            return;
        }

        var config = m_ConfigService.Load();
        var orderAmount = OrderPrice * OrderVolume;
        if (orderAmount > config.Broker.MaxSingleOrderAmount)
        {
            OrderMessage = $"单笔金额 {orderAmount:N0} 超过限制 {config.Broker.MaxSingleOrderAmount:N0}";
            return;
        }

        LiveOrderResult result;
        if (OrderDirection == "BUY")
        {
            result = await m_LiveTradingService.BuyAsync(OrderSymbol, OrderPrice, OrderVolume);
        }
        else
        {
            result = await m_LiveTradingService.SellAsync(OrderSymbol, OrderPrice, OrderVolume);
        }

        if (result.Success)
        {
            var dirText = OrderDirection == "BUY" ? "买入" : "卖出";
            OrderMessage = $"{dirText}委托已提交 委托号={result.OrderId}";
        }
        else
        {
            OrderMessage = $"委托失败: {result.Message}";
        }

        await RefreshLiveAsync();
    }

    [RelayCommand]
    private void ResetAccount()
    {
        Log("重置模拟账户");
        if (IsLiveMode) return;
        m_TradingService.Reset();
        OrderMessage = "模拟账户已重置";
        Refresh();
    }

    [RelayCommand]
    private void FillMaxVolume()
    {
        Log($"FillMaxVolume Dir={OrderDirection} Cash={AccountCash}");
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
