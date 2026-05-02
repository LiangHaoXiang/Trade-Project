using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;

namespace TradeDashboard.Tests;

public class TabSwitchTests : AppTestBase
{
    [Fact]
    public void App_Launches_Successfully()
    {
        LaunchApp();
        Assert.NotNull(MainWindow);
        Assert.Contains("交易仪表盘", MainWindow.Title);
    }

    [Fact]
    public void App_Has_8_Tabs()
    {
        LaunchApp();
        Assert.NotNull(MainWindow);

        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tabControl);

        var tabs = tabControl.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.TabItem)
            .ToList();

        Assert.True(tabs.Count >= 8, $"期望至少 8 个 Tab，实际 {tabs.Count} 个");
    }

    [Theory]
    [InlineData(0, "总览")]
    [InlineData(1, "行情")]
    [InlineData(2, "交易")]
    [InlineData(3, "交易记录")]
    [InlineData(4, "资金曲线")]
    [InlineData(5, "回测")]
    [InlineData(6, "日志")]
    [InlineData(7, "配置")]
    public void Each_Tab_Is_Selectable(int index, string expectedHeader)
    {
        LaunchApp();
        var tab = GetTabByIndex(index);
        Assert.NotNull(tab);

        tab.Select();
        Thread.Sleep(300);

        Assert.True(tab.IsSelected, $"Tab[{index}] '{expectedHeader}' 选择失败");
    }
}
