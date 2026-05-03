"""
券商接口封装
支持 easytrader（同花顺/通达信）和 QMT（迅投）

使用方式：
    1. 安装 easytrader: pip install easytrader
    2. 打开券商客户端并登录
    3. 配置 config/settings.yaml 中 broker 部分
    4. 通过 BrokerFactory 创建实例

注意：实盘交易需先完成模拟盘验证（>= 30 个交易日）
"""
import json
import time
from abc import ABC, abstractmethod
from dataclasses import dataclass
from datetime import datetime
from pathlib import Path
from typing import Optional

import yaml

from monitor.logger import get_logger

logger = get_logger("broker")

AUDIT_LOG_DIR = Path(__file__).parent.parent / "data" / "logs"


@dataclass
class Order:
    symbol: str
    direction: str
    price: float
    volume: int
    order_type: str = "limit"


@dataclass
class OrderResult:
    success: bool
    order_id: str = ""
    message: str = ""
    filled_price: float = 0.0
    filled_volume: int = 0


@dataclass
class Position:
    symbol: str
    name: str = ""
    volume: int = 0
    available_volume: int = 0
    cost_price: float = 0.0
    current_price: float = 0.0
    pnl: float = 0.0
    pnl_pct: float = 0.0


@dataclass
class AccountInfo:
    total_assets: float = 0.0
    cash: float = 0.0
    market_value: float = 0.0
    positions: list[Position] = None

    def __post_init__(self):
        if self.positions is None:
            self.positions = []


@dataclass
class Entrust:
    entrust_no: str = ""
    symbol: str = ""
    name: str = ""
    direction: str = ""
    price: float = 0.0
    volume: int = 0
    filled_volume: int = 0
    status: str = ""
    order_time: str = ""


def _audit_log(action: str, detail: dict) -> None:
    AUDIT_LOG_DIR.mkdir(parents=True, exist_ok=True)
    log_file = AUDIT_LOG_DIR / "trade_audit.log"
    entry = {
        "timestamp": datetime.now().isoformat(),
        "action": action,
        **detail,
    }
    with open(log_file, "a", encoding="utf-8") as f:
        f.write(json.dumps(entry, ensure_ascii=False) + "\n")


class BrokerBase(ABC):
    """券商接口基类"""

    @abstractmethod
    def connect(self) -> bool:
        """连接券商客户端"""

    @abstractmethod
    def buy(self, symbol: str, price: float, volume: int) -> OrderResult:
        """买入"""

    @abstractmethod
    def sell(self, symbol: str, price: float, volume: int) -> OrderResult:
        """卖出"""

    @abstractmethod
    def cancel(self, order_id: str) -> OrderResult:
        """撤单"""

    @abstractmethod
    def get_positions(self) -> list[Position]:
        """获取当前持仓"""

    @abstractmethod
    def get_account(self) -> AccountInfo:
        """获取账户信息"""

    @abstractmethod
    def disconnect(self) -> None:
        """断开连接"""

    @property
    def is_connected(self) -> bool:
        return False

    def get_today_entrusts(self) -> list[Entrust]:
        return []

    def get_today_trades(self) -> list[dict]:
        return []


