using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using FlaUI.UIA3;
using FlaUI.Core.AutomationElements;

namespace TradeDashboard.Tools;

public class ScreenshotRunner
{
    private static readonly (string Key, string Label)[] Tabs =
    [
        ("DashboardView", "总览"),
        ("MarketDataView", "行情"),
        ("TradingView", "交易"),
        ("TradeHistoryView", "交易记录"),
        ("EquityCurveView", "资金曲线"),
        ("BacktestView", "回测"),
        ("LogViewerView", "日志"),
        ("ConfigurationView", "配置"),
    ];

    private static readonly string ScreenshotDir = Path.Combine(
        FindProjectRoot(), "tools", "screenshots");

    private const int MaxScreenshots = 1000;

    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "status")
        {
            ShowStatus();
            return;
        }

        Directory.CreateDirectory(ScreenshotDir);
        CleanupOldScreenshots();

        var exePath = Path.Combine(FindProjectRoot(), "dashboard", "TradeDashboard", "bin",
            "Debug", "net8.0-windows10.0.19041", "TradeDashboard.exe");

        if (!File.Exists(exePath))
        {
            Console.WriteLine($"错误: 未找到应用 - {exePath}");
            Console.WriteLine("请先执行 dotnet build");
            Environment.Exit(1);
        }

        KillOrphanedProcesses();

        Console.WriteLine("正在启动应用...");
        var app = FlaUI.Core.Application.Launch(exePath);
        Thread.Sleep(3000);

        using var automation = new UIA3Automation();
        var mainWindow = app.GetMainWindow(automation, TimeSpan.FromSeconds(15));

        if (mainWindow == null)
        {
            Console.WriteLine("错误: 未找到主窗口");
            app.Kill();
            Environment.Exit(1);
        }

        Console.WriteLine($"找到主窗口: {mainWindow.Title}");

        var tabControl = mainWindow.FindFirstDescendant(
            FlaUI.Core.Definitions.TreeScope.Descendants,
            automation.ConditionFactory.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));

        if (tabControl == null)
        {
            Console.WriteLine("错误: 未找到 TabControl");
            app.Kill();
            Environment.Exit(1);
        }

        var tabItems = tabControl.FindAllChildren();
        Console.WriteLine($"找到 {tabItems.Length} 个 Tab 页签");

        for (int i = 0; i < Tabs.Length && i < tabItems.Length; i++)
        {
            var (key, label) = Tabs[i];
            Console.WriteLine($"切换到: {label} ({key}) ...");

            try
            {
                tabItems[i].Click();
                Thread.Sleep(1500);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  点击失败: {ex.Message}，尝试 Focus...");
                try
                {
                    tabItems[i].Focus();
                    Thread.Sleep(1500);
                }
                catch
                {
                    Console.WriteLine($"  跳过: {label}");
                    continue;
                }
            }

            mainWindow.SetForeground();
            Thread.Sleep(300);

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var filename = $"{timestamp}_{key}_{label}.png";
            var filepath = Path.Combine(ScreenshotDir, filename);

            CaptureWindow(mainWindow.AutomationId, filepath);
            Console.WriteLine($"  已保存: {filename}");
        }

        Console.WriteLine("\n截图完成！");
        Console.WriteLine($"共保存 {Tabs.Length} 张截图到: {ScreenshotDir}");

        app.Kill();
    }

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll")]
    private static extern IntPtr GetWindowDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, int nFlags);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int xDest, int yDest, int wDest, int hDest,
        IntPtr hdcSrc, int xSrc, int ySrc, int rop);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("gdi32.dll")]
    private static extern int DeleteDC(IntPtr hdc);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    private static void CaptureWindow(IntPtr hwnd, string filepath)
    {
        GetWindowRect(hwnd, out var rect);
        int width = rect.Right - rect.Left;
        int height = rect.Bottom - rect.Top;

        var hdcWindow = GetWindowDC(hwnd);
        var hdcMem = CreateCompatibleDC(hdcWindow);
        var hBitmap = CreateCompatibleBitmap(hdcWindow, width, height);
        SelectObject(hdcMem, hBitmap);
        PrintWindow(hwnd, hdcMem, 3);

        using var bmp = new Bitmap(width, height);
        var bmpGraphics = System.Drawing.Graphics.FromImage(bmp);
        var hdcBmp = bmpGraphics.GetHdc();
        BitBlt(hdcBmp, 0, 0, width, height, hdcMem, 0, 0, 0x00CC0020 | 0x40000000);
        bmpGraphics.ReleaseHdc(hdcBmp);

        bmp.Save(filepath, ImageFormat.Png);

        bmpGraphics.Dispose();
        bmp.Dispose();
        DeleteObject(hBitmap);
        DeleteDC(hdcMem);
        ReleaseDC(hwnd, hdcWindow);
    }

    private static void CleanupOldScreenshots()
    {
        var files = new DirectoryInfo(ScreenshotDir)
            .GetFiles("*.png")
            .OrderBy(f => f.CreationTime)
            .ToList();

        while (files.Count >= MaxScreenshots)
        {
            var oldest = files[0];
            files.RemoveAt(0);
            oldest.Delete();
            Console.WriteLine($"  清理旧截图: {oldest.Name}");
        }
    }

    private static void ShowStatus()
    {
        var files = new DirectoryInfo(ScreenshotDir).GetFiles("*.png");
        var totalSize = files.Sum(f => f.Length) / (1024.0 * 1024.0);
        Console.WriteLine($"截图目录: {ScreenshotDir}");
        Console.WriteLine($"截图数量: {files.Length} / {MaxScreenshots}");
        Console.WriteLine($"占用空间: {totalSize:F1} MB");
    }

    private static void KillOrphanedProcesses()
    {
        foreach (var proc in System.Diagnostics.Process.GetProcessesByName("TradeDashboard"))
        {
            try { proc.Kill(true); proc.WaitForExit(3000); }
            catch { }
            finally { proc.Dispose(); }
        }
    }

    private static string FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "PLAN.md")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        return Directory.GetCurrentDirectory();
    }
}
