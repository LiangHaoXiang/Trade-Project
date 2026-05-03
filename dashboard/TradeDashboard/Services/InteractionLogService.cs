using System;

namespace TradeDashboard.Services;

public static class InteractionLogService
{
    private static string s_LastLog = "";
    private static string s_PendingLog = "";

    public static event Action<string>? LogChanged;

    public static string LastLog => s_LastLog;

    public static void Write(string source, string message)
    {
        var timestamp = DateTime.Now.ToString("HH:mm:ss");
        s_LastLog = $"[{source}] {timestamp} {message}";
        s_PendingLog = s_LastLog;
        LogChanged?.Invoke(s_LastLog);
    }

    public static string ConsumePending()
    {
        var log = s_PendingLog;
        s_PendingLog = "";
        return log;
    }
}
