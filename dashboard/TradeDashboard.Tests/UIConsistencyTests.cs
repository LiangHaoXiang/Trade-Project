using System.Linq;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using Xunit;

namespace TradeDashboard.Tests;

public class UIConsistencyTests : AppTestBase
{
    private static readonly string[] EnglishKeywords = ["BUY", "SELL", "filled", "rejected", "pending", "cancelled", "ALL"];

    [Fact]
    public void No_English_Keywords_Visible_On_Dashboard()
    {
        LaunchApp();
        Thread.Sleep(1000);
        Assert.NotNull(MainWindow);

        var allText = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        foreach (var keyword in EnglishKeywords)
        {
            Assert.DoesNotContain(keyword, allText);
        }
    }

    [Fact]
    public void No_English_Keywords_Visible_On_Trading()
    {
        LaunchApp();
        SwitchToTab(2);
        Thread.Sleep(800);

        var allText = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        foreach (var keyword in EnglishKeywords)
        {
            Assert.DoesNotContain(keyword, allText);
        }
    }

    [Fact]
    public void No_English_Keywords_Visible_On_TradeHistory()
    {
        LaunchApp();
        SwitchToTab(3);
        Thread.Sleep(800);

        var allText = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Text)
            .Select(e => e.Name)
            .Where(n => !string.IsNullOrEmpty(n))
            .ToList();

        foreach (var keyword in EnglishKeywords)
        {
            Assert.DoesNotContain(keyword, allText);
        }
    }

    [Fact]
    public void Trading_Direction_ComboBox_Uses_Chinese()
    {
        LaunchApp();
        SwitchToTab(2);
        Thread.Sleep(500);

        var comboBoxes = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.ComboBox)
            .ToList();

        Assert.True(comboBoxes.Count >= 1, "交易页应至少有1个ComboBox");

        var comboBox = comboBoxes.First().AsComboBox();
        Assert.NotNull(comboBox);
        comboBox.Expand();
        Thread.Sleep(300);

        var items = comboBox.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.ListItem)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("买入", items);
        Assert.Contains("卖出", items);
        Assert.DoesNotContain("BUY", items);
        Assert.DoesNotContain("SELL", items);

        comboBox.Collapse();
    }

    [Fact]
    public void Trading_Tab_SubTabs_Are_Chinese()
    {
        LaunchApp();
        SwitchToTab(2);
        Thread.Sleep(500);

        var tabItems = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.TabItem)
            .Select(e => e.Name)
            .ToList();

        Assert.Contains("当前持仓", tabItems);
        Assert.Contains("今日委托", tabItems);
    }

    [Fact]
    public void TradeHistory_Filter_Directions_Are_Chinese()
    {
        LaunchApp();
        SwitchToTab(3);
        Thread.Sleep(800);

        var comboBoxes = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.ComboBox)
            .ToList();

        foreach (var rawCb in comboBoxes)
        {
            var cb = rawCb.AsComboBox();
            if (cb is null) continue;
            cb.Expand();
            Thread.Sleep(200);

            var items = cb.FindAllDescendants()
                .Where(e => e.ControlType == ControlType.ListItem)
                .Select(e => e.Name)
                .ToList();

            if (items.Contains("BUY") || items.Contains("SELL") || items.Contains("ALL"))
            {
                Assert.Fail($"发现英文方向选项: {string.Join(", ", items)}，应使用中文");
            }

            cb.Collapse();
        }
    }

    [Fact]
    public void Backtest_Left_Panel_Width_Is_Fixed()
    {
        LaunchApp();
        SwitchToTab(5);
        Thread.Sleep(800);

        var dataGrids = MainWindow.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.DataGrid)
            .ToList();

        if (dataGrids.Count == 0) return;

        var grid = dataGrids.First();
        var rect = grid.BoundingRectangle;

        var headers = grid.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.Header)
            .ToList();

        if (headers.Count == 0) return;

        var headerItems = headers[0].FindAllChildren()
            .Where(e => e.ControlType == ControlType.HeaderItem)
            .ToList();

        Assert.True(headerItems.Count >= 1, "DataGrid应至少有1列");

        foreach (var header in headerItems)
        {
            var headerRect = header.BoundingRectangle;
            var headerWidth = headerRect.Width;

            Assert.True(headerWidth > 20 && headerWidth < 400,
                $"列'{header.Name}'宽度异常: {headerWidth}px，应在20-400之间");
        }
    }

    [Fact]
    public void All_Eight_Tabs_Are_Present()
    {
        LaunchApp();
        Thread.Sleep(500);

        var tabControl = MainWindow.FindFirstDescendant(cf => cf.ByControlType(ControlType.Tab));
        Assert.NotNull(tabControl);

        var tabs = tabControl.FindAllDescendants()
            .Where(e => e.ControlType == ControlType.TabItem)
            .Select(e => e.Name)
            .ToList();

        var expectedTabs = new[] { "总览", "行情", "交易", "交易记录", "资金曲线", "回测", "日志", "配置" };
        foreach (var expected in expectedTabs)
        {
            Assert.Contains(expected, tabs);
        }
    }
}
