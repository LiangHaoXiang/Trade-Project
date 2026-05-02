using System.Linq;
using System.Threading;
using FlaUI.Core.Definitions;
using Xunit;

namespace TradeDashboard.Tests;

public class DashboardTests : AppTestBase
{
    [Fact]
    public void Dashboard_Shows_Summary_Cards()
    {
        LaunchApp();
        Assert.NotNull(MainWindow);

        // 总览页默认显示，验证卡片标签
        Thread.Sleep(1000);

        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("总资产", labels);
        Assert.Contains("总盈亏", labels);
        Assert.Contains("收益率", labels);
        Assert.Contains("交易次数", labels);
    }

    [Fact]
    public void Dashboard_Shows_Recent_Trades_Section()
    {
        LaunchApp();
        Assert.NotNull(MainWindow);

        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("近期交易", labels);
    }

    [Fact]
    public void Dashboard_Shows_Equity_Chart()
    {
        LaunchApp();
        Assert.NotNull(MainWindow);

        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("资金曲线", labels);
    }
}
