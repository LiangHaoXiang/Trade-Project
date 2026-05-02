"""
回测报告生成模块
整合绩效指标，支持 JSON 输出和文本摘要
"""
import json
from typing import Sequence

from backtest.engine import BacktestResult
from backtest.metrics import BacktestMetrics, compute_metrics


def build_report(
    result: BacktestResult,
    dates: Sequence | None = None,
) -> dict:
    """构建回测报告字典，包含完整的绩效指标和交易明细

    参数 result: 回测引擎输出的 BacktestResult
    参数 dates: 对应权益曲线的日期序列
    返回 符合 API_CONTRACT.md 定义的字典
    """
    metrics = compute_metrics(
        equity_curve=result.equity_curve,
        trades=result.trades,
        dates=dates,
    )

    pnl = result.final_value - result.initial_cash
    pnl_pct = pnl / result.initial_cash * 100 if result.initial_cash else 0

    report = {
        "initial_cash": result.initial_cash,
        "final_value": round(result.final_value, 2),
        "pnl": round(pnl, 2),
        "pnl_pct": round(pnl_pct, 4),
        "trade_count": len(result.trades),
        "metrics": metrics.to_dict(),
        "trades": [
            {
                "symbol": t.symbol,
                "direction": t.direction.value,
                "price": round(t.price, 4),
                "volume": t.volume,
                "date": str(t.date.date()) if hasattr(t.date, "date") else str(t.date),
                "reason": t.reason,
            }
            for t in result.trades
        ],
        "equity_curve": [round(v, 2) for v in result.equity_curve],
    }

    return report


def to_json(report: dict) -> str:
    """将报告转为 JSON 字符串"""
    return json.dumps(report, ensure_ascii=False)


def print_text_summary(report: dict) -> None:
    """打印文本格式的回测摘要"""
    m = report["metrics"]
    print("\n" + "=" * 60)
    print("  回测报告")
    print("=" * 60)
    print(f"  初始资金:     {report['initial_cash']:>12,.2f}")
    print(f"  最终资金:     {report['final_value']:>12,.2f}")
    print(f"  总盈亏:       {report['pnl']:>+12,.2f}  ({report['pnl_pct']:+.2f}%)")
    print("-" * 60)
    print(f"  年化收益率:   {m['annual_return_pct']:>+.2f}%")
    print(f"  夏普比率:     {m['sharpe_ratio']:>12.2f}")
    print(f"  年化波动率:   {m['volatility_pct']:>12.2f}%")
    print(f"  最大回撤:     {m['max_drawdown_pct']:>12.2f}%")
    if m["max_drawdown_start"]:
        print(f"    回撤区间:   {m['max_drawdown_start']} ~ {m['max_drawdown_end']}")
    print("-" * 60)
    print(f"  交易天数:     {m['total_trading_days']:>12d}")
    print(f"  交易次数:     {report['trade_count']:>12d}")
    print(f"  胜率:         {m['win_rate']:>11.1f}%")
    print(f"  盈亏比:       {m['profit_loss_ratio']:>12.2f}")
    print(f"  日均收益:     {m['avg_daily_return_pct']:>+11.4f}%")

    if m["monthly_returns"]:
        print("-" * 60)
        print("  月度收益:")
        for month, ret in sorted(m["monthly_returns"].items()):
            bar = "+" * int(ret / 2) if ret > 0 else "-" * int(abs(ret) / 2)
            print(f"    {month}: {ret:>+7.2f}%  {bar}")

    print("=" * 60)
