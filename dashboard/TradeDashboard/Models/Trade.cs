namespace TradeDashboard.Models;

public class Trade
{
    public int Id { get; set; }
    public int BacktestRunId { get; set; }
    public string Symbol { get; set; } = "";
    public string Direction { get; set; } = ""; // "BUY" or "SELL"
    public double Price { get; set; }
    public int Volume { get; set; }
    public DateTime TradeDate { get; set; }
    public string Reason { get; set; } = "";

    public double Amount => Price * Volume;
}
