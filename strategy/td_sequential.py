"""
神奇九转 (TD Sequential) 策略
源于汤姆·狄马克的TD序列，通过比较当日收盘价与之前第4个交易日的收盘价来识别趋势拐点。
- 低九转(连续9日收盘价低于前4日)：买入机会
- 高九转(连续9日收盘价高于前4日)：卖出风险
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal


class TDSequentialStrategy(BaseStrategy):
    def __init__(self, compare_period: int = 4, sequential_count: int = 9):
        super().__init__(name="td_sequential")
        self.compare_period = compare_period
        self.sequential_count = sequential_count

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy().reset_index(drop=True)
        n = len(self._data)

        close = self._data["close"].values
        td_buy_seq = [0] * n
        td_sell_seq = [0] * n

        for i in range(self.compare_period, n):
            prev_idx = i - self.compare_period
            if close[i] < close[prev_idx]:
                td_buy_seq[i] = td_buy_seq[i - 1] + 1 if i > 0 and td_buy_seq[i - 1] > 0 else 1
                td_sell_seq[i] = 0
            elif close[i] > close[prev_idx]:
                td_sell_seq[i] = td_sell_seq[i - 1] + 1 if i > 0 and td_sell_seq[i - 1] > 0 else 1
                td_buy_seq[i] = 0
            else:
                td_buy_seq[i] = 0
                td_sell_seq[i] = 0

        self._data["td_buy_seq"] = td_buy_seq
        self._data["td_sell_seq"] = td_sell_seq
        self._buy_signal_generated = [False] * n
        self._sell_signal_generated = [False] * n

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row["symbol"]

        if idx < self.compare_period:
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        buy_seq = int(row["td_buy_seq"])
        sell_seq = int(row["td_sell_seq"])

        if buy_seq == self.sequential_count and not self._buy_signal_generated[idx]:
            self._buy_signal_generated[idx] = True
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=row["close"],
                reason=f"低{self.sequential_count}转买入 TD序列",
            )

        if sell_seq == self.sequential_count and not self._sell_signal_generated[idx]:
            self._sell_signal_generated[idx] = True
            return Signal(
                symbol=symbol,
                direction=Direction.SELL,
                price=row["close"],
                reason=f"高{self.sequential_count}转卖出 TD序列",
            )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])
