# 股票量化交易系统 — 落地方案

## 1. 项目定位

个人级股票量化交易系统，支持 **A股** 市场，覆盖从数据采集、策略开发、回测验证到模拟/实盘交易的完整链路。

**核心目标：**

- 低门槛启动，逐步迭代
- 策略可插拔，方便快速验证想法
- 回测结果可信，支持模拟盘过渡到实盘
- 风控兜底，防止单次重大亏损

---

## 2. 技术选型


| 层面   | 选型                                  | 理由                   |
| ---- | ----------------------------------- | -------------------- |
| 语言   | Python 3.11+                        | 量化生态最成熟，库丰富          |
| 数据存储 | SQLite + CSV                        | 个人项目够用，零运维           |
| 行情数据 | AKShare（免费） / Tushare（需积分）/BaoStock | A股数据源，支持日/分钟级        |
| 回测框架 | Backtrader / 自研轻量框架                 | Backtrader 社区大；自研更灵活 |
| 因子计算 | Pandas + NumPy + TA-Lib             | 标准组合                 |
| 机器学习 | Scikit-learn（初期）/ PyTorch（后期）       | 渐进引入                 |
| 交易接口 | easytrader / QMT（券商量化客户端）           | A股个人可用的自动化方案         |
| 调度   | APScheduler / 系统定时任务                | 定时执行策略               |
| 可视化  | Streamlit / Matplotlib              | 策略监控面板               |
| 项目管理 | uv（包管理）+ pyproject.toml             | 现代 Python 项目管理       |


---

## 3. 系统架构

```
TradeProject/
├── pyproject.toml              # 项目配置 & 依赖
├── config/
│   ├── settings.yaml           # 全局配置（账户、参数）
│   └── strategies/             # 策略配置文件
├── data/
│   ├── manager.py              # 数据下载 & 更新入口
│   ├── storage.py              # 数据存储层（SQLite 操作）
│   └── cache/                  # 本地数据缓存目录
├── strategy/
│   ├── base.py                 # 策略基类（定义接口）
│   ├── factors.py              # 通用因子库
│   ├── ma_cross.py             # 示例：双均线策略
│   ├── mean_reversion.py       # 示例：均值回归策略
│   └── momentum.py             # 示例：动量策略
├── backtest/
│   ├── engine.py               # 回测引擎
│   ├── portfolio.py            # 持仓 & 资金管理
│   ├── metrics.py              # 绩效指标计算
│   └── report.py               # 回测报告生成
├── risk/
│   ├── manager.py              # 风控管理器
│   └── rules.py                # 风控规则（止损/仓位/集中度）
├── trader/
│   ├── simulator.py            # 模拟交易
│   └── broker.py               # 券商接口封装（实盘）
├── monitor/
│   ├── dashboard.py            # Streamlit 监控面板
│   └── notifier.py             # 消息通知（邮件/微信）
├── scheduler.py                # 定时任务调度
└── main.py                     # 入口
```

---

## 4. 核心模块设计

### 4.1 数据层

**职责：** 统一获取、缓存、更新行情数据

- 日线/分钟线 OHLCV 数据
- 财务数据（PE、PB、市值）
- 板块/指数数据
- 本地 SQLite 缓存，每日增量更新
- 提供统一的 DataFrame 输出接口

**关键接口：**

```python
class DataManager:
    def get_daily(self, symbol: str, start: str, end: str) -> pd.DataFrame
    def get_minutes(self, symbol: str, period: int = 5) -> pd.DataFrame
    def update_all(self) -> None  # 增量更新
```

### 4.2 策略层

**职责：** 定义策略接口，提供因子库

所有策略继承基类，统一接口：

```python
class BaseStrategy(ABC):
    def init(self, data: pd.DataFrame, context: dict) -> None: ...
    def next(self, bar: pd.Series) -> Signal: ...

@dataclass
class Signal:
    symbol: str
    direction: Direction  # BUY / SELL / HOLD
    price: float
    volume: int
    reason: str
```

