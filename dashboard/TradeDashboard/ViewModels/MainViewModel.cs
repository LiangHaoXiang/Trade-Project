using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class MainViewModel : ObservableObject
{
    #region 私有变量

    private readonly IConfigurationService m_ConfigService;

    #endregion

    #region 公有属性

    public DashboardViewModel Dashboard { get; }
    public MarketDataViewModel MarketData { get; }
    public TradingViewModel Trading { get; }
    public TradeHistoryViewModel TradeHistory { get; }
    public EquityCurveViewModel EquityCurve { get; }
    public BacktestViewModel Backtest { get; }
    public NewsViewModel News { get; }
    public LogViewerViewModel LogViewer { get; }
    public ConfigurationViewModel Configuration { get; }

    #endregion

    #region 构造函数

    public MainViewModel(
        IConfigurationService configService,
        DashboardViewModel dashboard,
        MarketDataViewModel marketData,
        TradingViewModel trading,
        TradeHistoryViewModel tradeHistory,
        EquityCurveViewModel equityCurve,
        BacktestViewModel backtest,
        NewsViewModel news,
        LogViewerViewModel logViewer,
        ConfigurationViewModel configuration)
    {
        m_ConfigService = configService;
        Dashboard = dashboard;
        MarketData = marketData;
        Trading = trading;
        TradeHistory = tradeHistory;
        EquityCurve = equityCurve;
        Backtest = backtest;
        News = news;
        LogViewer = logViewer;
        Configuration = configuration;

        _ = InitializeAsync();
    }

    #endregion

    #region 公有接口

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = "Refreshing...";
        try
        {
            await Dashboard.RefreshAsync();
            await MarketData.InitializeAsync();
            await EquityCurve.LoadLatestAsync();
            StatusMessage = "Refreshed";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private int _selectedTabIndex;
    [ObservableProperty] private string _statusMessage = "Ready";
    [ObservableProperty] private string _statusBarText = "Trade Dashboard v1.0";

    private async Task InitializeAsync()
    {
        StatusBarText = "检查数据更新...";
        try
        {
            await CheckDataUpdateAsync();
        }
        catch
        {
            // 非阻塞：更新失败不阻止启动
        }

        StatusBarText = "加载数据...";
        try
        {
            await Task.WhenAll(
                Dashboard.RefreshAsync(),
                MarketData.InitializeAsync(),
                Trading.LoadDataAsync(),
                EquityCurve.LoadLatestAsync(),
                Backtest.LoadLatestResultAsync(),
                News.RefreshAsync()
            );
            News.StartAutoRefresh();
            StatusBarText = "交易仪表盘 v1.0";
        }
        catch (Exception ex)
        {
            StatusBarText = $"加载异常: {ex.Message}";
        }
    }

    private async Task CheckDataUpdateAsync()
    {
        var projectRoot = m_ConfigService.GetProjectRootPath();
        var pythonExe = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        if (!File.Exists(pythonExe))
        {
            return;
        }
        var mainPy = Path.Combine(projectRoot, "main.py");

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{mainPy}\" check-update",
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = Process.Start(psi);
        if (process is null)
        {
            return;
        }
        await process.WaitForExitAsync();
    }

    #endregion
}
