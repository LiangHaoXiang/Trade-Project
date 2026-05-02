namespace TradeDashboard.Models;

public class NewsItem
{
    public string Title { get; set; } = "";
    public string Source { get; set; } = "";
    public string Url { get; set; } = "";
    public string PublishedAt { get; set; } = "";
    public string Summary { get; set; } = "";
    public bool HasUrl => !string.IsNullOrWhiteSpace(Url);
}
