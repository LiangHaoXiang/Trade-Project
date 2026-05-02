# API 契约文档

> Python 后端与 C# WPF 前端之间的数据交互规范。
> 任何接口变更必须先更新本文档，再同步两端代码。

---

## 1. 交互方式

当前采用 **CLI 调用 + JSON 输出** 模式：

- C# 端通过 `Process` 调用 `python main.py <command> [options] --json`
- Python 端在标准输出中打印 `JSON_OUTPUT_START` 标记，紧接着一行完整 JSON
- C# 端解析标记后的 JSON 行

```
[可选的日志输出行...]
JSON_OUTPUT_START
{"key": "value", ...}
```

---

## 2. 接口清单

### 2.1 回测运行

**命令：** `python main.py backtest --symbol <代码> --start <日期> --end <日期> --strategy <策略名> --short-window <N> --long-window <N> --initial-cash <金额> --json`

**策略名称对照表：**

| 策略ID | 中文名称 | 说明 |
|--------|---------|------|
| `ma_cross` | 双均线交叉 | 经典趋势跟踪 |
| `mean_reversion` | 均值回归 | 布林带+RSI超跌反弹 |
| `momentum` | 动量选股 | 涨幅排名轮动 |
| `macd_divergence` | MACD背离 | 底背离买入/顶背离卖出 |
| `rsi_extreme` | RSI超买超卖 | RSI穿越30/70阈值 |
| `bollinger_breakout` | 布林带突破 | 突破上下轨 |
| `kdj_cross` | KDJ金叉死叉 | K/D线交叉信号 |

**C# 端中文名 → Python 端策略ID 映射：**

| C# 显示名 | Python --strategy 参数 |
|-----------|----------------------|
| 双均线交叉 | `ma_cross` |
| 均值回归 | `mean_reversion` |
| 动量选股 | `momentum` |
| MACD背离 | `macd_divergence` |
| RSI超买超卖 | `rsi_extreme` |
| 布林带突破 | `bollinger_breakout` |
| KDJ金叉死叉 | `kdj_cross` |

**Python 输出 JSON 格式：**

```json
{
  "initial_cash": 100000.0,
  "final_value": 105234.56,
  "pnl": 5234.56,
  "pnl_pct": 5.23,
  "trade_count": 10,
  "metrics": {
    "annual_return_pct": 5.23,
    "sharpe_ratio": 1.35,
    "max_drawdown_pct": 8.42,
    "max_drawdown_start": "2024-03-15",
    "max_drawdown_end": "2024-04-10",
    "win_rate": 60.0,
    "profit_loss_ratio": 1.8,
    "total_trading_days": 244,
    "avg_daily_return_pct": 0.021,
    "volatility_pct": 12.5,
    "monthly_returns": {
      "2024-01": 2.1,
      "2024-02": -1.3,
      "2024-03": 3.5
    }
  },
  "trades": [
    {
      "symbol": "000001",
      "direction": "BUY",
      "price": 12.85,
      "volume": 7400,
      "date": "2024-02-05T00:00:00",
      "reason": "金叉 MA5/20"
    }
  ],
  "equity_curve": [100000.0, 100500.0, 99800.0, ...]
}
```

**C# 模型映射：**

