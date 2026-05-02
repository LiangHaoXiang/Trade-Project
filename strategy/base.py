"""
策略基类 - 定义所有策略的统一接口
"""
from abc import ABC, abstractmethod
from dataclasses import dataclass
from enum import Enum

import pandas as pd


class Direction(Enum):
    BUY = "BUY"
    SELL = "SELL"
    HOLD = "HOLD"


@dataclass
class Signal:
    symbol: str
    direction: Direction
    price: float
    volume: int = 0
    reason: str = ""


class BaseStrategy(ABC):
    """策略基类，所有策略必须实现 init 和 next 方法"""

    def __init__(self, name: str = "base"):
        self.name = name

    @abstractmethod
    def init(self, data: pd.DataFrame) -> None:
        """策略初始化，计算指标"""

    @abstractmethod
    def next(self, idx: int, data: pd.DataFrame) -> Signal:
        """逐 bar 产生信号"""
