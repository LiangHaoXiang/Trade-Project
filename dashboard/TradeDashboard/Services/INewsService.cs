using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface INewsService
{
    Task<IReadOnlyList<NewsItem>> FetchNewsAsync();
}