| JSON 字段 | C# 类型 | C# 属性 | 说明 |
|-----------|---------|---------|------|
| `initial_cash` | `double` | `BacktestResult.InitialCash` | |
| `final_value` | `double` | `BacktestResult.FinalValue` | |
| `pnl` | `double` | 计算属性 `Pnl = FinalValue - InitialCash` | |
| `pnl_pct` | `double` | 计算属性 `PnlPct` | |
| `trade_count` | `int` | `BacktestResult.TradeCount` | |
| `metrics` | `object` | `BacktestMetrics` (新增) | 绩效指标 |
| `metrics.annual_return_pct` | `double` | `BacktestMetrics.AnnualReturnPct` | 年化收益率 |
| `metrics.sharpe_ratio` | `double` | `BacktestMetrics.SharpeRatio` | 夏普比率 |
| `metrics.max_drawdown_pct` | `double` | `BacktestMetrics.MaxDrawdownPct` | 最大回撤 |
| `metrics.max_drawdown_start` | `string` | `BacktestMetrics.MaxDrawdownStart` | 回撤起始日 |
| `metrics.max_drawdown_end` | `string` | `BacktestMetrics.MaxDrawdownEnd` | 回撤结束日 |
| `metrics.win_rate` | `double` | `BacktestMetrics.WinRate` | 胜率(%) |
| `metrics.profit_loss_ratio` | `double` | `BacktestMetrics.ProfitLossRatio` | 盈亏比 |
| `metrics.total_trading_days` | `int` | `BacktestMetrics.TotalTradingDays` | 交易天数 |
| `metrics.avg_daily_return_pct` | `double` | `BacktestMetrics.AvgDailyReturnPct` | 日均收益率 |
| `metrics.volatility_pct` | `double` | `BacktestMetrics.VolatilityPct` | 年化波动率 |
| `metrics.monthly_returns` | `Dictionary<string, double>` | `BacktestMetrics.MonthlyReturns` | 月度收益 |
| `trades[].symbol` | `string` | `Trade.Symbol` | |
| `trades[].direction` | `string` | `Trade.Direction` | "BUY"/"SELL" |
| `trades[].price` | `double` | `Trade.Price` | |
| `trades[].volume` | `int` | `Trade.Volume` | |
| `trades[].date` | `string` | `Trade.TradeDate` | ISO 8601 |
| `trades[].reason` | `string` | `Trade.Reason` | |
| `equity_curve[]` | `double[]` | `EquityPoint.Equity` | 日期按起始日逐日递增 |

---

### 2.2 股票列表

**命令：** `python main.py stock-list`

**Python 输出 JSON 格式：**

```json
[
  {"symbol": "000001", "name": "平安银行"},
  {"symbol": "000002", "name": "万科A"}
]
```

**C# 模型映射：**

| JSON 字段 | C# 类型 | C# 属性 |
|-----------|---------|---------|
| `symbol` | `string` | `StockInfo.Symbol` |
| `name` | `string` | `StockInfo.Name` |

---

### 2.3 日线行情

**命令：** `python main.py daily-data --symbol <代码> --start <日期> --end <日期>`

**Python 输出 JSON 格式：**

```json
[
  {
    "symbol": "000001",
    "date": "2024-01-02",
    "open": 10.50,
    "high": 10.80,
    "low": 10.40,
    "close": 10.75,
    "volume": 50000000.0,
    "amount": 537500000.0
  }
]
```

**C# 模型映射：**

| JSON 字段 | C# 类型 | C# 属性 |
|-----------|---------|---------|
| `symbol` | `string` | `DailyBar.Symbol` |
| `date` | `DateTime` | `DailyBar.Date` | 
| `open` | `double` | `DailyBar.Open` |
| `high` | `double` | `DailyBar.High` |
| `low` | `double` | `DailyBar.Low` |
| `close` | `double` | `DailyBar.Close` |
| `volume` | `double` | `DailyBar.Volume` | 单位：股 |
| `amount` | `double` | `DailyBar.Amount` | 单位：元 |

---

### 2.4 数据更新检查

**命令：** `python main.py check-update`

**Python 输出 JSON 格式：**

```json
{
  "status": "up_to_date",
  "latest": "2025-04-30"
}
```

`status` 取值：`"up_to_date"` / `"updated"` / `"no_data"` / `"error"`

---

### 2.5 数据下载

**命令：** `python main.py download --symbol <代码> --start <日期> --end <日期>`

无 JSON 输出，仅日志文本。

---

### 2.6 全市场历史下载

**命令：** `python main.py download-all --start <日期>`

无 JSON 输出，仅日志文本。

---

### 2.7 新闻资讯（多源聚合）

**命令：** `python main.py news --json`

**数据源：** 东方财富（`stock_info_global_em`）+ 新浪财经（`stock_info_global_sina`），通过 AKShare 免费接口聚合，自动去重。

**Python 输出 JSON 格式：**

```json
{
  "fetched_at": "2025-05-01T10:30:00",
  "items": [
    {
      "title": "国务院常务会议部署稳经济措施",
      "source": "东方财富",
      "url": "https://finance.eastmoney.com/...",
      "published_at": "2025-05-01T09:15:00",
      "summary": "会议指出要加大宏观政策调控力度..."
    }
  ]
}
```

**C# 模型映射：**

