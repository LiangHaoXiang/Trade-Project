using System.Collections.Generic;

namespace TradeDashboard.Models;

public class BacktestMetrics
{
    public double AnnualReturnPct { get; set; }
    public double SharpeRatio { get; set; }
    public double MaxDrawdownPct { get; set; }
    public string MaxDrawdownStart { get; set; } = "";
    public string MaxDrawdownEnd { get; set; } = "";
    public double WinRate { get; set; }
    public double ProfitLossRatio { get; set; }
    public int TotalTradingDays { get; set; }
    public double AvgDailyReturnPct { get; set; }
    public double VolatilityPct { get; set; }
    public Dictionary<string, double> MonthlyReturns { get; set; } = [];
}
