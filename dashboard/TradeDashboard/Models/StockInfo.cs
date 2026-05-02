namespace TradeDashboard.Models;

public class StockInfo
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";

    public string DisplayText => $"{Symbol}  {Name}";
}
