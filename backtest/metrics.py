"""
回测绩效指标计算模块
计算年化收益、夏普比率、最大回撤、胜率、盈亏比、月度收益分布等核心指标
"""
from dataclasses import dataclass, field
from typing import Sequence

import numpy as np
import pandas as pd

from strategy.base import Direction

TRADING_DAYS_PER_YEAR = 242
RISK_FREE_RATE_ANNUAL = 0.02


@dataclass
class BacktestMetrics:
    annual_return_pct: float = 0.0
    sharpe_ratio: float = 0.0
    max_drawdown_pct: float = 0.0
    max_drawdown_start: str = ""
    max_drawdown_end: str = ""
    win_rate: float = 0.0
    profit_loss_ratio: float = 0.0
    total_trading_days: int = 0
    avg_daily_return_pct: float = 0.0
    volatility_pct: float = 0.0
    monthly_returns: dict[str, float] = field(default_factory=dict)

    def to_dict(self) -> dict:
        return {
            "annual_return_pct": round(self.annual_return_pct, 4),
            "sharpe_ratio": round(self.sharpe_ratio, 4),
            "max_drawdown_pct": round(self.max_drawdown_pct, 4),
            "max_drawdown_start": self.max_drawdown_start,
            "max_drawdown_end": self.max_drawdown_end,
            "win_rate": round(self.win_rate, 2),
            "profit_loss_ratio": round(self.profit_loss_ratio, 4),
            "total_trading_days": self.total_trading_days,
            "avg_daily_return_pct": round(self.avg_daily_return_pct, 6),
            "volatility_pct": round(self.volatility_pct, 4),
            "monthly_returns": {k: round(v, 4) for k, v in self.monthly_returns.items()},
        }


def calc_max_drawdown(equity_curve: Sequence[float]) -> tuple[float, int, int]:
    """计算最大回撤百分比及起止位置索引

    返回 (最大回撤百分比, 回撤起始索引, 回撤结束索引)
    """
    if not equity_curve or len(equity_curve) < 2:
        return 0.0, -1, -1

    peak = equity_curve[0]
    max_dd = 0.0
    start_idx = 0
    end_idx = 0
    peak_idx = 0

    for i in range(1, len(equity_curve)):
        if equity_curve[i] > peak:
            peak = equity_curve[i]
            peak_idx = i
        dd = (peak - equity_curve[i]) / peak if peak > 0 else 0.0
        if dd > max_dd:
            max_dd = dd
            start_idx = peak_idx
            end_idx = i

    return max_dd * 100, start_idx, end_idx


def calc_trade_stats(trades: list) -> tuple[float, float]:
    """计算胜率和盈亏比

    参数 trades: 包含 direction, price, volume 字段的交易记录列表
    返回 (胜率百分比, 盈亏比)
    """
    if not trades:
        return 0.0, 0.0

    buys: dict[str, list[tuple[float, int]]] = {}
    profits = []

    for t in trades:
        sym = t.symbol
        if t.direction == Direction.BUY:
            buys.setdefault(sym, []).append((t.price, t.volume))
        elif t.direction == Direction.SELL:
            buy_list = buys.get(sym, [])
            remaining_vol = t.volume
            cost = 0.0
            matched = 0
            for bp, bv in buy_list:
                if remaining_vol <= 0:
                    break
                vol = min(remaining_vol, bv)
                cost += vol * bp
                matched += vol
                remaining_vol -= vol
            if matched > 0:
                revenue = matched * t.price
                profit = revenue - cost
                profits.append(profit)

    if not profits:
        return 0.0, 0.0

    wins = [p for p in profits if p > 0]
    losses = [p for p in profits if p < 0]

    win_rate = len(wins) / len(profits) * 100

    avg_win = sum(wins) / len(wins) if wins else 0.0
    avg_loss = abs(sum(losses) / len(losses)) if losses else 0.0
    profit_loss_ratio = avg_win / avg_loss if avg_loss > 0 else float("inf") if avg_win > 0 else 0.0

    return win_rate, profit_loss_ratio


