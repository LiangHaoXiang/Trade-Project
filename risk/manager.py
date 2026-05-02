"""
风控管理器
组合所有风控规则，在交易前执行检查
"""
from dataclasses import dataclass, field

from strategy.base import Direction
from risk.rules import (
    RiskCheckResult,
    MaxPositionRule,
    MaxDailyLossRule,
    StopLossRule,
    MaxHoldingsRule,
    LimitPriceRule,
)


@dataclass
class PositionInfo:
    symbol: str
    volume: int
    cost_price: float
    current_price: float = 0.0


@dataclass
class RiskContext:
    total_equity: float = 100_000.0
    today_pnl: float = 0.0
    current_holdings: int = 0
    positions: dict[str, PositionInfo] = field(default_factory=dict)


class RiskManager:
    """风控管理器 — 在信号通过后、下单前执行风控检查"""

    def __init__(
        self,
        max_position_pct: float = 0.2,
        max_daily_loss_pct: float = 0.03,
        stop_loss_pct: float = 0.08,
        max_holdings: int = 5,
        limit_pct: float = 0.095,
    ):
        self.m_max_position = MaxPositionRule(max_position_pct)
        self.m_max_daily_loss = MaxDailyLossRule(max_daily_loss_pct)
        self.m_stop_loss = StopLossRule(stop_loss_pct)
        self.m_max_holdings = MaxHoldingsRule(max_holdings)
        self.m_limit_price = LimitPriceRule(limit_pct)

        self.m_rejected: list[RiskCheckResult] = []

    def check(
        self,
        symbol: str,
        direction: Direction,
        price: float,
        volume: int,
        context: RiskContext,
        prev_close: float = 0.0,
    ) -> RiskCheckResult:
        """执行全部风控检查，返回是否通过

        任何一条规则拒绝则整体拒绝
        """
        self.m_rejected.clear()
        order_value = price * volume
        is_new_buy = direction == Direction.BUY

        # 1. 单笔最大仓位
        result = self.m_max_position.check(order_value, context.total_equity)
        if not result.passed:
            self.m_rejected.append(result)
            return result

        # 2. 单日最大亏损
        result = self.m_max_daily_loss.check(context.today_pnl, context.total_equity)
        if not result.passed:
            self.m_rejected.append(result)
            return result

        # 3. 最大持仓数（仅买入时检查）
        if is_new_buy:
            result = self.m_max_holdings.check(context.current_holdings, is_new_buy)
            if not result.passed:
                self.m_rejected.append(result)
                return result

        # 4. 个股止损（仅卖出时检查持仓盈亏）
        if direction == Direction.SELL and symbol in context.positions:
            pos = context.positions[symbol]
            result = self.m_stop_loss.check(pos.cost_price, price)
            if not result.passed:
                self.m_rejected.append(result)
                return result

        # 5. 涨跌停不交易
        if prev_close > 0:
            result = self.m_limit_price.check(prev_close, price, direction.value)
            if not result.passed:
                self.m_rejected.append(result)
                return result

        return RiskCheckResult(True, "all", "风控检查通过")

    @property
    def rejected_rules(self) -> list[RiskCheckResult]:
        return self.m_rejected.copy()
