namespace TradeDashboard.Models;

public class LogEntry
{
    public DateTime Timestamp { get; set; }
    public string Level { get; set; } = "INFO";
    public string Source { get; set; } = "";
    public string Message { get; set; } = "";

    public static LogEntry Parse(string line)
    {
        var entry = new LogEntry();
        var parts = line.Split('|', 4);
        if (parts.Length >= 1 && DateTime.TryParse(parts[0], out var ts))
            entry.Timestamp = ts;
        if (parts.Length >= 2)
            entry.Level = parts[1].Trim();
        if (parts.Length >= 3)
            entry.Source = parts[2].Trim();
        if (parts.Length >= 4)
            entry.Message = parts[3].Trim();
        return entry;
    }
}
