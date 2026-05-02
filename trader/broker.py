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
from abc import ABC, abstractmethod
from dataclasses import dataclass
from typing import Protocol

import yaml


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


class EasyTraderBroker(BrokerBase):
    """easytrader 券商接口实现

    支持同花顺、通达信等客户端的自动化操作
    需要先安装 easytrader 并打开券商客户端
    """

    def __init__(self, broker_type: str = "ths", exe_path: str = ""):
        self.m_broker_type = broker_type
        self.m_exe_path = exe_path
        self.m_trader = None

    def connect(self) -> bool:
        try:
            import easytrader
            self.m_trader = easytrader.use(self.m_broker_type)
            if self.m_exe_path:
                self.m_trader.prepare(exe_path=self.m_exe_path)
            else:
                self.m_trader.prepare()
            return True
        except Exception as e:
            print(f"easytrader 连接失败: {e}")
            return False

    def buy(self, symbol: str, price: float, volume: int) -> OrderResult:
        try:
            result = self.m_trader.buy(security=symbol, price=price, amount=volume)
            return OrderResult(
                success=True,
                order_id=str(result.get("entrust_no", "")),
                message=str(result),
            )
        except Exception as e:
            return OrderResult(success=False, message=str(e))

    def sell(self, symbol: str, price: float, volume: int) -> OrderResult:
        try:
            result = self.m_trader.sell(security=symbol, price=price, amount=volume)
            return OrderResult(
                success=True,
                order_id=str(result.get("entrust_no", "")),
                message=str(result),
            )
        except Exception as e:
            return OrderResult(success=False, message=str(e))

    def cancel(self, order_id: str) -> OrderResult:
        try:
            result = self.m_trader.cancel_entrust(entrust_no=order_id)
            return OrderResult(success=True, message=str(result))
        except Exception as e:
            return OrderResult(success=False, message=str(e))

    def get_positions(self) -> list[Position]:
        try:
            raw = self.m_trader.position
            positions = []
            for item in raw if isinstance(raw, list) else [raw]:
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
        except Exception as e:
            print(f"获取持仓失败: {e}")
            return []

    def get_account(self) -> AccountInfo:
        try:
            raw = self.m_trader.balance
            if isinstance(raw, list):
                raw = raw[0]
            return AccountInfo(
                total_assets=float(raw.get("总资产", 0)),
                cash=float(raw.get("可用金额", 0)),
                market_value=float(raw.get("股票市值", 0)),
            )
        except Exception as e:
            print(f"获取账户信息失败: {e}")
            return AccountInfo()

    def disconnect(self) -> None:
        self.m_trader = None


class QMTBroker(BrokerBase):
    """QMT（迅投）券商接口实现

    需要安装 QMT 客户端并开通量化交易权限
    通过 xtquant 包连接
    """

    def __init__(self, qmt_path: str = ""):
        self.m_qmt_path = qmt_path
        self.m_xt = None
        self.m_trader = None

    def connect(self) -> bool:
        try:
            from xtquant import xttrader
            from xtquant.xttype import StockAccount
            self.m_xt = xttrader
            path = self.m_qmt_path or r"C:\国金QMT交易端\userdata_mini"
            session_id = self.m_xt.create_trader_session(path=path)
            self.m_trader = session_id
            return True
        except Exception as e:
            print(f"QMT 连接失败: {e}")
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


class SimBroker(BrokerBase):
    """模拟券商 — 用于开发和测试，不连接真实券商"""

    def __init__(self, initial_cash: float = 100_000.0):
        from trader.simulator import Simulator
        self.m_simulator = Simulator(initial_cash=initial_cash)

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
    from pathlib import Path

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
