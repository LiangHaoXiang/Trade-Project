using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using TradeDashboard.Models;
using TradeDashboard.Services;

namespace TradeDashboard.ViewModels;

public partial class LogViewerViewModel : ObservableObject, IDisposable
{
    #region 私有变量

    private readonly ILogMonitorService m_LogMonitor;
    private readonly IConfigurationService m_ConfigService;

    #endregion

    #region 构造函数

    public LogViewerViewModel(ILogMonitorService logMonitor, IConfigurationService configService)
    {
        m_LogMonitor = logMonitor;
        m_ConfigService = configService;
        m_LogMonitor.NewLogEntry += OnNewLogEntry;

        var logPath = GetLogPath();
        var existing = m_LogMonitor.LoadExistingLogs(logPath);
        LogEntries = new ObservableCollection<LogEntry>(existing);
    }

    #endregion

    #region 公有接口

    public void Dispose()
    {
        m_LogMonitor.NewLogEntry -= OnNewLogEntry;
        m_LogMonitor.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion

    #region 私有接口

    [ObservableProperty] private ObservableCollection<LogEntry> _logEntries = [];
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedLevel = "ALL";
    [ObservableProperty] private bool _autoScroll = true;
    [ObservableProperty] private bool _isMonitoring;
    [ObservableProperty] private LogEntry? _selectedLogEntry;
    [ObservableProperty] private ObservableCollection<string> _logLevels = ["ALL", "DEBUG", "INFO", "WARNING", "ERROR"];

    private string GetLogPath()
    {
        return Path.Combine(m_ConfigService.GetProjectRootPath(), "data", "logs", "trade.log");
    }

    [RelayCommand]
    private void ToggleMonitoring()
    {
        if (IsMonitoring)
        {
            m_LogMonitor.StopMonitoring();
            IsMonitoring = false;
        }
        else
        {
            var logPath = GetLogPath();
            m_LogMonitor.StartMonitoring(logPath);
            IsMonitoring = true;
        }
    }

    [RelayCommand]
    private void Clear()
    {
        LogEntries.Clear();
    }

    [RelayCommand]
    private void LoadFromFile()
    {
        var logPath = GetLogPath();
        var existing = m_LogMonitor.LoadExistingLogs(logPath);
        LogEntries = new ObservableCollection<LogEntry>(existing);
    }

    private void OnNewLogEntry(LogEntry entry)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            LogEntries.Add(entry);
            while (LogEntries.Count > 10000)
            {
                LogEntries.RemoveAt(0);
            }
        });
    }

    #endregion
}
