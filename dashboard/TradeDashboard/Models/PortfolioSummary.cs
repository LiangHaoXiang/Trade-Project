namespace TradeDashboard.Models;

public class PortfolioSummary
{
    public double TotalValue { get; set; }
    public double TotalPnl { get; set; }
    public double PnlPercent { get; set; }
    public int TradeCount { get; set; }
    public double MaxDrawdown { get; set; }
}
