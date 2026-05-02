using System.IO;
using TradeDashboard.Models;

namespace TradeDashboard.Services;

public class FileLogMonitorService : ILogMonitorService
{
    #region 私有变量

    private FileSystemWatcher? m_Watcher;
    private FileStream? m_Stream;
    private StreamReader? m_Reader;
    private long m_LastPosition;
    private bool m_Disposed;

    #endregion

    #region 公有接口

    public event Action<LogEntry>? NewLogEntry;

    public void StartMonitoring(string logFilePath)
    {
        StopMonitoring();

        var dir = Path.GetDirectoryName(logFilePath);
        if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
        {
            return;
        }

        // Create file if it doesn't exist
        if (!File.Exists(logFilePath))
        {
            Directory.CreateDirectory(dir);
            File.Create(logFilePath).Dispose();
        }

        m_Stream = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        m_Reader = new StreamReader(m_Stream);
        m_Stream.Seek(0, SeekOrigin.End);
        m_LastPosition = m_Stream.Position;

        m_Watcher = new FileSystemWatcher(dir)
        {
            Filter = Path.GetFileName(logFilePath),
            NotifyFilter = NotifyFilters.Size | NotifyFilters.LastWrite,
        };
        m_Watcher.Changed += OnFileChanged;
        m_Watcher.EnableRaisingEvents = true;
    }

    public void StopMonitoring()
    {
        if (m_Watcher != null)
        {
            m_Watcher.EnableRaisingEvents = false;
            m_Watcher.Changed -= OnFileChanged;
            m_Watcher.Dispose();
            m_Watcher = null;
        }
        m_Reader?.Dispose();
        m_Reader = null;
        m_Stream?.Dispose();
        m_Stream = null;
        m_LastPosition = 0;
    }

    public IReadOnlyList<LogEntry> LoadExistingLogs(string logFilePath)
    {
        var entries = new List<LogEntry>();
        if (!File.Exists(logFilePath))
        {
            return entries;
        }

        try
        {
            using var fs = new FileStream(logFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var sr = new StreamReader(fs);
            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    entries.Add(LogEntry.Parse(line));
                }
            }
        }
        catch (IOException) { }

        return entries;
    }

    public void Dispose()
    {
        if (!m_Disposed)
        {
            StopMonitoring();
            m_Disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    #endregion

    #region 私有接口

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        if (m_Stream == null || m_Reader == null)
        {
            return;
        }

        try
        {
            m_Stream.Seek(m_LastPosition, SeekOrigin.Begin);
            string? line;
            while ((line = m_Reader.ReadLine()) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    var entry = LogEntry.Parse(line);
                    NewLogEntry?.Invoke(entry);
                }
            }
            m_LastPosition = m_Stream.Position;
        }
        catch (IOException) { }
    }

    #endregion
}
