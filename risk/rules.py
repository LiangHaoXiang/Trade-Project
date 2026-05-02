"""
风控规则模块
定义各类风控检查规则，每条规则独立、可组合
"""
from dataclasses import dataclass


@dataclass
class RiskCheckResult:
    passed: bool
    rule_name: str
    message: str


class MaxPositionRule:
    """单笔最大仓位限制"""

    def __init__(self, max_pct: float = 0.2):
        self.max_pct = max_pct

    def check(self, order_value: float, total_equity: float) -> RiskCheckResult:
        if total_equity <= 0:
            return RiskCheckResult(False, "max_position", "总权益为零")
        ratio = order_value / total_equity
        if ratio > self.max_pct:
            return RiskCheckResult(
                False, "max_position",
                f"单笔仓位 {ratio:.1%} 超过限制 {self.max_pct:.1%}",
            )
        return RiskCheckResult(True, "max_position", "")


class MaxDailyLossRule:
    """单日最大亏损限制"""

    def __init__(self, max_pct: float = 0.03):
        self.max_pct = max_pct

    def check(self, today_pnl: float, total_equity: float) -> RiskCheckResult:
        if total_equity <= 0:
            return RiskCheckResult(False, "max_daily_loss", "总权益为零")
        loss_pct = abs(today_pnl) / total_equity if today_pnl < 0 else 0
        if loss_pct > self.max_pct:
            return RiskCheckResult(
                False, "max_daily_loss",
                f"今日亏损 {loss_pct:.1%} 超过限制 {self.max_pct:.1%}",
            )
        return RiskCheckResult(True, "max_daily_loss", "")


class StopLossRule:
    """个股止损规则"""

    def __init__(self, stop_pct: float = 0.08):
        self.stop_pct = stop_pct

    def check(self, cost_price: float, current_price: float) -> RiskCheckResult:
        if cost_price <= 0:
            return RiskCheckResult(True, "stop_loss", "")
        loss_pct = (cost_price - current_price) / cost_price
        if loss_pct > self.stop_pct:
            return RiskCheckResult(
                False, "stop_loss",
                f"个股亏损 {loss_pct:.1%} 触发止损 {self.stop_pct:.1%}",
            )
        return RiskCheckResult(True, "stop_loss", "")


class MaxHoldingsRule:
    """最大持仓数量限制"""

    def __init__(self, max_count: int = 5):
        self.max_count = max_count

    def check(self, current_holdings: int, is_new_buy: bool) -> RiskCheckResult:
        if is_new_buy and current_holdings >= self.max_count:
            return RiskCheckResult(
                False, "max_holdings",
                f"持仓数 {current_holdings} 已达上限 {self.max_count}",
            )
        return RiskCheckResult(True, "max_holdings", "")


class LimitPriceRule:
    """涨跌停不交易规则"""

    def __init__(self, limit_pct: float = 0.095):
        self.limit_pct = limit_pct

    def check(self, prev_close: float, current_price: float, direction: str) -> RiskCheckResult:
        if prev_close <= 0:
            return RiskCheckResult(True, "limit_price", "")
        change_pct = (current_price - prev_close) / prev_close

        if direction == "BUY" and change_pct >= self.limit_pct:
            return RiskCheckResult(False, "limit_price", f"涨停板 {change_pct:.1%} 禁止买入")
        if direction == "SELL" and change_pct <= -self.limit_pct:
            return RiskCheckResult(False, "limit_price", f"跌停板 {change_pct:.1%} 禁止卖出")

        return RiskCheckResult(True, "limit_price", "")
