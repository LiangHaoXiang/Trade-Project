namespace TradeDashboard.Models;

public enum StrategyParameterType
{
    Int,
    Float,
}

public class StrategyParameterDefinition
{
    public string Key { get; init; } = "";
    public StrategyParameterType Type { get; init; }
    public double DefaultValue { get; init; }
    public string Label { get; init; } = "";
    public string Tooltip { get; init; } = "";
}
