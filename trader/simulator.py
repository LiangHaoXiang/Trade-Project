"""
模拟交易模块
所有订单写入本地记录，不连接券商
用于策略验证和模拟盘测试
"""
import json
import sqlite3
from datetime import datetime
from pathlib import Path
from typing import Protocol

from strategy.base import Direction, Signal
from risk.manager import RiskManager, RiskContext, PositionInfo


class TradeRecord:
    def __init__(
        self,
        symbol: str,
        direction: str,
        price: float,
        volume: int,
        timestamp: str,
        reason: str = "",
        status: str = "filled",
    ):
        self.symbol = symbol
        self.direction = direction
        self.price = price
        self.volume = volume
        self.timestamp = timestamp
        self.reason = reason
        self.status = status

    def to_dict(self) -> dict:
        return {
            "symbol": self.symbol,
            "direction": self.direction,
            "price": self.price,
            "volume": self.volume,
            "timestamp": self.timestamp,
            "reason": self.reason,
            "status": self.status,
        }


class Simulator:
    """模拟交易器

    接收信号 → 风控检查 → 模拟成交 → 记录交易
    """

    def __init__(
        self,
        initial_cash: float = 100_000.0,
        commission: float = 0.0001,
        stamp_tax: float = 0.001,
        db_path: str | Path | None = None,
    ):
        self.initial_cash = initial_cash
        self.cash = initial_cash
        self.commission = commission
        self.stamp_tax = stamp_tax
        self.db_path = Path(db_path) if db_path else None

        self.positions: dict[str, PositionInfo] = {}
        self.trades: list[TradeRecord] = []
        self.risk_manager = RiskManager()
        self.context = RiskContext(total_equity=initial_cash)

    @property
    def total_equity(self) -> float:
        pos_value = sum(p.volume * p.current_price for p in self.positions.values())
        return self.cash + pos_value

    @property
    def current_holdings_count(self) -> int:
        return len(self.positions)

    def process_signal(self, signal: Signal, prev_close: float = 0.0) -> TradeRecord | None:
        """处理交易信号，返回成交记录或 None（被风控拒绝）"""
        if signal.direction == Direction.HOLD:
            return None

        # 更新风控上下文
        self.context.total_equity = self.total_equity
        self.context.current_holdings = self.current_holdings_count
        self.context.positions = self.positions

        # 风控检查
        risk_result = self.risk_manager.check(
            symbol=signal.symbol,
            direction=signal.direction,
            price=signal.price,
            volume=signal.volume if signal.volume > 0 else self._calc_volume(signal),
            context=self.context,
            prev_close=prev_close,
        )

        if not risk_result.passed:
            return TradeRecord(
                symbol=signal.symbol,
                direction=signal.direction.value,
                price=signal.price,
                volume=signal.volume,
                timestamp=datetime.now().isoformat(),
                reason=risk_result.message,
                status="rejected",
            )

        if signal.direction == Direction.BUY:
            return self._execute_buy(signal)
        elif signal.direction == Direction.SELL:
            return self._execute_sell(signal)

        return None

    def _calc_volume(self, signal: Signal) -> int:
        """计算可买入数量（按 95% 资金，取整到 100 股）"""
        available = self.cash * 0.95
        volume = int(available / signal.price / 100) * 100
        return max(volume, 0)

    def _execute_buy(self, signal: Signal) -> TradeRecord | None:
        volume = signal.volume if signal.volume > 0 else self._calc_volume(signal)
        if volume <= 0:
            return None

        cost = volume * signal.price * (1 + self.commission)
        if cost > self.cash:
            return None

        self.cash -= cost
        self.positions[signal.symbol] = PositionInfo(
            symbol=signal.symbol,
            volume=volume,
            cost_price=signal.price,
            current_price=signal.price,
        )

        record = TradeRecord(
            symbol=signal.symbol,
            direction="BUY",
            price=signal.price,
            volume=volume,
            timestamp=datetime.now().isoformat(),
            reason=signal.reason,
        )
        self.trades.append(record)
        return record

    def _execute_sell(self, signal: Signal) -> TradeRecord | None:
        pos = self.positions.get(signal.symbol)
        if pos is None or pos.volume <= 0:
            return None

        volume = min(signal.volume, pos.volume) if signal.volume > 0 else pos.volume
        revenue = volume * signal.price * (1 - self.commission - self.stamp_tax)

        self.cash += revenue
        del self.positions[signal.symbol]

        record = TradeRecord(
            symbol=signal.symbol,
            direction="SELL",
            price=signal.price,
            volume=volume,
            timestamp=datetime.now().isoformat(),
            reason=signal.reason,
        )
        self.trades.append(record)
        return record

    def update_prices(self, prices: dict[str, float]) -> None:
        """更新持仓标的的当前价格"""
        for symbol, price in prices.items():
            if symbol in self.positions:
                self.positions[symbol].current_price = price

    def get_status(self) -> dict:
        """获取当前模拟账户状态"""
        return {
            "initial_cash": self.initial_cash,
            "cash": round(self.cash, 2),
            "total_equity": round(self.total_equity, 2),
            "pnl": round(self.total_equity - self.initial_cash, 2),
            "pnl_pct": round((self.total_equity - self.initial_cash) / self.initial_cash * 100, 4),
            "positions": {
                sym: {"volume": p.volume, "cost": p.cost_price, "current": p.current_price}
                for sym, p in self.positions.items()
            },
            "trade_count": len([t for t in self.trades if t.status == "filled"]),
            "rejected_count": len([t for t in self.trades if t.status == "rejected"]),
        }

    def save_trades_to_db(self) -> None:
        """将交易记录保存到 SQLite"""
        if not self.db_path:
            return

        conn = sqlite3.connect(self.db_path)
        conn.execute("""
            CREATE TABLE IF NOT EXISTS sim_trades (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                symbol TEXT NOT NULL,
                direction TEXT NOT NULL,
                price REAL NOT NULL,
                volume INTEGER NOT NULL,
                timestamp TEXT NOT NULL,
                reason TEXT,
                status TEXT NOT NULL
            )
        """)
        for t in self.trades:
            conn.execute(
                "INSERT INTO sim_trades (symbol, direction, price, volume, timestamp, reason, status) VALUES (?, ?, ?, ?, ?, ?, ?)",
                (t.symbol, t.direction, t.price, t.volume, t.timestamp, t.reason, t.status),
            )
        conn.commit()
        conn.close()
