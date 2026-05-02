using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.Json;
using TradeDashboard.Models;

namespace TradeDashboard.Services;

public class PythonNewsService : INewsService
{
    #region 私有变量

    private readonly IConfigurationService m_ConfigService;

    #endregion

    #region 构造函数

    public PythonNewsService(IConfigurationService configService)
    {
        m_ConfigService = configService;
    }

    #endregion

    #region 公有接口

    public async Task<IReadOnlyList<NewsItem>> FetchNewsAsync()
    {
        var projectRoot = m_ConfigService.GetProjectRootPath();
        var pythonExe = FindPythonExe(projectRoot);
        var mainPy = Path.Combine(projectRoot, "main.py");

        var args = $"\"{mainPy}\" news --json";

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
            throw new InvalidOperationException($"获取新闻失败 (exit code {process.ExitCode}): {errorBuilder}");
        }

        var output = outputBuilder.ToString().Trim();
        var jsonLine = FindJsonLine(output);

        if (string.IsNullOrEmpty(jsonLine))
        {
            return Array.Empty<NewsItem>();
        }

        return ParseNewsItems(jsonLine);
    }

    #endregion

    #region 私有接口

    private static string FindPythonExe(string projectRoot)
    {
        var venvPython = Path.Combine(projectRoot, ".venv", "Scripts", "python.exe");
        if (File.Exists(venvPython))
        {
            return venvPython;
        }

        return "python";
    }

    private static string? FindJsonLine(string output)
    {
        var lines = output.Split('\n');
        for (int i = 0; i < lines.Length - 1; i++)
        {
            if (lines[i].Trim() == "JSON_OUTPUT_START")
            {
                return lines[i + 1].Trim();
            }
        }

        var lastJson = lines.LastOrDefault(s => s.TrimStart().StartsWith("{"));
        return lastJson?.Trim();
    }

    private static IReadOnlyList<NewsItem> ParseNewsItems(string jsonLine)
    {
        var items = new List<NewsItem>();

        try
        {
            var json = JsonDocument.Parse(jsonLine);
            var root = json.RootElement;

            if (!root.TryGetProperty("items", out var itemsEl))
            {
                return items;
            }

            foreach (var item in itemsEl.EnumerateArray())
            {
                items.Add(new NewsItem
                {
                    Title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "",
                    Source = item.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "",
                    Url = item.TryGetProperty("url", out var u) ? u.GetString() ?? "" : "",
                    PublishedAt = item.TryGetProperty("published_at", out var p) ? p.GetString() ?? "" : "",
                    Summary = item.TryGetProperty("summary", out var sm) ? sm.GetString() ?? "" : "",
                });
            }
        }
        catch (JsonException)
        {
            return items;
        }

        return items;
    }

    #endregion
}
