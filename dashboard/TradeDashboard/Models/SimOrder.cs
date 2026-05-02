namespace TradeDashboard.Models;

public class SimOrder
{
    public int Id { get; set; }
    public string Symbol { get; set; } = "";
    public string Direction { get; set; } = "";
    public double Price { get; set; }
    public int Volume { get; set; }
    public DateTime OrderTime { get; set; }
    public string Status { get; set; } = "";
    public string Reason { get; set; } = "";

    public double Amount => Price * Volume;
}
