using System.Diagnostics;
using System.IO;
using FlaUI.UIA3;
using FlaUI.Core;
using FlaUI.Core.AutomationElements;
using Xunit;

namespace TradeDashboard.Tests;

public abstract class AppTestBase : IDisposable
{
    #region 私有变量

    private readonly string m_ExePath;
    private bool m_Disposed;
    private bool m_AppLaunched;

    #endregion

    #region 公有属性

    protected Application? App { get; private set; }
    protected UIA3Automation Automation { get; } = new();
    protected Window? MainWindow { get; private set; }

    #endregion

    protected AppTestBase()
    {
        var projectRoot = FindProjectRoot();
        m_ExePath = Path.Combine(projectRoot, "dashboard", "TradeDashboard", "bin", "Debug",
            "net8.0-windows10.0.19041", "TradeDashboard.exe");
    }

    #region 公有接口

    protected void LaunchApp()
    {
        if (m_AppLaunched)
        {
            return;
        }
        m_AppLaunched = true;

        if (!File.Exists(m_ExePath))
        {
            throw new FileNotFoundException($"应用程序未找到: {m_ExePath}，请先执行 dotnet build");
        }

        KillOrphanedProcesses();

        App = Application.Launch(m_ExePath);
        Assert.NotNull(App);

        MainWindow = App.GetMainWindow(Automation, TimeSpan.FromSeconds(15));
        Assert.NotNull(MainWindow);
    }

    private void KillOrphanedProcesses()
    {
        var exeName = Path.GetFileNameWithoutExtension(m_ExePath);
        foreach (var proc in Process.GetProcessesByName(exeName))
        {
            try
            {
                proc.Kill(true);
                proc.WaitForExit(3000);
            }
            catch
            {
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    protected TabItem GetTabByIndex(int index)
    {
        Assert.NotNull(MainWindow);
        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(FlaUI.Core.Definitions.ControlType.Tab));
        Assert.NotNull(tabControl);
        var tabs = tabControl.FindAllDescendants()
            .Where(e => e.ControlType == FlaUI.Core.Definitions.ControlType.TabItem)
            .ToList();
        Assert.True(index < tabs.Count, $"Tab 索引 {index} 超出范围（共 {tabs.Count} 个 Tab）");
        return tabs[index].AsTabItem();
    }

    protected void SwitchToTab(int index)
    {
        var tab = GetTabByIndex(index);
        tab.Select();
        Thread.Sleep(500);
    }

    protected AutomationElement? FindElementByName(string name)
    {
        Assert.NotNull(MainWindow);
        return MainWindow.FindFirstDescendant(cf => cf.ByName(name));
    }

    protected AutomationElement? FindElementByAutomationId(string automationId)
    {
        Assert.NotNull(MainWindow);
        return MainWindow.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
    }

    protected TextBox? FindTextBox(string automationId)
    {
        var element = FindElementByAutomationId(automationId);
        return element?.AsTextBox();
    }

    protected Button? FindButton(string automationId)
    {
        var element = FindElementByAutomationId(automationId);
        return element?.AsButton();
    }

    #endregion

    #region 私有接口

    private static string FindProjectRoot()
    {
        var dir = AppDomain.CurrentDomain.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "PLAN.md")))
            {
                return dir;
            }
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException("无法找到项目根目录（PLAN.md 所在位置）");
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (m_Disposed)
        {
            return;
        }
        m_Disposed = true;

        try
        {
            if (App is not null)
            {
                App.Close();
                App.Dispose();
            }
        }
        catch
        {
        }

        KillOrphanedProcesses();

        Automation.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}
