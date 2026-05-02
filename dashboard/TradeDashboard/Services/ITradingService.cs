using TradeDashboard.Models;

namespace TradeDashboard.Services;

public interface ITradingService
{
    SimAccount GetAccount();
    IReadOnlyList<SimPosition> GetPositions();
    IReadOnlyList<SimOrder> GetOrders();
    IReadOnlyList<SimOrder> GetTodayOrders();
    SimOrder Buy(string symbol, double price, int volume, string reason = "");
    SimOrder Sell(string symbol, double price, int volume, string reason = "");
    void UpdatePrices(Dictionary<string, double> prices);
    void Reset();
}
