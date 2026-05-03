"""
波段趋势 (Wave Trend) 策略
以参考均线为基准，通过计算均线对比前一日的变化力度判断趋势转折。
- 均线由下降转为上升（蓝转紫）：买入信号
- 均线由上升转为下降（紫转蓝）：卖出信号
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal


class WaveTrendStrategy(BaseStrategy):
    def __init__(self, ma_period: int = 20):
        super().__init__(name="wave_trend")
        self.ma_period = ma_period

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy().reset_index(drop=True)
        self._data["ma"] = self._data["close"].rolling(self.ma_period).mean()
        self._data["ma_diff"] = self._data["ma"].diff()
        self._prev_direction = None

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row["symbol"]

        if idx < self.ma_period + 1 or pd.isna(row["ma"]) or pd.isna(row["ma_diff"]):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        ma_diff = row["ma_diff"]
        current_direction = "up" if ma_diff > 0 else "down"

        if self._prev_direction is not None:
            if self._prev_direction == "down" and current_direction == "up":
                self._prev_direction = current_direction
                return Signal(
                    symbol=symbol,
                    direction=Direction.BUY,
                    price=row["close"],
                    reason=f"波段趋势反转向上 MA{self.ma_period}",
                )
            elif self._prev_direction == "up" and current_direction == "down":
                self._prev_direction = current_direction
                return Signal(
                    symbol=symbol,
                    direction=Direction.SELL,
                    price=row["close"],
                    reason=f"波段趋势反转向下 MA{self.ma_period}",
                )

        self._prev_direction = current_direction
        return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])
