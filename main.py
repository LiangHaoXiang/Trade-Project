"""
股票量化交易系统入口
"""
import argparse
import json
import sys
from datetime import datetime

import numpy as np
import pandas as pd

import json as json_mod

from data.manager import DataManager
from strategy.ma_cross import MACrossStrategy
from strategy.mean_reversion import MeanReversionStrategy
from strategy.momentum import MomentumStrategy
from strategy.macd_divergence import MACDDivergenceStrategy
from strategy.rsi_extreme import RSIExtremeStrategy
from strategy.bollinger_breakout import BollingerBreakoutStrategy
from strategy.kdj_cross import KDJCrossoverStrategy
from strategy.td_sequential import TDSequentialStrategy
from strategy.wave_trend import WaveTrendStrategy
from backtest.engine import BacktestEngine
from backtest.report import build_report, print_text_summary, to_json
from monitor.logger import get_logger
from news.news_fetcher import fetch_news

STRATEGY_REGISTRY = {
    "ma_cross": MACrossStrategy,
    "mean_reversion": MeanReversionStrategy,
    "momentum": MomentumStrategy,
    "macd_divergence": MACDDivergenceStrategy,
    "rsi_extreme": RSIExtremeStrategy,
    "bollinger_breakout": BollingerBreakoutStrategy,
    "kdj_cross": KDJCrossoverStrategy,
    "td_sequential": TDSequentialStrategy,
    "wave_trend": WaveTrendStrategy,
}

logger = get_logger("main")


def main():
    parser = argparse.ArgumentParser(description="股票量化交易系统")
    parser.add_argument("command", nargs="?", default="backtest",
                        choices=["download", "download-all", "backtest", "run",
                                 "stock-list", "daily-data", "check-update", "news"],
                        help="执行命令（默认 backtest）")
    parser.add_argument("--symbol", default="000001", help="股票代码（默认 000001 平安银行）")
    parser.add_argument("--start", default=None, help="起始日期")
    parser.add_argument("--end", default=None, help="结束日期")
    parser.add_argument("--initial-cash", type=float, default=None, help="初始资金")
    parser.add_argument("--strategy", default="ma_cross", choices=list(STRATEGY_REGISTRY.keys()), help="策略名称")
    parser.add_argument("--strategy-params", default=None, help="策略专属参数 JSON 字符串")
    parser.add_argument("--json", action="store_true", help="输出 JSON 格式结果")
    args = parser.parse_args()

    dm = DataManager()

    if args.command == "download":
        start = args.start or "2024-01-01"
        end = args.end or datetime.now().strftime("%Y-%m-%d")
        logger.info(f"DOWNLOAD_START symbol={args.symbol}")
        print(f"正在下载 {args.symbol} 行情数据...")
        dm.download(args.symbol, start, end)
        logger.info(f"DOWNLOAD_COMPLETE symbol={args.symbol}")
        print("下载完成")

    elif args.command == "download-all":
        start = args.start or "2009-01-01"
        logger.info(f"DOWNLOAD_ALL_START from={start}")
        dm.download_all_history(start)
        logger.info("DOWNLOAD_ALL_COMPLETE")

    elif args.command == "backtest":
        start = args.start or "2024-01-01"
        end = args.end or "2025-01-01"
        data = dm.load(args.symbol, start, end)
        if data.empty:
            logger.info("NO_LOCAL_DATA using mock data")
            print("本地无数据，使用模拟数据进行演示...")
            data = _generate_mock_data(args.symbol)

        initial = args.initial_cash or 100_000
        strategy_name = args.strategy

        strategy_params = {}
        if args.strategy_params:
            strategy_params = json.loads(args.strategy_params)

        strategy_cls = STRATEGY_REGISTRY.get(strategy_name, MACrossStrategy)
        strategy = strategy_cls(**strategy_params)

        logger.info(f"BACKTEST_START strategy={strategy_name} symbol={args.symbol} params={strategy_params}")
        print(f"正在回测 {args.symbol} [{start} ~ {end}] 策略={strategy_name}，共 {len(data)} 条数据...")

        engine = BacktestEngine(initial_cash=initial)
        result = engine.run(strategy, data)

        dates = data.index if isinstance(data.index, pd.DatetimeIndex) else data.get("date")
        report = build_report(result, dates=dates)

        if not args.json:
            print_text_summary(report)
        else:
            print("JSON_OUTPUT_START")
            print(to_json(report))

        logger.info(f"BACKTEST_COMPLETE final_value={result.final_value} trades={len(result.trades)}")

    elif args.command == "run":
        logger.info("RUN_START (not implemented)")
        print("模拟/实盘运行（待实现）")

    elif args.command == "stock-list":
        stocks = dm.get_stock_list()
        print("JSON_OUTPUT_START")
        print(json.dumps(stocks, ensure_ascii=False))

    elif args.command == "daily-data":
        start = args.start or "2009-01-01"
        end = args.end or datetime.now().strftime("%Y-%m-%d")
        data = dm.load(args.symbol, start, end)
        if data.empty:
            print("JSON_OUTPUT_START")
            print("[]")
        else:
            records = data.reset_index().to_dict(orient="records")
            for r in records:
                r["date"] = str(r["date"])[:10]
            print("JSON_OUTPUT_START")
            print(json.dumps(records, ensure_ascii=False))

    elif args.command == "check-update":
        result = dm.check_and_update()
        print(result)

    elif args.command == "news":
        logger.info("NEWS_FETCH_START")
        if not args.json:
            print("正在获取财经新闻...")
        result = fetch_news(max_items=50)
        print("JSON_OUTPUT_START")
        print(json_mod.dumps(result, ensure_ascii=False))
        logger.info(f"NEWS_FETCH_COMPLETE items={len(result.get('items', []))}")


def _generate_mock_data(symbol: str = "000001", days: int = 200) -> pd.DataFrame:
    """生成模拟行情数据，用于无网络时测试"""
    np.random.seed(42)
    dates = pd.date_range("2024-01-01", periods=days, freq="B")
    prices = 15 + np.cumsum(np.random.randn(days) * 0.3)
    return pd.DataFrame({
        "symbol": symbol,
        "open": prices - 0.1,
        "high": prices + 0.2,
        "low": prices - 0.2,
        "close": prices,
        "volume": 1_000_000.0,
    }, index=dates)


if __name__ == "__main__":
    main()
