"""
KDJ金叉死叉策略
基于 KDJ 随机指标的金叉死叉信号

买入条件：K 线从下方上穿 D 线（金叉）且 J < 80（非超买区）
卖出条件：K 线从上方下穿 D 线（死叉）且 J > 20（非超卖区）
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal
from strategy.factors import kdj


class KDJCrossoverStrategy(BaseStrategy):
    def __init__(
        self,
        n: int = 9,
        m1: int = 3,
        m2: int = 3,
        overbought: float = 80.0,
        oversold: float = 20.0,
    ):
        super().__init__(name="kdj_cross")
        self.n = n
        self.m1 = m1
        self.m2 = m2
        self.overbought = overbought
        self.oversold = oversold

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        k, d, j = kdj(data["high"], data["low"], data["close"], self.n, self.m1, self.m2)
        self._data["kdj_k"] = k
        self._data["kdj_d"] = d
        self._data["kdj_j"] = j

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row.get("symbol", "UNKNOWN")

        if idx < self.n or pd.isna(row.get("kdj_k")):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        close = row["close"]
        k = row["kdj_k"]
        d_val = row["kdj_d"]
        j = row["kdj_j"]

        prev = self._data.iloc[idx - 1]
        prev_k = prev["kdj_k"]
        prev_d = prev["kdj_d"]

        if pd.isna(prev_k) or pd.isna(prev_d):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=close)

        if prev_k <= prev_d and k > d_val and j < self.overbought:
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=close,
                reason=f"KDJ金叉 K={k:.1f} D={d_val:.1f} J={j:.1f}",
            )

        if prev_k >= prev_d and k < d_val and j > self.oversold:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=close,
                reason=f"KDJ死叉 K={k:.1f} D={d_val:.1f} J={j:.1f}",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=close)