**初期提供 3 个示例策略：**

1. **双均线交叉** — 经典趋势跟踪，适合入门验证系统
2. **均值回归** — 利用短期超跌反弹
3. **动量选股** — 按涨跌幅排名选股轮动

### 4.3 回测引擎

**职责：** 模拟历史交易，输出绩效指标

- 逐 bar 驱动，支持日线和分钟线
- 手续费、滑点、印花税模拟（A股：买入万1，卖出万1+千1印花税）
- 支持多策略、多标的同时回测

**输出指标：**


| 指标       | 说明     |
| -------- | ------ |
| 年化收益率    | 核心收益指标 |
| 最大回撤     | 风险控制   |
| 夏普比率     | 风险调整收益 |
| 胜率 / 盈亏比 | 策略有效性  |
| 月度收益分布   | 策略稳定性  |


### 4.4 风控模块

**职责：** 在交易前/中/后执行风控检查


| 规则     | 默认值     | 说明        |
| ------ | ------- | --------- |
| 单笔最大仓位 | 总资金 20% | 避免集中持仓    |
| 单日最大亏损 | 总资金 3%  | 触发后当日停止交易 |
| 个股止损线  | -8%     | 自动止损      |
| 最大持仓数  | 5 只     | 分散风险      |
| 涨跌停不交易 | —       | A股特殊规则    |


### 4.5 交易执行层

**职责：** 从信号到下单的执行

```
信号 → 风控检查 → 仓位计算 → 下单 → 记录
```

- **模拟模式：** 所有订单写入本地记录，不连券商
- **实盘模式：** 通过 easytrader / QMT 连接券商客户端下单
- 实盘前必须通过模拟盘验证 >= 30 个交易日

### 4.6 监控面板

Streamlit 搭建简易 Web 面板：

- 当前持仓 & 盈亏
- 当日成交记录
- 策略绩效曲线
- 风控状态

---

## 5. 数据流

```
AKShare/Tushare
       │
       ▼
  DataManager ──► SQLite 本地库
       │
       ▼
  Strategy.next(bar) ──► Signal
       │
       ▼
  RiskManager.check(signal) ──► 通过/拒绝
       │
       ▼
  Trader.execute(signal) ──► 模拟 / 券商下单
       │
       ▼
  Portfolio 更新 + Monitor 展示
```

---

## 6. 实施路线

### 第一阶段：基础设施（第 1-2 周）

- 项目初始化（uv、pyproject.toml、目录结构）
- DataManager 数据下载 & 缓存（efinance + AKShare 双源，SQLite 本地缓存）
- SQLite 存储层封装（data/storage.py）
- 基础因子计算（MA、RSI、布林带等）→ strategy/factors.py

### 第二阶段：回测系统（第 3-4 周）

- BaseStrategy 策略基类（Signal / Direction 定义）
- 回测引擎（逐 bar 驱动，手续费/印花税/滑点模拟）
- 双均线策略实现并验证
- 资金 & 持仓管理（backtest/portfolio.py）
- 绩效指标计算（backtest/metrics.py：夏普比率、最大回撤、胜率等）
- 回测报告生成（backtest/report.py）

### 第三阶段：策略开发（第 5-6 周）

- 均值回归策略
- 动量选股策略
- 因子库扩展（MACD、KDJ、换手率等）
- 策略对比 & 参数优化

### 第四阶段：风控 & 执行（第 7-8 周）

- 风控规则模块（risk/manager.py、risk/rules.py）
- 模拟交易模块（trader/simulator.py）
- 定时调度（收盘后运行策略）
- 交易记录持久化

### 第五阶段：监控 & 实盘准备（第 9-10 周）

- ~~Streamlit 监控面板~~ → 已改用 WPF 桌面仪表盘（见下方）
- 消息通知
- 券商接口对接（trader/broker.py）
- 模拟盘跑满 30 个交易日

### 第六阶段：迭代优化（持续）

