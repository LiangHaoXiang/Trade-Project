namespace TradeDashboard.Models;

public class LiveBrokerStatus
{
    public bool Connected { get; set; }
    public string BrokerType { get; set; } = "";
    public string Error { get; set; } = "";
}

public class LiveAccountInfo
{
    public double TotalAssets { get; set; }
    public double Cash { get; set; }
    public double MarketValue { get; set; }
}

public class LivePosition
{
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public int Volume { get; set; }
    public int AvailableVolume { get; set; }
    public double CostPrice { get; set; }
    public double CurrentPrice { get; set; }
    public double Pnl { get; set; }
    public double PnlPct { get; set; }

    public double MarketValue => Volume * CurrentPrice;
}

public class LiveOrderResult
{
    public bool Success { get; set; }
    public string OrderId { get; set; } = "";
    public string Message { get; set; } = "";
    public double FilledPrice { get; set; }
    public int FilledVolume { get; set; }
}

public class LiveEntrust
{
    public string EntrustNo { get; set; } = "";
    public string Symbol { get; set; } = "";
    public string Name { get; set; } = "";
    public string Direction { get; set; } = "";
    public double Price { get; set; }
    public int Volume { get; set; }
    public int FilledVolume { get; set; }
    public string Status { get; set; } = "";
    public string OrderTime { get; set; } = "";
}
