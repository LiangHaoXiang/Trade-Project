using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class BacktestViewModel : ObservableObject
{
    #region 私有变量

    private readonly IBacktestService m_BacktestService;
    private readonly IDataService m_DataService;

    #endregion

    #region 构造函数

    public BacktestViewModel(IBacktestService backtestService, IDataService dataService)
    {
        m_BacktestService = backtestService;
        m_DataService = dataService;
        _ = LoadFavoriteSymbolsAsync();
        RefreshStrategyParams();
    }

    #endregion

    #region 公有接口

    public async Task LoadLatestResultAsync()
    {
        var latest = await m_DataService.GetLatestBacktestResultAsync();
        if (latest == null)
        {
            return;
        }

        Result = latest;
        ResultPnl = latest.Pnl;
        ResultPnlPct = latest.PnlPct;
        ResultMaxDD = latest.MaxDrawdownPct;
        ResultTradeCount = latest.TradeCount;
        ResultSharpeRatio = latest.SharpeRatio;
        ResultWinRate = latest.WinRate;
        ResultProfitLossRatio = latest.ProfitLossRatio;
        ResultAnnualReturnPct = latest.AnnualReturnPct;
        ResultVolatilityPct = latest.VolatilityPct;
        ResultDrawdownPeriod = latest.Metrics?.MaxDrawdownStart is { Length: > 0 } start
            ? $"{start} ~ {latest.Metrics.MaxDrawdownEnd}"
            : "";

        var trades = await m_DataService.GetTradesAsync(latest.Id);
        ResultTrades = new ObservableCollection<Trade>(trades);

        EquityDataChanged?.Invoke(this, EventArgs.Empty);

        ProgressText = $"Loaded: {latest.PnlPct:+0.00;-0.00}% return, {latest.TradeCount} trades";
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private BacktestParameters _parameters = new();

    partial void OnParametersChanged(BacktestParameters value)
    {
        OnPropertyChanged(nameof(SelectedStrategy));
    }

    public string SelectedStrategy
    {
        get => Parameters.Strategy;
        set
        {
            if (Parameters.Strategy == value) return;
            Parameters.Strategy = value;
            OnPropertyChanged(nameof(SelectedStrategy));
            RefreshStrategyParams();
        }
    }

    [ObservableProperty] private ObservableCollection<StrategyParamEntry> _currentStrategyParams = [];
    [ObservableProperty] private ObservableCollection<StockInfo> _favoriteSymbols = [];
    [ObservableProperty] private ObservableCollection<string> _availableStrategies =
    [
        "双均线交叉",
        "均值回归",
        "动量选股",
        "MACD背离",
        "RSI超买超卖",
        "布林带突破",
        "KDJ金叉死叉",
        "神奇九转",
        "波段趋势",
    ];
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _progressText = "";
    [ObservableProperty] private BacktestResult? _result;
    [ObservableProperty] private ObservableCollection<Trade> _resultTrades = [];
    [ObservableProperty] private double _resultPnl;
    [ObservableProperty] private double _resultPnlPct;
    [ObservableProperty] private double _resultMaxDD;
    [ObservableProperty] private int _resultTradeCount;
    [ObservableProperty] private double _resultSharpeRatio;
    [ObservableProperty] private double _resultWinRate;
    [ObservableProperty] private double _resultProfitLossRatio;
    [ObservableProperty] private double _resultAnnualReturnPct;
    [ObservableProperty] private double _resultVolatilityPct;
    [ObservableProperty] private string _resultDrawdownPeriod = "";

    private void RefreshStrategyParams()
    {
        var defaults = StrategyParameterRegistry.GetDefaultParams(Parameters.Strategy);
        Parameters.StrategyParams = new Dictionary<string, double>(defaults);

        if (!StrategyParameterRegistry.Definitions.TryGetValue(Parameters.Strategy, out var defs))
        {
            CurrentStrategyParams = [];
            return;
        }

        var entries = new ObservableCollection<StrategyParamEntry>();
        foreach (var d in defs)
        {
            var entry = new StrategyParamEntry(d, defaults[d.Key]);
            entry.ValueChanged += (key, val) =>
            {
                Parameters.StrategyParams[key] = val;
            };
            entries.Add(entry);
        }
        CurrentStrategyParams = entries;
    }

    [RelayCommand]
    private async Task RunBacktestAsync()
    {
        IsRunning = true;
        ProgressText = "正在运行回测...";
        try
        {
            var progress = new Progress<string>(msg => ProgressText = msg);
            var result = await m_BacktestService.RunBacktestAsync(Parameters, progress);

            Result = result;
            ResultPnl = result.Pnl;
            ResultPnlPct = result.PnlPct;
            ResultMaxDD = result.MaxDrawdownPct;
            ResultTradeCount = result.TradeCount;
            ResultSharpeRatio = result.SharpeRatio;
            ResultWinRate = result.WinRate;
            ResultProfitLossRatio = result.ProfitLossRatio;
            ResultAnnualReturnPct = result.AnnualReturnPct;
            ResultVolatilityPct = result.VolatilityPct;
            ResultDrawdownPeriod = result.Metrics?.MaxDrawdownStart is { Length: > 0 } s
                ? $"{s} ~ {result.Metrics.MaxDrawdownEnd}"
                : "";
            ResultTrades = new ObservableCollection<Trade>(result.Trades);

            EquityDataChanged?.Invoke(this, EventArgs.Empty);

            ProgressText = $"回测完成: 收益率 {result.Pnl:+0.00;-0.00}%，交易次数 {result.TradeCount}";
        }
        catch (Exception ex)
        {
            ProgressText = $"回测异常: {ex.Message}";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private async Task LoadFavoriteSymbolsAsync()
    {
        var favs = await m_DataService.GetFavoriteStocksAsync();
        FavoriteSymbols = new ObservableCollection<StockInfo>(favs);
    }

    #endregion

    #region 事件

    public event EventHandler? EquityDataChanged;

    #endregion
}

public class StrategyParamEntry : ObservableObject
{
    public StrategyParameterDefinition Definition { get; }

    public string Label => Definition.Label;
    public string Tooltip => Definition.Tooltip;
    public StrategyParameterType ParamType => Definition.Type;

    private string _displayValue;
    public string DisplayValue
    {
        get => _displayValue;
        set
        {
            if (SetProperty(ref _displayValue, value))
            {
                if (Definition.Type == StrategyParameterType.Int && int.TryParse(value, out var intVal))
                {
                    ValueChanged?.Invoke(Definition.Key, intVal);
                }
                else if (Definition.Type == StrategyParameterType.Float && double.TryParse(value, out var floatVal))
                {
                    ValueChanged?.Invoke(Definition.Key, floatVal);
                }
            }
        }
    }

    public event Action<string, double>? ValueChanged;

    public StrategyParamEntry(StrategyParameterDefinition definition, double defaultValue)
    {
        Definition = definition;
        _displayValue = definition.Type == StrategyParameterType.Int
            ? ((int)defaultValue).ToString()
            : defaultValue.ToString();
    }
}
