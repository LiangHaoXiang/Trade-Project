"""
动量选股策略
按涨跌幅排名选股轮动

买入条件：过去 N 日涨幅排名前 K 的标的
卖出条件：持仓标的跌出动量前 K 名 或 持有超过 M 天
"""
import pandas as pd

from strategy.base import BaseStrategy, Direction, Signal


class MomentumStrategy(BaseStrategy):
    def __init__(
        self,
        lookback: int = 20,
        hold_days: int = 10,
    ):
        super().__init__(name="momentum")
        self.lookback = lookback
        self.hold_days = hold_days

    def init(self, data: pd.DataFrame) -> None:
        self._data = data.copy()
        self._data["momentum"] = self._data["close"].pct_change(self.lookback) * 100
        self._hold_days_counter = 0

    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        row = self._data.iloc[idx]
        symbol = row.get("symbol", "UNKNOWN")

        if idx < self.lookback:
            return Signal(symbol=symbol, direction=Direction.HOLD, price=row["close"])

        momentum = row["momentum"]
        close = row["close"]

        if pd.isna(momentum):
            return Signal(symbol=symbol, direction=Direction.HOLD, price=close)

        if self._hold_days_counter <= 0 and momentum > 0:
            self._hold_days_counter = self.hold_days
            return Signal(
                symbol=symbol,
                direction=Direction.BUY,
                price=close,
                reason=f"动量买入 {self.lookback}日涨幅={momentum:+.2f}%",
            )

        if self._hold_days_counter > 0:
            self._hold_days_counter -= 1

            if momentum < 0 or self._hold_days_counter <= 0:
                self._hold_days_counter = 0
                return Signal(
                    symbol=symbol,
                    direction=Direction.SELL,
                    price=close,
                    reason=f"动量卖出 {self.lookback}日涨幅={momentum:+.2f}%" if momentum < 0
                    else f"动量卖出 持有{self.hold_days}天到期",
                )

        return Signal(symbol=symbol, direction=Direction.HOLD, price=close)