def calc_monthly_returns(dates: Sequence, equity_curve: Sequence[float]) -> dict[str, float]:
    """计算月度收益率

    参数 dates: 日期序列（pd.DatetimeIndex 或可转 datetime 的序列）
    参数 equity_curve: 权益曲线
    返回 {"2024-01": 2.1, "2024-02": -1.3, ...}
    """
    if len(dates) == 0 or len(equity_curve) == 0:
        return {}

    if not isinstance(dates, pd.DatetimeIndex):
        dates = pd.to_datetime(dates)

    df = pd.DataFrame({"date": dates, "equity": list(equity_curve)})
    df["month"] = df["date"].dt.to_period("M")

    monthly = {}
    for period, group in df.groupby("month"):
        month_start = group["equity"].iloc[0]
        month_end = group["equity"].iloc[-1]
        if month_start > 0:
            ret = (month_end - month_start) / month_start * 100
            monthly[str(period)] = round(ret, 4)

    return monthly


def calc_sharpe_ratio(daily_returns: np.ndarray, annual_rf: float = RISK_FREE_RATE_ANNUAL) -> float:
    """计算年化夏普比率

    参数 daily_returns: 日收益率数组
    参数 annual_rf: 年化无风险利率
    """
    if len(daily_returns) < 2:
        return 0.0

    daily_rf = annual_rf / TRADING_DAYS_PER_YEAR
    excess = daily_returns - daily_rf
    std = np.std(excess, ddof=1)
    if std < 1e-10:
        return 0.0
    return float(np.mean(excess) / std * np.sqrt(TRADING_DAYS_PER_YEAR))


def compute_metrics(
    equity_curve: list[float],
    trades: list,
    dates: Sequence | None = None,
) -> BacktestMetrics:
    """从回测结果计算全部绩效指标

    参数 equity_curve: 逐 bar 权益曲线
    参数 trades: 交易记录列表（需包含 direction, symbol, price, volume）
    参数 dates: 对应权益曲线的日期序列（用于月度收益计算）
    """
    metrics = BacktestMetrics()

    n = len(equity_curve)
    if n == 0:
        return metrics

    metrics.total_trading_days = n

    initial = equity_curve[0]
    final = equity_curve[-1]

    # 总收益率 → 年化
    total_return = (final - initial) / initial if initial > 0 else 0.0
    years = n / TRADING_DAYS_PER_YEAR
    if years > 0 and total_return > -1:
        metrics.annual_return_pct = ((1 + total_return) ** (1 / years) - 1) * 100
    else:
        metrics.annual_return_pct = total_return * 100

    # 日收益率序列
    equity_arr = np.array(equity_curve, dtype=np.float64)
    daily_returns = np.diff(equity_arr) / equity_arr[:-1]
    daily_returns = daily_returns[np.isfinite(daily_returns)]

    # 日均收益
    if len(daily_returns) > 0:
        metrics.avg_daily_return_pct = float(np.mean(daily_returns)) * 100

    # 年化波动率
    if len(daily_returns) > 1:
        metrics.volatility_pct = float(np.std(daily_returns, ddof=1) * np.sqrt(TRADING_DAYS_PER_YEAR)) * 100

    # 夏普比率
    metrics.sharpe_ratio = calc_sharpe_ratio(daily_returns)

    # 最大回撤
    max_dd, start_idx, end_idx = calc_max_drawdown(equity_curve)
    metrics.max_drawdown_pct = max_dd

    if dates is not None and start_idx >= 0 and end_idx >= 0:
        dt_list = list(dates)
        if start_idx < len(dt_list) and end_idx < len(dt_list):
            s = dt_list[start_idx]
            e = dt_list[end_idx]
            metrics.max_drawdown_start = str(pd.Timestamp(s).date()) if not isinstance(s, str) else s
            metrics.max_drawdown_end = str(pd.Timestamp(e).date()) if not isinstance(e, str) else e

    # 胜率 & 盈亏比
    metrics.win_rate, metrics.profit_loss_ratio = calc_trade_stats(trades)

    # 月度收益
    if dates is not None:
        metrics.monthly_returns = calc_monthly_returns(dates, equity_curve)

    return metrics