| JSON 字段 | C# 类型 | C# 属性 | 说明 |
|-----------|---------|---------|------|
| `fetched_at` | `string` | — | 本次拉取时间 |
| `items[].title` | `string` | `NewsItem.Title` | 新闻标题 |
| `items[].source` | `string` | `NewsItem.Source` | 来源（"东方财富" / "新浪财经"） |
| `items[].url` | `string` | `NewsItem.Url` | 原文链接，C# 端点击可跳转浏览器 |
| `items[].published_at` | `string` | `NewsItem.PublishedAt` | 发布时间 ISO 8601 |
| `items[].summary` | `string` | `NewsItem.Summary` | 摘要 |

**说明：**
- 聚合东方财富 + 新浪财经两个数据源，按标题去重，按时间倒序
- 默认返回最新 50 条
- C# 端每 30 分钟自动调用一次刷新
- 新闻数据不持久化，每次实时拉取
- `url` 字段在 C# 端实现为可点击的超链接，点击后打开默认浏览器查看原文

---

## 3. SQLite 共享数据库

Python 和 C# 共享同一个 SQLite 数据库文件（路径配置于 `config/settings.yaml`）。

### 表结构

#### daily — 日线行情

| 列名 | 类型 | 说明 |
|------|------|------|
| `symbol` | TEXT | 股票代码（如 "000001"） |
| `date` | TEXT | 日期 "YYYY-MM-DD" |
| `open` | REAL | 开盘价 |
| `high` | REAL | 最高价 |
| `low` | REAL | 最低价 |
| `close` | REAL | 收盘价 |
| `volume` | REAL | 成交量（股） |
| `amount` | REAL | 成交额（元） |
| **PK** | | `(symbol, date)` |

#### backtest_runs — 回测运行记录

| 列名 | 类型 | 说明 |
|------|------|------|
| `id` | INTEGER PK | 自增 ID |
| `symbol` | TEXT | 回测标的 |
| `strategy_name` | TEXT | 策略名称 |
| `start_date` | TEXT | "YYYY-MM-DD" |
| `end_date` | TEXT | "YYYY-MM-DD" |
| `initial_cash` | REAL | 初始资金 |
| `final_value` | REAL | 最终资金 |
| `total_return_pct` | REAL | 总收益率(%) |
| `max_drawdown_pct` | REAL | 最大回撤(%) |
| `trade_count` | INTEGER | 交易次数 |
| `run_at` | TEXT | 运行时间 ISO 8601 |
| `parameters_json` | TEXT | 策略参数 JSON |

#### trades — 交易记录

| 列名 | 类型 | 说明 |
|------|------|------|
| `id` | INTEGER PK | 自增 ID |
| `backtest_run_id` | INTEGER FK | 关联回测运行 |
| `symbol` | TEXT | 股票代码 |
| `direction` | TEXT | "BUY" / "SELL" |
| `price` | REAL | 成交价格 |
| `volume` | INTEGER | 成交数量（股） |
| `trade_date` | TEXT | "YYYY-MM-DD" |
| `reason` | TEXT | 交易原因 |

#### equity_curve — 权益曲线

| 列名 | 类型 | 说明 |
|------|------|------|
| `id` | INTEGER PK | 自增 ID |
| `backtest_run_id` | INTEGER FK | 关联回测运行 |
| `date` | TEXT | "YYYY-MM-DD" |
| `equity` | REAL | 当日权益 |

#### stock_basic — 股票基本信息

| 列名 | 类型 | 说明 |
|------|------|------|
| `symbol` | TEXT PK | 股票代码 |
| `name` | TEXT | 股票名称 |

---

## 4. 变更日志

| 日期 | 变更内容 | 影响端 |
|------|---------|--------|
| 2025-05-01 | 初始版本：定义回测/行情/股票列表接口 | Python + C# |
| 2025-05-01 | 新增 `metrics` 字段：年化收益、夏普、胜率、盈亏比、月度收益等 | Python 输出 + C# 解析 |
| 2026-05-01 | 新增 `2.7 新闻资讯` 接口：多源聚合（东方财富+新浪），AKShare 免费接口 | Python 输出 + C# 解析 |
| 2026-05-02 | 新增 `--strategy` 参数：支持 7 种策略选择（双均线/均值回归/动量/MACD背离/RSI超买超卖/布林带突破/KDJ金叉死叉） | Python 输入 + C# 传入 |
