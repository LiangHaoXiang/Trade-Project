namespace TradeDashboard.Models;

public class BacktestResult
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string StrategyName { get; set; } = "";
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double InitialCash { get; set; }
    public double FinalValue { get; set; }
    public double TotalReturnPct { get; set; }
    public double MaxDrawdownPct { get; set; }
    public int TradeCount { get; set; }
    public DateTime RunAt { get; set; }
    public List<Trade> Trades { get; set; } = [];
    public List<EquityPoint> EquityCurve { get; set; } = [];
    public BacktestMetrics Metrics { get; set; } = new();

    public double Pnl => FinalValue - InitialCash;
    public double PnlPct => InitialCash > 0 ? (FinalValue - InitialCash) / InitialCash * 100 : 0;
    public double SharpeRatio => Metrics?.SharpeRatio ?? 0;
    public double WinRate => Metrics?.WinRate ?? 0;
    public double ProfitLossRatio => Metrics?.ProfitLossRatio ?? 0;
    public double AnnualReturnPct => Metrics?.AnnualReturnPct ?? 0;
    public double VolatilityPct => Metrics?.VolatilityPct ?? 0;
}
