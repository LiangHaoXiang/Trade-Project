namespace TradeDashboard.Models;

public class BacktestParameters
{
    public string Symbol { get; set; } = "000001";
    public DateTime StartDate { get; set; } = new(2024, 1, 1);
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string Strategy { get; set; } = "双均线交叉";
    public double InitialCash { get; set; } = 100_000;
    public Dictionary<string, double> StrategyParams { get; set; } = [];
}
