using System.Collections.Frozen;

namespace TradeDashboard.Models;

public static class StrategyParameterRegistry
{
    public static readonly FrozenDictionary<string, string> ChineseToEnglishKey = new Dictionary<string, string>
    {
        ["双均线交叉"] = "ma_cross",
        ["均值回归"] = "mean_reversion",
        ["动量选股"] = "momentum",
        ["MACD背离"] = "macd_divergence",
        ["RSI超买超卖"] = "rsi_extreme",
        ["布林带突破"] = "bollinger_breakout",
        ["KDJ金叉死叉"] = "kdj_cross",
        ["神奇九转"] = "td_sequential",
        ["波段趋势"] = "wave_trend",
    }.ToFrozenDictionary();

    public static readonly FrozenDictionary<string, string> EnglishKeyToChinese = ChineseToEnglishKey
        .ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

    public static readonly string[] DisplayNames = [.. ChineseToEnglishKey.Keys];

    public static readonly FrozenDictionary<string, StrategyParameterDefinition[]> Definitions = new Dictionary<string, StrategyParameterDefinition[]>
    {
        ["双均线交叉"] = [
            new() { Key = "short_window", Type = StrategyParameterType.Int, DefaultValue = 5, Label = "短周期均线", Tooltip = "短期移动平均线周期，用于捕捉短期趋势变化" },
            new() { Key = "long_window", Type = StrategyParameterType.Int, DefaultValue = 20, Label = "长周期均线", Tooltip = "长期移动平均线周期，用于判断大趋势方向" },
        ],
        ["均值回归"] = [
            new() { Key = "boll_window", Type = StrategyParameterType.Int, DefaultValue = 20, Label = "布林带窗口", Tooltip = "布林带移动平均线计算周期" },
            new() { Key = "boll_std", Type = StrategyParameterType.Float, DefaultValue = 2.0, Label = "标准差倍数", Tooltip = "布林带上下轨距中轨的标准差倍数" },
            new() { Key = "rsi_window", Type = StrategyParameterType.Int, DefaultValue = 14, Label = "RSI周期", Tooltip = "相对强弱指标(RSI)的计算周期" },
            new() { Key = "oversold", Type = StrategyParameterType.Float, DefaultValue = 30.0, Label = "超卖阈值", Tooltip = "RSI低于此值视为超卖，可能出现买入机会" },
            new() { Key = "overbought", Type = StrategyParameterType.Float, DefaultValue = 70.0, Label = "超买阈值", Tooltip = "RSI高于此值视为超买，可能出现卖出机会" },
        ],
        ["动量选股"] = [
            new() { Key = "lookback", Type = StrategyParameterType.Int, DefaultValue = 20, Label = "回看天数", Tooltip = "计算动量（涨跌幅）的回看天数" },
            new() { Key = "hold_days", Type = StrategyParameterType.Int, DefaultValue = 10, Label = "最大持有天数", Tooltip = "买入后最长持有天数，到期自动卖出" },
        ],
        ["MACD背离"] = [
            new() { Key = "fast", Type = StrategyParameterType.Int, DefaultValue = 12, Label = "快线周期", Tooltip = "MACD快速EMA计算周期" },
            new() { Key = "slow", Type = StrategyParameterType.Int, DefaultValue = 26, Label = "慢线周期", Tooltip = "MACD慢速EMA计算周期" },
            new() { Key = "signal", Type = StrategyParameterType.Int, DefaultValue = 9, Label = "信号线周期", Tooltip = "MACD信号线(DEA)的EMA计算周期" },
            new() { Key = "lookback", Type = StrategyParameterType.Int, DefaultValue = 20, Label = "背离回看窗口", Tooltip = "检测价格与MACD背离时回看的历史窗口长度" },
        ],
        ["RSI超买超卖"] = [
            new() { Key = "rsi_window", Type = StrategyParameterType.Int, DefaultValue = 14, Label = "RSI周期", Tooltip = "相对强弱指标(RSI)的计算周期" },
            new() { Key = "oversold", Type = StrategyParameterType.Float, DefaultValue = 30.0, Label = "超卖阈值", Tooltip = "RSI从下方穿越此值时触发买入信号" },
            new() { Key = "overbought", Type = StrategyParameterType.Float, DefaultValue = 70.0, Label = "超买阈值", Tooltip = "RSI从上方穿越此值时触发卖出信号" },
        ],
        ["布林带突破"] = [
            new() { Key = "window", Type = StrategyParameterType.Int, DefaultValue = 20, Label = "布林带窗口", Tooltip = "布林带移动平均线计算周期" },
            new() { Key = "num_std", Type = StrategyParameterType.Float, DefaultValue = 2.0, Label = "标准差倍数", Tooltip = "布林带上下轨距中轨的标准差倍数" },
        ],
        ["KDJ金叉死叉"] = [
            new() { Key = "n", Type = StrategyParameterType.Int, DefaultValue = 9, Label = "KDJ周期", Tooltip = "KDJ指标中RSV的计算周期" },
            new() { Key = "m1", Type = StrategyParameterType.Int, DefaultValue = 3, Label = "K平滑周期", Tooltip = "K值对RSV的指数移动平均周期" },
            new() { Key = "m2", Type = StrategyParameterType.Int, DefaultValue = 3, Label = "D平滑周期", Tooltip = "D值对K值的指数移动平均周期" },
            new() { Key = "overbought", Type = StrategyParameterType.Float, DefaultValue = 80.0, Label = "超买阈值", Tooltip = "J值高于此值时处于超买区域，金叉信号被过滤" },
            new() { Key = "oversold", Type = StrategyParameterType.Float, DefaultValue = 20.0, Label = "超卖阈值", Tooltip = "J值低于此值时处于超卖区域，死叉信号被过滤" },
        ],
        ["神奇九转"] = [
            new() { Key = "compare_period", Type = StrategyParameterType.Int, DefaultValue = 4, Label = "比较周期", Tooltip = "当日收盘价与前第N日收盘价比较，判断涨跌" },
            new() { Key = "sequential_count", Type = StrategyParameterType.Int, DefaultValue = 9, Label = "序列计数", Tooltip = "连续满足条件的次数达到此值时产生信号（经典为9）" },
        ],
        ["波段趋势"] = [
            new() { Key = "ma_period", Type = StrategyParameterType.Int, DefaultValue = 20, Label = "均线周期", Tooltip = "波段趋势参考的移动平均线周期" },
        ],
    }.ToFrozenDictionary();

    public static Dictionary<string, double> GetDefaultParams(string strategyName)
    {
        if (!Definitions.TryGetValue(strategyName, out var defs))
        {
            return [];
        }

        var result = new Dictionary<string, double>();
        foreach (var d in defs)
        {
            result[d.Key] = d.DefaultValue;
        }
        return result;
    }
}
