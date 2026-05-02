"""
MACD背离策略
利用 MACD 柱状图与价格的背离信号进行交易

买入条件：价格创新低 但 MACD 柱状图未创新低（底背离）
卖出条件：价格创新高 但 MACD 柱状图未创新高（顶背离）或 MACD 死叉
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal
from strategy.factors import macd


class MACDDivergenceStrategy(BaseStrategy):
    def __init__(
        self,
        fast: int = 12,
        slow: int = 26,
        signal: int = 9,
        lookback: int = 20,
    ):
        super().__init__(name="macd_divergence")
        self.fast = fast
        self.slow = slow
        self.signal = signal
        self.lookback = lookback

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        macd_line, signal_line, histogram = macd(data["close"], self.fast, self.slow, self.signal)
        self._data["macd_line"] = macd_line
        self._data["macd_signal"] = signal_line
        self._data["macd_hist"] = histogram

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row.get("symbol", "UNKNOWN")

        min_idx = self.slow + self.signal
        if idx < min_idx or pd.isna(row.get("macd_hist")):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        close = row["close"]
        hist = row["macd_hist"]
        macd_line = row["macd_line"]
        sig_line = row["macd_signal"]

        lb = min(self.lookback, idx - min_idx)
        if lb < 5:
            return Signal(symbol=symbol, direction=Direction.HOLD, price=close)

        window = self._data.iloc[idx - lb : idx + 1]

        if hist < 0:
            price_low = window["close"].min()
            hist_low = window["macd_hist"].min()
            prev_hist_low_idx = window["macd_hist"].idxmin()
            if prev_hist_low_idx != window.index[-1]:
                if close <= price_low and hist > hist_low:
                    return Signal(
                        symbol=symbol,
                        direction=Direction.BUY,
                        price=close,
                        reason=f"MACD底背离 价格新低{close:.2f} MACD柱未新低",
                    )

        if hist > 0:
            price_high = window["close"].max()
            hist_high = window["macd_hist"].max()
            if close >= price_high and hist < hist_high:
                return Signal(
                    symbol=symbol,
                    direction=Direction.SELL,
                    price=close,
                    reason=f"MACD顶背离 价格新高{close:.2f} MACD柱未新高",
                )

        prev = self._data.iloc[idx - 1]
        if prev["macd_line"] >= prev["macd_signal"] and macd_line < sig_line:
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=close,
                reason="MACD死叉卖出",
            )

        if prev["macd_line"] <= prev["macd_signal"] and macd_line > sig_line:
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=close,
                reason="MACD金叉买入",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=close)
