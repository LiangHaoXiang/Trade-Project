namespace TradeDashboard.Models;

public class AppConfiguration
{
    public DatabaseConfig Database { get; set; } = new();
    public DataConfig Data { get; set; } = new();
    public StrategyConfig Strategy { get; set; } = new();
    public RiskConfig Risk { get; set; } = new();
    public TradingConfig Trading { get; set; } = new();
    public BrokerConfig Broker { get; set; } = new();
}

public class DatabaseConfig
{
    public string Path { get; set; } = "data/cache/market.db";
}

public class DataConfig
{
    public string Source { get; set; } = "akshare";
    public string TushareToken { get; set; } = "";
}

public class StrategyConfig
{
    public StrategyDefaults Default { get; set; } = new();
}

public class StrategyDefaults
{
    public int ShortWindow { get; set; } = 5;
    public int LongWindow { get; set; } = 20;
}

public class RiskConfig
{
    public double MaxPositionPct { get; set; } = 0.2;
    public double MaxDailyLossPct { get; set; } = 0.03;
    public double StopLossPct { get; set; } = 0.08;
    public int MaxHoldings { get; set; } = 5;
}

public class TradingConfig
{
    public double CommissionRate { get; set; } = 0.0001;
    public double StampTax { get; set; } = 0.001;
    public double Slippage { get; set; } = 0.001;
}

public class BrokerConfig
{
    public string Type { get; set; } = "sim";
    public string Client { get; set; } = "ths";
    public string ExePath { get; set; } = "";
    public string QmtPath { get; set; } = "";
    public double MaxSingleOrderAmount { get; set; } = 50000;
}