class EasyTraderBroker(BrokerBase):
    """easytrader 券商接口实现

    支持同花顺、通达信等客户端的自动化操作
    需要先安装 easytrader 并打开券商客户端
    """

    def __init__(self, broker_type: str = "ths", exe_path: str = ""):
        self.m_broker_type = broker_type
        self.m_exe_path = exe_path
        self.m_trader = None
        self.m_connected = False
        self.m_max_retries = 3
        self.m_retry_delay = 2.0

    @property
    def is_connected(self) -> bool:
        return self.m_connected and self.m_trader is not None

    def connect(self) -> bool:
        try:
            import easytrader
            logger.info(f"BROKER_CONNECT type={self.m_broker_type} exe={self.m_exe_path or 'auto'}")
            self.m_trader = easytrader.use(self.m_broker_type)
            if self.m_exe_path:
                self.m_trader.prepare(exe_path=self.m_exe_path)
            else:
                self.m_trader.prepare()
            self.m_connected = True
            logger.info("BROKER_CONNECT_SUCCESS")
            return True
        except Exception as e:
            self.m_connected = False
            logger.error(f"BROKER_CONNECT_FAILED error={e}")
            return False

    def _ensure_connected(self) -> bool:
        if self.is_connected:
            return True
        logger.info("BROKER_RECONNECT")
        return self.connect()

    def _with_retry(self, action_name: str, action_fn, max_retries: int = None):
        retries = max_retries or self.m_max_retries
        last_error = None
        for attempt in range(retries):
            try:
                if not self._ensure_connected():
                    raise ConnectionError("券商客户端未连接")
                return action_fn()
            except Exception as e:
                last_error = e
                logger.warning(f"BROKER_{action_name}_RETRY attempt={attempt + 1}/{retries} error={e}")
                if attempt < retries - 1:
                    self.m_connected = False
                    time.sleep(self.m_retry_delay)
        logger.error(f"BROKER_{action_name}_FAILED after {retries} retries: {last_error}")
        return None

    def buy(self, symbol: str, price: float, volume: int) -> OrderResult:
        _audit_log("BUY", {"symbol": symbol, "price": price, "volume": volume})
        logger.info(f"BROKER_BUY symbol={symbol} price={price} volume={volume}")

        result = self._with_retry("BUY", lambda: self.m_trader.buy(security=symbol, price=price, amount=volume))
        if result is None:
            _audit_log("BUY_FAILED", {"symbol": symbol, "price": price, "volume": volume, "error": "重试失败"})
            return OrderResult(success=False, message="买入失败：重试后仍无法连接券商客户端")

        order_result = OrderResult(
            success=True,
            order_id=str(result.get("entrust_no", "")),
            message=str(result),
        )
        logger.info(f"BROKER_BUY_RESULT order_id={order_result.order_id}")
        _audit_log("BUY_RESULT", {"order_id": order_result.order_id, "message": order_result.message})
        return order_result

    def sell(self, symbol: str, price: float, volume: int) -> OrderResult:
        _audit_log("SELL", {"symbol": symbol, "price": price, "volume": volume})
        logger.info(f"BROKER_SELL symbol={symbol} price={price} volume={volume}")

        result = self._with_retry("SELL", lambda: self.m_trader.sell(security=symbol, price=price, amount=volume))
        if result is None:
            _audit_log("SELL_FAILED", {"symbol": symbol, "price": price, "volume": volume, "error": "重试失败"})
            return OrderResult(success=False, message="卖出失败：重试后仍无法连接券商客户端")

        order_result = OrderResult(
            success=True,
            order_id=str(result.get("entrust_no", "")),
            message=str(result),
        )
        logger.info(f"BROKER_SELL_RESULT order_id={order_result.order_id}")
        _audit_log("SELL_RESULT", {"order_id": order_result.order_id, "message": order_result.message})
        return order_result

    def cancel(self, order_id: str) -> OrderResult:
        _audit_log("CANCEL", {"order_id": order_id})
        logger.info(f"BROKER_CANCEL order_id={order_id}")

        result = self._with_retry("CANCEL", lambda: self.m_trader.cancel_entrust(entrust_no=order_id))
        if result is None:
            return OrderResult(success=False, message="撤单失败：重试后仍无法连接券商客户端")

        order_result = OrderResult(success=True, message=str(result))
        logger.info(f"BROKER_CANCEL_RESULT order_id={order_id}")
        _audit_log("CANCEL_RESULT", {"order_id": order_id, "message": order_result.message})
        return order_result

    def get_positions(self) -> list[Position]:
        def _get():
            raw = self.m_trader.position
            if not raw:
                return []
            positions = []
            items = raw if isinstance(raw, list) else [raw]
            for item in items:
                if not isinstance(item, dict):
                    continue
                positions.append(Position(
                    symbol=str(item.get("证券代码", "")),
                    name=str(item.get("证券名称", "")),
                    volume=int(item.get("股票余额", 0)),
                    available_volume=int(item.get("可用余额", 0)),
                    cost_price=float(item.get("成本价", 0)),
                    current_price=float(item.get("当前价", 0)),
                    pnl=float(item.get("盈亏", 0)),
                    pnl_pct=float(item.get("盈亏%", 0)),
                ))
            return positions

        result = self._with_retry("POSITIONS", _get)
        return result if result is not None else []

    def get_account(self) -> AccountInfo:
        def _get():
            raw = self.m_trader.balance
            if isinstance(raw, list):
                raw = raw[0] if raw else {}
            return AccountInfo(
                total_assets=float(raw.get("总资产", 0)),
                cash=float(raw.get("可用金额", 0)),
                market_value=float(raw.get("股票市值", 0)),
            )

        result = self._with_retry("ACCOUNT", _get)
        return result if result is not None else AccountInfo()

    def get_today_entrusts(self) -> list[Entrust]:
        def _get():
            raw = self.m_trader.today_entrusts
            if not raw:
                return []
            entrusts = []
            items = raw if isinstance(raw, list) else [raw]
            for item in items:
                if not isinstance(item, dict):
                    continue
                entrusts.append(Entrust(
                    entrust_no=str(item.get("委托编号", "")),
                    symbol=str(item.get("证券代码", "")),
                    name=str(item.get("证券名称", "")),
                    direction=str(item.get("操作", "")),
                    price=float(item.get("委托价格", 0)),
                    volume=int(item.get("委托数量", 0)),
                    filled_volume=int(item.get("成交数量", 0)),
                    status=str(item.get("状态", "")),
                    order_time=str(item.get("委托时间", "")),
                ))
            return entrusts

        result = self._with_retry("ENTRUSTS", _get)
        return result if result is not None else []

    def get_today_trades(self) -> list[dict]:
        def _get():
            raw = self.m_trader.today_trades
            if not raw:
                return []
            trades = []
            items = raw if isinstance(raw, list) else [raw]
            for item in items:
                if isinstance(item, dict):
                    trades.append(item)
            return trades

        result = self._with_retry("TRADES", _get)
        return result if result is not None else []

    def disconnect(self) -> None:
        logger.info("BROKER_DISCONNECT")
        self.m_trader = None
        self.m_connected = False


