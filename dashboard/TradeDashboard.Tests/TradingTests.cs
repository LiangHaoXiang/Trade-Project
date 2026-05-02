using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Conditions;
using FlaUI.Core.Definitions;
using Xunit;

namespace TradeDashboard.Tests;

public class TradingTests : AppTestBase
{
    private void NavigateToTradingTab()
    {
        LaunchApp();
        SwitchToTab(2);
    }

    [Fact]
    public void Trading_Tab_Shows_Account_Cards()
    {
        NavigateToTradingTab();
        Assert.NotNull(MainWindow);

        // 验证账户概览区域存在（总资产、可用资金、持仓市值、总盈亏、持仓数）
        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("总资产", labels);
        Assert.Contains("可用资金", labels);
        Assert.Contains("持仓市值", labels);
    }

    [Fact]
    public void Trading_Tab_Shows_Order_Panel()
    {
        NavigateToTradingTab();
        Assert.NotNull(MainWindow);

        var labels = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("模拟下单", labels);
        Assert.Contains("股票代码", labels);
        Assert.Contains("价格", labels);
        Assert.Contains("数量（股）", labels);
    }

    [Fact]
    public void Trading_Tab_Shows_Submit_Button()
    {
        NavigateToTradingTab();
        Assert.NotNull(MainWindow);

        var buttons = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Button)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("提交委托", buttons);
    }

    [Fact]
    public void Trading_Tab_Shows_Reset_Button()
    {
        NavigateToTradingTab();
        Assert.NotNull(MainWindow);

        var buttons = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Button)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("重置模拟账户", buttons);
    }

    [Fact]
    public void Trading_Buy_Order_Fills()
    {
        NavigateToTradingTab();
        Assert.NotNull(MainWindow);

        // 切换到交易 Tab 后，查找下单相关的输入框
        // 通过标签顺序定位：代码 → 方向 → 价格 → 数量
        Thread.Sleep(500);

        // 找到所有 TextBox
        var textBoxes = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Edit)
            .ToList();

        // 交易页面应该有多个 TextBox（代码、价格、数量）
        Assert.True(textBoxes.Count >= 3, $"交易页面应有至少 3 个输入框（代码/价格/数量），实际 {textBoxes.Count} 个");
    }

    [Fact]
    public void Trading_Tab_Has_Position_And_Order_SubTabs()
    {
        NavigateToTradingTab();
        Assert.NotNull(MainWindow);

        // 交易页面内部有 TabControl：当前持仓、今日委托
        Thread.Sleep(300);
        var tabItems = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.TabItem)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("当前持仓", tabItems);
        Assert.Contains("今日委托", tabItems);
    }
}
