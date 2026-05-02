using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface ILogMonitorService : IDisposable
{
    event Action<LogEntry>? NewLogEntry;
    void StartMonitoring(string logFilePath);
    void StopMonitoring();
    IReadOnlyList<LogEntry> LoadExistingLogs(string logFilePath);
}
