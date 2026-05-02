"""
布林带突破策略
基于布林带上下轨的突破交易

买入条件：价格向上突破布林带上轨（强势突破）
卖出条件：价格回落至布林带中轨以下 或 触及上轨后回落
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal
from strategy.factors import bollinger


class BollingerBreakoutStrategy(BaseStrategy):
    def __init__(
        self,
        window: int = 20,
        num_std: float = 2.0,
    ):
        super().__init__(name="bollinger_breakout")
        self.window = window
        self.num_std = num_std

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        upper, middle, lower = bollinger(data["close"], self.window, self.num_std)
        self._data["boll_upper"] = upper
        self._data["boll_middle"] = middle
        self._data["boll_lower"] = lower

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row.get("symbol", "UNKNOWN")

        if idx < self.window or pd.isna(row.get("boll_upper")):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        close = row["close"]
        upper = row["boll_upper"]
        middle = row["boll_middle"]
        lower = row["boll_lower"]

        prev = self._data.iloc[idx - 1]

        if prev["close"] <= prev["boll_upper"] and close > upper:
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=close,
                reason=f"布林带上轨突破 价格{close:.2f} 上轨{upper:.2f}",
            )

        if prev["close"] >= prev["boll_lower"] and close < lower:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=close,
                reason=f"布林带下轨破位 价格{close:.2f} 下轨{lower:.2f}",
            )

        if close < middle and prev["close"] >= prev["boll_middle"]:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=close,
                reason=f"跌破布林带中轨 价格{close:.2f}",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=close)
