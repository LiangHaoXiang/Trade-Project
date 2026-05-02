"""
RSI超买超卖策略
基于 RSI 极值区域的反转交易

买入条件：RSI 从超卖区域（< 30）回升穿过 30
卖出条件：RSI 从超买区域（> 70）回落穿过 70
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal
from strategy.factors import rsi


class RSIExtremeStrategy(BaseStrategy):
    def __init__(
        self,
        rsi_window: int = 14,
        oversold: float = 30.0,
        overbought: float = 70.0,
    ):
        super().__init__(name="rsi_extreme")
        self.rsi_window = rsi_window
        self.oversold = oversold
        self.overbought = overbought

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        self._data["rsi"] = rsi(data["close"], self.rsi_window)

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row.get("symbol", "UNKNOWN")

        if idx < self.rsi_window or pd.isna(row.get("rsi")):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        close = row["close"]
        rsi_val = row["rsi"]
        prev_rsi = self._data.iloc[idx - 1]["rsi"]

        if pd.isna(prev_rsi):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=close)

        if prev_rsi < self.oversold and rsi_val >= self.oversold:
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=close,
                reason=f"RSI超卖回升 RSI={rsi_val:.1f}",
            )

        if prev_rsi > self.overbought and rsi_val <= self.overbought:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=close,
                reason=f"RSI超买回落 RSI={rsi_val:.1f}",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=close)
