using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using TradeDashboard.Models;

namespace TradeDashboard.Services;

public class LiveTradingService : ILiveTradingService
{
    #region 私有变量

    private readonly IConfigurationService m_ConfigService;

    #endregion

    #region 构造函数

    public LiveTradingService(IConfigurationService configService)
    {
        m_ConfigService = configService;
    }

    #endregion

    #region 公有接口

    public async Task<LiveBrokerStatus> ConnectAsync()
    {
        return await RunCommandAsync<LiveBrokerStatus>("trade-connect");
    }

    public async Task DisconnectAsync()
    {
        await RunCommandAsync<LiveBrokerStatus>("trade-disconnect");
    }

    public async Task<LiveBrokerStatus> GetStatusAsync()
    {
        return await RunCommandAsync<LiveBrokerStatus>("trade-status");
    }

    public async Task<LiveAccountInfo> GetAccountAsync()
    {
        return await RunCommandAsync<LiveAccountInfo>("trade-account");
    }

    public async Task<IReadOnlyList<LivePosition>> GetPositionsAsync()
    {
        return await RunCommandAsync<IReadOnlyList<LivePosition>>("trade-positions");
    }

    public async Task<IReadOnlyList<LiveEntrust>> GetEntrustsAsync()
    {
        return await RunCommandAsync<IReadOnlyList<LiveEntrust>>("trade-entrusts");
    }

    public async Task<LiveOrderResult> BuyAsync(string symbol, double price, int volume)
    {
        return await RunCommandAsync<LiveOrderResult>(
            $"trade-buy --symbol {symbol} --price {price} --volume {volume}");
    }

    public async Task<LiveOrderResult> SellAsync(string symbol, double price, int volume)
    {
        return await RunCommandAsync<LiveOrderResult>(
            $"trade-sell --symbol {symbol} --price {price} --volume {volume}");
    }

    public async Task<LiveOrderResult> CancelAsync(string orderId)
    {
        return await RunCommandAsync<LiveOrderResult>(
            $"trade-cancel --order-id {orderId}");
    }

    #endregion

    #region 私有接口

    private async Task<T> RunCommandAsync<T>(string commandArgs)
    {
        var projectRoot = m_ConfigService.GetProjectRootPath();
        var pythonExe = FindPythonExe(projectRoot);
        var mainPy = Path.Combine(projectRoot, "main.py");

        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = $"\"{mainPy}\" {commandArgs} --json",
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

        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var errMsg = errorBuilder.ToString().Trim();
            throw new InvalidOperationException($"Python 命令执行失败 (exit={process.ExitCode}): {errMsg}");
        }

        var output = outputBuilder.ToString().Trim();
        var jsonLine = ExtractJsonLine(output);
        return JsonSerializer.Deserialize<T>(jsonLine, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;
    }

    private static string ExtractJsonLine(string output)
    {
        var lines = output.Split('\n');
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Trim() == "JSON_OUTPUT_START")
            {
                return lines[i + 1].Trim();
            }
        }

        var lastJson = lines.LastOrDefault(s => s.TrimStart().StartsWith("{") || s.TrimStart().StartsWith("["));
        return lastJson?.Trim() ?? throw new InvalidOperationException("未找到 JSON 输出");
    }

    private static string FindPythonExe(string projectRoot)
    {
        var venvPython = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        return "python";
    }

    #endregion
}
