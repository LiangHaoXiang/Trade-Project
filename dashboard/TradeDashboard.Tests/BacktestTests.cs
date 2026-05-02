using System.Linq;
using System.Threading;
using FlaUI.Core.Definitions;
using Xunit;

namespace TradeDashboard.Tests;

public class BacktestTests : AppTestBase
{
    private void NavigateToBacktestTab()
    {
        LaunchApp();
        SwitchToTab(5);
    }

    [Fact]
    public void Backtest_Tab_Shows_Parameter_Panel()
    {
        NavigateToBacktestTab();
        Assert.NotNull(MainWindow);

        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("回测参数", labels);
        Assert.Contains("代码", labels);
        Assert.Contains("开始日期", labels);
        Assert.Contains("结束日期", labels);
    }

    [Fact]
    public void Backtest_Tab_Shows_Strategy_ComboBox()
    {
        NavigateToBacktestTab();
        Assert.NotNull(MainWindow);

        var comboBoxes = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.ComboBox)
            .ToList();

        Assert.True(comboBoxes.Count >= 1, "回测页面应至少有 1 个 ComboBox（策略选择）");
    }

    [Fact]
    public void Backtest_Tab_Shows_Run_Button()
    {
        NavigateToBacktestTab();
        Assert.NotNull(MainWindow);

        var buttons = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Button)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("运行回测", buttons);
    }

    [Fact]
    public void Backtest_Tab_Shows_Metric_Cards()
    {
        NavigateToBacktestTab();
        Assert.NotNull(MainWindow);

        Thread.Sleep(500);

        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        // 验证绩效指标卡片标签存在
        Assert.Contains("盈亏", labels);
        Assert.Contains("夏普比率", labels);
        Assert.Contains("最大回撤", labels);
    }
}
