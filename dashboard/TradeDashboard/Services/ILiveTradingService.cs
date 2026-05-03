using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface ILiveTradingService
{
    Task<LiveBrokerStatus> ConnectAsync();
    Task DisconnectAsync();
    Task<LiveBrokerStatus> GetStatusAsync();
    Task<LiveAccountInfo> GetAccountAsync();
    Task<IReadOnlyList<LivePosition>> GetPositionsAsync();
    Task<IReadOnlyList<LiveEntrust>> GetEntrustsAsync();
    Task<LiveOrderResult> BuyAsync(string symbol, double price, int volume);
    Task<LiveOrderResult> SellAsync(string symbol, double price, int volume);
    Task<LiveOrderResult> CancelAsync(string orderId);
}
