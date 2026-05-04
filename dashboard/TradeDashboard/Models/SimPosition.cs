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
    public double MarketValueOverride { get; set; }
    public double PnlOverride { get; set; }
    public double PnlPctOverride { get; set; }

    public double MarketValue => MarketValueOverride > 0 ? MarketValueOverride : Volume * CurrentPrice;
    public double Pnl => PnlOverride != 0 ? PnlOverride : (CurrentPrice - CostPrice) * Volume;
    public double PnlPct => PnlPctOverride != 0 ? PnlPctOverride : (CostPrice > 0 ? (CurrentPrice - CostPrice) / CostPrice * 100 : 0);
}
