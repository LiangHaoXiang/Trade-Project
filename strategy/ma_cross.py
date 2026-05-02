"""
双均线交叉策略
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal


class MACrossStrategy(BaseStrategy):
    def __init__(self, short_window: int = 5, long_window: int = 20):
        super().__init__(name="ma_cross")
        self.short_window = short_window
        self.long_window = long_window

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        self._data["ma_short"] = self._data["close"].rolling(self.short_window).mean()
        self._data["ma_long"] = self._data["close"].rolling(self.long_window).mean()

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row["symbol"]

        if idx < self.long_window:
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        prev = self._data.iloc[idx - 1]

        # 金叉：短期均线上穿长期均线 → 买入
        if prev["ma_short"] <= prev["ma_long"] and row["ma_short"] > row["ma_long"]:
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=row["close"],
                reason=f"金叉 MA{self.short_window}/{self.long_window}",
            )

        # 死叉：短期均线下穿长期均线 → 卖出
        if prev["ma_short"] >= prev["ma_long"] and row["ma_short"] < row["ma_long"]:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=row["close"],
                reason=f"死叉 MA{self.short_window}/{self.long_window}",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])
