using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using TradeDashboard.Services;
using TradeDashboard.ViewModels;
using TradeDashboard.Views;

namespace TradeDashboard;

public partial class App : Application
{
    #region 私有变量

    private ServiceProvider m_ServiceProvider = null!;

    #endregion

    #region 构造函数

    public App()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            MessageBox.Show($"Unhandled:\n{e.Exception}", "Error");
            e.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            MessageBox.Show($"Domain unhandled:\n{e.ExceptionObject}", "Error");
        };
    }

    #endregion

    #region 重写接口

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfigurationService, YamlConfigurationService>();
            services.AddSingleton<IDataService, SqliteDataService>();
            services.AddSingleton<IBacktestService, PythonBacktestService>();
            services.AddSingleton<ILogMonitorService, FileLogMonitorService>();
            services.AddSingleton<ITradingService, SimulatedTradingService>();
            services.AddSingleton<INewsService, PythonNewsService>();

            services.AddTransient<DashboardViewModel>();
            services.AddTransient<MarketDataViewModel>();
            services.AddTransient<TradingViewModel>();
            services.AddTransient<TradeHistoryViewModel>();
            services.AddTransient<EquityCurveViewModel>();
            services.AddTransient<BacktestViewModel>();
            services.AddTransient<NewsViewModel>();
            services.AddTransient<LogViewerViewModel>();
            services.AddTransient<ConfigurationViewModel>();
            services.AddTransient<MainViewModel>();

            m_ServiceProvider = services.BuildServiceProvider();

            var mainWindow = new Views.MainWindow
            {
                DataContext = m_ServiceProvider.GetRequiredService<MainViewModel>()
            };
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Startup:\n{ex}", "Error");
            Shutdown();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        m_ServiceProvider?.Dispose();
        base.OnExit(e);
    }

    #endregion
}
