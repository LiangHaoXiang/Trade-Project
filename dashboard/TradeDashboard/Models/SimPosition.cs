namespace TradeDashboard.Models;

public class SimPosition
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public int Volume { get; set; }
    public int AvailableVolume { get; set; }
    public double CostPrice { get; set; }
    public double CurrentPrice { get; set; }

    public double MarketValue => Volume * CurrentPrice;
    public double Pnl => (CurrentPrice - CostPrice) * Volume;
    public double PnlPct => CostPrice > 0 ? (CurrentPrice - CostPrice) / CostPrice * 100 : 0;
}
