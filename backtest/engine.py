"""
回测引擎 - 逐 bar 驱动策略并模拟交易
"""
from dataclasses import dataclass, field

import pandas as pd

from strategy.base import BaseStrategy, Direction


@dataclass
class Trade:
    symbol: str
    direction: Direction
    price: float
    volume: int
    date: pd.Timestamp
    reason: str


@dataclass
class BacktestResult:
    trades: list[Trade] = field(default_factory=list)
    equity_curve: list[float] = field(default_factory=list)
    initial_cash: float = 100_000.0
    final_value: float = 0.0

    def print_summary(self):
        print("\n" + "=" * 50)
        print("回测报告")
        print("=" * 50)
        print(f"初始资金: {self.initial_cash:,.2f}")
        print(f"最终资金: {self.final_value:,.2f}")
        pnl = self.final_value - self.initial_cash
        pnl_pct = pnl / self.initial_cash * 100
        print(f"总盈亏: {pnl:+,.2f} ({pnl_pct:+.2f}%)")
        print(f"交易次数: {len(self.trades)}")

        buys = [t for t in self.trades if t.direction == Direction.BUY]
        sells = [t for t in self.trades if t.direction == Direction.SELL]
        print(f"买入 {len(buys)} 次, 卖出 {len(sells)} 次")

        if self.equity_curve:
            peak = max(self.equity_curve)
            max_dd = max(1 - v / peak for v in self.equity_curve) if peak > 0 else 0
            print(f"最大回撤: {max_dd * 100:.2f}%")

        print("=" * 50)


class BacktestEngine:
    """简单回测引擎"""

    def __init__(self, initial_cash: float = 100_000, commission: float = 0.0001, stamp_tax: float = 0.001):
        self.initial_cash = initial_cash
        self.commission = commission
        self.stamp_tax = stamp_tax

    def run(self, strategy: BaseStrategy, data: pd.DataFrame) -> BacktestResult:
        strategy.init(data)

        result = BacktestResult(initial_cash=self.initial_cash)
        cash = self.initial_cash
        position = 0  # 持仓股数
        symbol = data["symbol"].iloc[0] if "symbol" in data.columns else "UNKNOWN"

        for i in range(len(data)):
            signal = strategy.next(i, data)
            price = data["close"].iloc[i]
            date = data.index[i] if isinstance(data.index, pd.DatetimeIndex) else pd.Timestamp(data["date"].iloc[i])

            if signal.direction == Direction.BUY and position == 0:
                # 全仓买入
                volume = int(cash * 0.95 / price / 100) * 100  # 按手（100股）
                if volume > 0:
                    cost = volume * price * (1 + self.commission)
                    cash -= cost
                    position = volume
                    result.trades.append(Trade(symbol, Direction.BUY, price, volume, date, signal.reason))

            elif signal.direction == Direction.SELL and position > 0:
                revenue = position * price * (1 - self.commission - self.stamp_tax)
                cash += revenue
                result.trades.append(Trade(symbol, Direction.SELL, price, position, date, signal.reason))
                position = 0

            # 记录权益曲线
            equity = cash + position * price
            result.equity_curve.append(equity)

        result.final_value = result.equity_curve[-1] if result.equity_curve else self.initial_cash
        return result