class QMTBroker(BrokerBase):
    """QMT（迅投）券商接口实现

    需要安装 QMT 客户端并开通量化交易权限
    通过 xtquant 包连接
    """

    def __init__(self, qmt_path: str = ""):
        self.m_qmt_path = qmt_path
        self.m_xt = None
        self.m_trader = None
        self.m_connected = False

    @property
    def is_connected(self) -> bool:
        return self.m_connected

    def connect(self) -> bool:
        try:
            from xtquant import xttrader
            from xtquant.xttype import StockAccount
            self.m_xt = xttrader
            path = self.m_qmt_path or r"C:\国金QMT交易端\userdata_mini"
            session_id = self.m_xt.create_trader_session(path=path)
            self.m_trader = session_id
            self.m_connected = True
            return True
        except Exception as e:
            logger.error(f"QMT 连接失败: {e}")
            return False

    def buy(self, symbol: str, price: float, volume: int) -> OrderResult:
        try:
            from xtquant.xttype import StockAccount
            account = StockAccount("your_account", "STOCK")
            order_id = self.m_xt.order_stock(
                account, symbol, self.m_xt.STOCK_BUY, price, volume
            )
            return OrderResult(success=True, order_id=str(order_id))
        except Exception as e:
            return OrderResult(success=False, message=str(e))

    def sell(self, symbol: str, price: float, volume: int) -> OrderResult:
        try:
            from xtquant.xttype import StockAccount
            account = StockAccount("your_account", "STOCK")
            order_id = self.m_xt.order_stock(
                account, symbol, self.m_xt.STOCK_SELL, price, volume
            )
            return OrderResult(success=True, order_id=str(order_id))
        except Exception as e:
            return OrderResult(success=False, message=str(e))

    def cancel(self, order_id: str) -> OrderResult:
        try:
            self.m_xt.cancel_order_stock(int(order_id))
            return OrderResult(success=True)
        except Exception as e:
            return OrderResult(success=False, message=str(e))

    def get_positions(self) -> list[Position]:
        return []

    def get_account(self) -> AccountInfo:
        return AccountInfo()

    def disconnect(self) -> None:
        self.m_trader = None
        self.m_connected = False


class SimBroker(BrokerBase):
    """模拟券商 — 用于开发和测试，不连接真实券商"""

    def __init__(self, initial_cash: float = 100_000.0):
        from trader.simulator import Simulator
        self.m_simulator = Simulator(initial_cash=initial_cash)
        self.m_connected = True

    @property
    def is_connected(self) -> bool:
        return True

    def connect(self) -> bool:
        return True

    def buy(self, symbol: str, price: float, volume: int) -> OrderResult:
        from strategy.base import Signal, Direction
        signal = Signal(symbol=symbol, direction=Direction.BUY, price=price, volume=volume)
        record = self.m_simulator.process_signal(signal)
        if record and record.status == "filled":
            return OrderResult(success=True, filled_price=price, filled_volume=volume, message="模拟买入成功")
        return OrderResult(success=False, message=record.reason if record else "买入失败")

    def sell(self, symbol: str, price: float, volume: int) -> OrderResult:
        from strategy.base import Signal, Direction
        signal = Signal(symbol=symbol, direction=Direction.SELL, price=price, volume=volume)
        record = self.m_simulator.process_signal(signal)
        if record and record.status == "filled":
            return OrderResult(success=True, filled_price=price, filled_volume=volume, message="模拟卖出成功")
        return OrderResult(success=False, message=record.reason if record else "卖出失败")

    def cancel(self, order_id: str) -> OrderResult:
        return OrderResult(success=True, message="模拟撤单")

    def get_positions(self) -> list[Position]:
        return [
            Position(symbol=sym, volume=p.volume, cost_price=p.cost_price, current_price=p.current_price)
            for sym, p in self.m_simulator.positions.items()
        ]

    def get_account(self) -> AccountInfo:
        status = self.m_simulator.get_status()
        return AccountInfo(
            total_assets=status["total_equity"],
            cash=status["cash"],
            market_value=status["total_equity"] - status["cash"],
        )

    def disconnect(self) -> None:
        pass


def create_broker(config_path: str = "config/settings.yaml") -> BrokerBase:
    """根据配置创建券商实例"""
    config_file = Path(__file__).parent.parent / config_path
    if config_file.exists():
        with open(config_file) as f:
            config = yaml.safe_load(f)
    else:
        config = {}

    broker_config = config.get("broker", {})
    broker_type = broker_config.get("type", "sim")

    if broker_type == "easytrader":
        return EasyTraderBroker(
            broker_type=broker_config.get("client", "ths"),
            exe_path=broker_config.get("exe_path", ""),
        )
    elif broker_type == "qmt":
        return QMTBroker(qmt_path=broker_config.get("qmt_path", ""))
    else:
        return SimBroker(initial_cash=config.get("trading", {}).get("initial_cash", 100_000))
