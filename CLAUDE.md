# 项目说明

个人级 A 股量化交易系统，Python 后端 + WPF 仪表盘。

## C# 代码规范

编写或修改 `.cs` 文件时，遵循 `.cursor/skills/csharp-code-style/` 下的规范：

- **核心速览**：`.cursor/skills/csharp-code-style/SKILL.md`
- **命名规范**：`.cursor/skills/csharp-code-style/naming.md`（`m_`/`s_`/`k_` 前缀、PascalCase 等）
- **格式规范**：`.cursor/skills/csharp-code-style/formatting.md`（花括号另起一行、using 排序、成员排序等）
- **设计原则**：`.cursor/skills/csharp-code-style/design.md`（SRP、DRY、错误处理）
- **示例**：`.cursor/skills/csharp-code-style/examples.md`

不确定时按需读取对应子文件，不要一次性全部读取。

## UI 自动化测试规范

测试项目位于 `dashboard/TradeDashboard.Tests/`，使用 **FlaUI.UIA3** + **xUnit**。

### 运行测试

```bash
cd dashboard
dotnet test TradeDashboard.Tests --verbosity normal
```

### Agent 职责分工

| Agent | 职责 | 触发时机 |
|-------|------|---------|
| **PM (项目经理)** | 统筹全局、分配任务、维护 API 契约 | 每次收到需求时 |
| **Python Agent** | 后端模块开发（策略/因子/风控/交易） | PM 分配后端任务时 |
| **C# Agent** | WPF 前端开发（ViewModel/View/Service） | PM 分配前端任务时 |
| **UI 测试 Agent** | 编写/维护/运行 UI 自动化测试 | 前端代码变更后 |

### UI 测试 Agent 工作流

1. **C# Agent 完成界面变更后**，PM 指派 UI 测试 Agent 执行测试
2. UI 测试 Agent 执行 `dotnet test`，报告结果
3. 如有失败，PM 评估优先级并指派 C# Agent 修复
4. 修复后重新测试，直到全部通过

### 测试文件组织

| 文件 | 覆盖范围 |
|------|---------|
| `AppTestBase.cs` | 测试基类：启动/关闭应用、定位窗口 |
| `TabSwitchTests.cs` | Tab 切换、应用启动验证 |
| `DashboardTests.cs` | 总览页卡片、近期交易、资金曲线 |
| `BacktestTests.cs` | 回测参数面板、运行按钮、绩效指标卡片 |
| `TradingTests.cs` | 模拟交易：下单面板、持仓、委托记录 |

### 新增测试的规则

- 每个新增的 Tab 页必须对应一个测试文件
- 每个新增的交互按钮必须至少有一个测试覆盖
- 新增 Model/Service 后，C# Agent 需同步通知 UI 测试 Agent 补充测试
- 测试必须在有 GUI 的环境下运行（不支持 headless）
