using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using TradeDashboard.Models;

namespace TradeDashboard.Services;

public class PythonBacktestService : IBacktestService
{
    #region 私有变量

    private readonly IConfigurationService m_ConfigService;
    private readonly IDataService m_DataService;

    #endregion

    #region 构造函数

    public PythonBacktestService(IConfigurationService configService, IDataService dataService)
    {
        m_ConfigService = configService;
        m_DataService = dataService;
    }

    #endregion

    #region 公有接口

    public async Task<BacktestResult> RunBacktestAsync(BacktestParameters parameters, IProgress<string>? progress = null, CancellationToken ct = default)
    {
        var projectRoot = m_ConfigService.GetProjectRootPath();
        var pythonExe = FindPythonExe(projectRoot);
        var mainPy = Path.Combine(projectRoot, "main.py");

        var strategyName = parameters.Strategy switch
        {
            "双均线交叉" => "ma_cross",
            "均值回归" => "mean_reversion",
            "动量选股" => "momentum",
            "MACD背离" => "macd_divergence",
            "RSI超买超卖" => "rsi_extreme",
            "布林带突破" => "bollinger_breakout",
            "KDJ金叉死叉" => "kdj_cross",
            "神奇九转" => "td_sequential",
            "波段趋势" => "wave_trend",
            var s => s
        };

        var args = $"\"{mainPy}\" backtest" +
                   $" --symbol {parameters.Symbol}" +
                   $" --start {parameters.StartDate:yyyy-MM-dd}" +
                   $" --end {parameters.EndDate:yyyy-MM-dd}" +
                   $" --strategy {strategyName}" +
                   $" --short-window {parameters.ShortWindow}" +
                   $" --long-window {parameters.LongWindow}" +
                   $" --initial-cash {parameters.InitialCash}" +
                   " --json";

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = args,
            WorkingDirectory = projectRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        psi.Environment["PYTHONUTF8"] = "1";

        using var process = new Process { StartInfo = psi };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                progress?.Report(e.Data);
            }
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        await process.WaitForExitAsync(ct);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Python backtest failed (exit code {process.ExitCode}): {errorBuilder}");
        }

        // Parse JSON output: find the line after JSON_OUTPUT_START marker
        var output = outputBuilder.ToString().Trim();
        var lines = output.Split('\n');
        var jsonLine = "";
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Trim() == "JSON_OUTPUT_START")
            {
                jsonLine = lines[i + 1].Trim();
                break;
            }
        }
        if (string.IsNullOrEmpty(jsonLine))
        {
            jsonLine = lines.Last(s => s.TrimStart().StartsWith("{"));
        }

        var json = JsonDocument.Parse(jsonLine);
        var root = json.RootElement;

        var result = new BacktestResult
        {
            Symbol = parameters.Symbol,
            StrategyName = parameters.Strategy,
            StartDate = parameters.StartDate,
            EndDate = parameters.EndDate,
            InitialCash = root.GetProperty("initial_cash").GetDouble(),
            FinalValue = root.GetProperty("final_value").GetDouble(),
            TotalReturnPct = root.GetProperty("pnl_pct").GetDouble(),
            TradeCount = root.GetProperty("trade_count").GetInt32(),
            RunAt = DateTime.Now,
        };

        // Parse metrics
        if (root.TryGetProperty("metrics", out var metricsEl))
        {
            result.Metrics = new BacktestMetrics
            {
                AnnualReturnPct = metricsEl.TryGetProperty("annual_return_pct", out var ar) ? ar.GetDouble() : 0,
                SharpeRatio = metricsEl.TryGetProperty("sharpe_ratio", out var sr) ? sr.GetDouble() : 0,
                MaxDrawdownPct = metricsEl.TryGetProperty("max_drawdown_pct", out var md) ? md.GetDouble() : 0,
                MaxDrawdownStart = metricsEl.TryGetProperty("max_drawdown_start", out var mds) ? mds.GetString() ?? "" : "",
                MaxDrawdownEnd = metricsEl.TryGetProperty("max_drawdown_end", out var mde) ? mde.GetString() ?? "" : "",
                WinRate = metricsEl.TryGetProperty("win_rate", out var wr) ? wr.GetDouble() : 0,
                ProfitLossRatio = metricsEl.TryGetProperty("profit_loss_ratio", out var plr) ? plr.GetDouble() : 0,
                TotalTradingDays = metricsEl.TryGetProperty("total_trading_days", out var td) ? td.GetInt32() : 0,
                AvgDailyReturnPct = metricsEl.TryGetProperty("avg_daily_return_pct", out var adr) ? adr.GetDouble() : 0,
                VolatilityPct = metricsEl.TryGetProperty("volatility_pct", out var vol) ? vol.GetDouble() : 0,
            };

            if (metricsEl.TryGetProperty("monthly_returns", out var mrEl))
            {
                foreach (var prop in mrEl.EnumerateObject())
                {
                    result.Metrics.MonthlyReturns[prop.Name] = prop.Value.GetDouble();
                }
            }

            result.MaxDrawdownPct = result.Metrics.MaxDrawdownPct;
        }

        // Parse trades
        foreach (var t in root.GetProperty("trades").EnumerateArray())
        {
            result.Trades.Add(new Trade
            {
                Symbol = t.GetProperty("symbol").GetString() ?? "",
                Direction = t.GetProperty("direction").GetString() ?? "",
                Price = t.GetProperty("price").GetDouble(),
                Volume = t.GetProperty("volume").GetInt32(),
                TradeDate = DateTime.Parse(t.GetProperty("date").GetString() ?? ""),
                Reason = t.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "",
            });
        }

        // Parse equity curve
        var equityArray = root.GetProperty("equity_curve");
        var startDate = parameters.StartDate;
        for (int i = 0; i < equityArray.GetArrayLength(); i++)
        {
            result.EquityCurve.Add(new EquityPoint
            {
                Date = startDate.AddDays(i),
                Equity = equityArray[i].GetDouble(),
            });
        }

        // Calculate max drawdown
        if (result.EquityCurve.Count > 0)
        {
            var peak = result.EquityCurve[0].Equity;
            var maxDD = 0.0;
            foreach (var pt in result.EquityCurve)
            {
                if (pt.Equity > peak)
                {
                    peak = pt.Equity;
                }
                var dd = (peak - pt.Equity) / peak;
                if (dd > maxDD)
                {
                    maxDD = dd;
                }
            }
            result.MaxDrawdownPct = maxDD * 100;
        }

        // Persist to SQLite
        await m_DataService.SaveBacktestResultAsync(result);

        return result;
    }

    #endregion

    #region 私有接口

    private static string FindPythonExe(string projectRoot)
    {
        // Check for .venv in project root
        var venvPython = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        // Fallback: try uv's python
        var uvPython = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        if (File.Exists(uvPython))
        {
            return uvPython;
        }

        // Fallback: system python
        return "python";
    }

    #endregion
}