- 引入机器学习因子
- 多策略组合 & 资金分配
- 分钟级策略
- 实盘运行 & 监控

---

## 7. WPF 可视化仪表盘

> 原计划使用 Streamlit Web 面板，实际采用 WPF 桌面应用，交互体验更接近专业交易软件。

**技术栈：** .NET 8 + WPF + ScottPlot 5 + CommunityToolkit.Mvvm + Serilog + SQLite

### 已完成

- 项目框架搭建（MVVM 架构，依赖注入）
- 深色主题 + 中文 UI
- Dashboard 总览页（资产概览卡片）
- MarketDataView 行情页（K线图 + ScottPlot 交互，十字光标、缩放拖拽）
- TradeHistoryView 交易历史页
- EquityCurveView 资金曲线页
- BacktestView 回测页
- LogViewerView 日志监控页
- ConfigurationView 配置管理页
- C# 代码规范化（m_/s_/k_ 前缀、花括号另起一行、#region 中文分组）

### 待完善

- 接入真实回测数据（目前部分视图使用模拟数据）
- 行情实时刷新
- 回测结果可视化（净值曲线、回撤图、月度收益热力图）
- 持仓 & 盈亏实时展示
- 风控状态面板
- 策略参数在线调整

### WPF UI 规范（必须遵循）

**1. 全中文显示**
- 所有面向用户的文本必须使用中文：方向（买入/卖出）、状态（已成交/已拒绝）、筛选选项（全部/买入/卖出）
- 数据模型内部可保留英文（BUY/SELL），通过 Converter 转换为中文显示
- 已有 Converter：`DirectionToTextConverter`（BUY→买入, SELL→卖出）、`StatusToTextConverter`（filled→已成交, rejected→已拒绝）

**2. DataGrid 列对齐规范**
- 全局 `DataGridColumnHeader.Padding` 为 `6,6`，`HorizontalContentAlignment` 为 `Left`
- 全局 `DataGridCell.Padding` 为 `0`（由 ElementStyle 控制 Margin）
- 所有数据列 ElementStyle 必须设置 `Margin="6,3"`
- 代码列、时间列、方向列、状态列 → **居中对齐**（TextAlignment=Center）
- 数值列（价格、数量、金额、盈亏等）→ **右对齐**（TextAlignment=Right）
- 原因列 → **左对齐**（默认）
- 所有列使用**固定宽度**（Width=具体数值），最后一列用 `Width="*"` 填充剩余空间
- DataGrid 设置 `ColumnWidth="*"` 确保列填充

**3. 颜色规范（A股惯例）**
- 买入/涨/正收益 → **红色**（#ef5350）
- 卖出/跌/负收益 → **绿色**（#26a69a）
- 零值/中性 → **黄色**（#c0ca33）
- 已有 Converter：`DirectionToBrushConverter`、`DecimalToColorConverter`

**4. 空状态提示**
- 使用 `CollectionEmptyToVisibilityConverter` 绑定 Visibility
- 有数据时隐藏空状态文本，无数据时显示

---

## 8. 关键风险 & 对策


| 风险     | 对策                          |
| ------ | --------------------------- |
| 数据源不稳定 | 双数据源切换（AKShare + Tushare）   |
| 过拟合    | 样本内训练、样本外验证；Walk-forward 分析 |
| 实盘滑点   | 回测中加入保守滑点估算（0.1%-0.2%）      |
| 策略失效   | 实时监控回撤，超阈值自动暂停              |
| 系统故障   | 关键操作日志记录 + 异常告警             |


---

## 9. 当前进度 & 下一步

**进度：** 第一阶段 ~60%，第二阶段 ~50%，WPF 仪表盘框架完成。

**下一步优先级：**

1. 补全回测绩效指标（metrics.py）和报告（report.py），让回测结果完整可用
2. 因子库（factors.py），为后续策略打基础
3. 更多策略（均值回归、动量选股）
4. 风控模块 + 模拟交易
5. WPF 仪表盘接入真实数据

