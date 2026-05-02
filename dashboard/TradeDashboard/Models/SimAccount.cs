namespace TradeDashboard.Models;

public class SimAccount
{
    public double InitialCash { get; set; } = 100_000;
    public double Cash { get; set; } = 100_000;
    public double MarketValue { get; set; }
    public double TotalAssets { get; set; } = 100_000;
    public double TotalPnl => TotalAssets - InitialCash;
    public double TotalPnlPct => InitialCash > 0 ? (TotalAssets - InitialCash) / InitialCash * 100 : 0;
    public int PositionCount { get; set; }
    public int TodayOrderCount { get; set; }
}
