"""
均值回归策略
基于布林带 + RSI 的超跌反弹策略

买入条件：价格跌破布林带下轨 且 RSI < 超卖阈值
卖出条件：价格触及布林带中轨 或 RSI > 超买阈值
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal
from strategy.factors import bollinger, rsi


class MeanReversionStrategy(BaseStrategy):
    def __init__(
        self,
        boll_window: int = 20,
        boll_std: float = 2.0,
        rsi_window: int = 14,
        oversold: float = 30.0,
        overbought: float = 70.0,
    ):
        super().__init__(name="mean_reversion")
        self.boll_window = boll_window
        self.boll_std = boll_std
        self.rsi_window = rsi_window
        self.oversold = oversold
        self.overbought = overbought

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        upper, middle, lower = bollinger(data["close"], self.boll_window, self.boll_std)
        self._data["boll_upper"] = upper
        self._data["boll_middle"] = middle
        self._data["boll_lower"] = lower
        self._data["rsi"] = rsi(data["close"], self.rsi_window)

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row.get("symbol", "UNKNOWN")

        min_idx = max(self.boll_window, self.rsi_window)
        if idx < min_idx or pd.isna(row.get("rsi")):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        close = row["close"]
        boll_lower = row["boll_lower"]
        boll_middle = row["boll_middle"]
        rsi_val = row["rsi"]

        if close <= boll_lower and rsi_val < self.oversold:
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=close,
                reason=f"均值回归买入 RSI={rsi_val:.1f} 低于布林下轨",
            )

        if close >= boll_middle and rsi_val > self.overbought:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=close,
                reason=f"均值回归卖出 RSI={rsi_val:.1f} 触及布林中轨",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=close)
