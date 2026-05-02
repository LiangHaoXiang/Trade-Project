"""
生成测试数据 - 股票量化交易系统
生成 A 股模拟行情数据，运行回测，保存到 SQLite
Python 和 C# Dashboard 共享同一数据库
"""
import sqlite3
import sys
from datetime import datetime, timedelta
from pathlib import Path

import numpy as np
import pandas as pd

# 项目根目录
PROJECT_ROOT = Path(__file__).resolve().parent.parent
DB_PATH = PROJECT_ROOT / "data" / "cache" / "market.db"
LOG_DIR = PROJECT_ROOT / "data" / "logs"
LOG_PATH = LOG_DIR / "trade.log"

# 测试股票配置: (代码, 基础价格, 日波动率, 日漂移)
TEST_STOCKS = [
    ("000001", 12.50, 0.020, 0.0003),   # 平安银行
    ("600036", 35.00, 0.018, 0.0002),   # 招商银行
    ("000858", 160.00, 0.022, 0.0001),  # 五粮液
]

START_DATE = "2024-01-02"
END_DATE = "2025-03-31"
INITIAL_CASH = 100_000.0
SHORT_WINDOW = 5
LONG_WINDOW = 20


def generate_ohlcv(symbol: str, base_price: float, volatility: float, drift: float,
                   start: str, end: str, seed: int) -> pd.DataFrame:
    """生成模拟 OHLCV 数据"""
    np.random.seed(seed)
    dates = pd.date_range(start, end, freq="B")  # 仅交易日
    n = len(dates)

    # 对数价格随机游走
    log_returns = np.random.normal(drift, volatility, n)
    log_prices = np.log(base_price) + np.cumsum(log_returns)
    closes = np.exp(log_prices)

    # 生成 OHLC
    intraday_range = closes * volatility * 0.5
    opens = closes * (1 + np.random.normal(0, 0.005, n))
    highs = np.maximum(opens, closes) + np.abs(np.random.normal(0, 1, n)) * intraday_range
    lows = np.minimum(opens, closes) - np.abs(np.random.normal(0, 1, n)) * intraday_range

    # 成交量 (手), 与价格波动正相关
    base_vol = 50_000_000 if base_price < 20 else 20_000_000 if base_price < 50 else 8_000_000
    volumes = base_vol * (1 + np.abs(log_returns) * 20) * (1 + np.random.normal(0, 0.3, n))
    volumes = np.clip(volumes, base_vol * 0.3, base_vol * 5).astype(float)

    amounts = volumes * closes

    df = pd.DataFrame({
        "symbol": symbol,
        "date": dates.strftime("%Y-%m-%d"),
        "open": np.round(opens, 2),
        "high": np.round(highs, 2),
        "low": np.round(lows, 2),
        "close": np.round(closes, 2),
        "volume": np.round(volumes, 0),
        "amount": np.round(amounts, 2),
    })
    return df


def init_database(conn: sqlite3.Connection):
    """创建所有表"""
    conn.executescript("""
        CREATE TABLE IF NOT EXISTS daily (
            symbol TEXT, date TEXT, open REAL, high REAL, low REAL,
            close REAL, volume REAL, amount REAL,
            PRIMARY KEY (symbol, date)
        );
        CREATE TABLE IF NOT EXISTS backtest_runs (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            symbol TEXT NOT NULL,
            strategy_name TEXT NOT NULL,
            start_date TEXT NOT NULL,
            end_date TEXT NOT NULL,
            initial_cash REAL NOT NULL,
            final_value REAL NOT NULL,
            total_return_pct REAL,
            max_drawdown_pct REAL,
            trade_count INTEGER,
            run_at TEXT NOT NULL,
            parameters_json TEXT
        );
        CREATE TABLE IF NOT EXISTS trades (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            backtest_run_id INTEGER NOT NULL,
            symbol TEXT NOT NULL,
            direction TEXT NOT NULL,
            price REAL NOT NULL,
            volume INTEGER NOT NULL,
            trade_date TEXT NOT NULL,
            reason TEXT,
            FOREIGN KEY (backtest_run_id) REFERENCES backtest_runs(id)
        );
        CREATE TABLE IF NOT EXISTS equity_curve (
            id INTEGER PRIMARY KEY AUTOINCREMENT,
            backtest_run_id INTEGER NOT NULL,
            date TEXT NOT NULL,
            equity REAL NOT NULL,
            FOREIGN KEY (backtest_run_id) REFERENCES backtest_runs(id)
        );
    """)
    conn.commit()


def insert_daily_data(conn: sqlite3.Connection, all_data: list[pd.DataFrame]):
    """插入日线数据"""
    for df in all_data:
        conn.execute("DELETE FROM daily WHERE symbol = ?", (df["symbol"].iloc[0],))
        df.to_sql("daily", conn, if_exists="append", index=False)
    conn.commit()
    total = sum(len(df) for df in all_data)
    print(f"  插入 {total} 条日线数据 ({len(all_data)} 只股票)")


def run_backtest(data: pd.DataFrame) -> dict:
    """使用项目自身的回测引擎运行回测"""
    # 添加项目根到 path 以导入模块
    sys.path.insert(0, str(PROJECT_ROOT))
    from strategy.ma_cross import MACrossStrategy
    from backtest.engine import BacktestEngine

    # 确保有 date 列作索引
    if "date" in data.columns:
        data = data.copy()
        data["date"] = pd.to_datetime(data["date"])
        data = data.set_index("date")

    strategy = MACrossStrategy(short_window=SHORT_WINDOW, long_window=LONG_WINDOW)
    engine = BacktestEngine(initial_cash=INITIAL_CASH)
    result = engine.run(strategy, data)

    # 计算最大回撤
    peak = max(result.equity_curve)
    max_dd = max(1 - v / peak for v in result.equity_curve) * 100 if peak > 0 else 0

    return {
        "result": result,
        "max_dd_pct": max_dd,
        "total_return_pct": (result.final_value - result.initial_cash) / result.initial_cash * 100,
    }


def save_backtest_result(conn: sqlite3.Connection, bt: dict, dates: list[str]):
    """保存回测结果到数据库"""
    result = bt["result"]
    symbol = result.trades[0].symbol if result.trades else "000001"

    conn.execute("DELETE FROM trades")
    conn.execute("DELETE FROM equity_curve")
    conn.execute("DELETE FROM backtest_runs")

    cursor = conn.execute("""
        INSERT INTO backtest_runs (symbol, strategy_name, start_date, end_date,
            initial_cash, final_value, total_return_pct, max_drawdown_pct,
            trade_count, run_at, parameters_json)
        VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?)
    """, (
        symbol, "ma_cross", START_DATE, END_DATE,
        result.initial_cash, result.final_value,
        bt["total_return_pct"], bt["max_dd_pct"],
        len(result.trades), datetime.now().strftime("%Y-%m-%d %H:%M:%S"),
        f'{{"short_window": {SHORT_WINDOW}, "long_window": {LONG_WINDOW}}}'
    ))
    run_id = cursor.lastrowid

    for t in result.trades:
        conn.execute("""
            INSERT INTO trades (backtest_run_id, symbol, direction, price, volume, trade_date, reason)
            VALUES (?, ?, ?, ?, ?, ?, ?)
        """, (run_id, t.symbol, t.direction.value, t.price, t.volume, str(t.date.date()), t.reason))

    for i, eq in enumerate(result.equity_curve):
        conn.execute("""
            INSERT INTO equity_curve (backtest_run_id, date, equity)
            VALUES (?, ?, ?)
        """, (run_id, dates[i] if i < len(dates) else END_DATE, eq))

    conn.commit()
    print(f"  回测结果: 初始 {result.initial_cash:,.0f} → 最终 {result.final_value:,.2f}")
    print(f"  收益率 {bt['total_return_pct']:+.2f}%, 最大回撤 {bt['max_dd_pct']:.2f}%, 交易 {len(result.trades)} 次")


def generate_log_file():
    """生成示例日志文件"""
    LOG_DIR.mkdir(parents=True, exist_ok=True)

    entries = [
        ("INFO", "main", "系统启动"),
        ("INFO", "data.manager", "加载数据 000001: 300 条"),
        ("INFO", "data.manager", "加载数据 600036: 300 条"),
        ("INFO", "data.manager", "加载数据 000858: 300 条"),
        ("INFO", "strategy.ma_cross", "初始化策略 MA5/20"),
        ("INFO", "backtest.engine", "开始回测 000001 [2024-01-02 ~ 2025-03-31]"),
        ("DEBUG", "strategy.ma_cross", "2024-02-05 金叉信号 MA5/20 price=12.85"),
        ("INFO", "backtest.engine", "买入 000001 @ 12.85 vol=7400"),
        ("DEBUG", "strategy.ma_cross", "2024-03-15 死叉信号 MA5/20 price=11.92"),
        ("INFO", "backtest.engine", "卖出 000001 @ 11.92 vol=7400"),
        ("WARNING", "risk.manager", "单笔亏损超过 5%: 000001 -7.24%"),
        ("DEBUG", "strategy.ma_cross", "2024-04-10 金叉信号 MA5/20 price=13.10"),
        ("INFO", "backtest.engine", "买入 000001 @ 13.10 vol=6700"),
        ("DEBUG", "strategy.ma_cross", "2024-05-20 死叉信号 MA5/20 price=12.45"),
        ("INFO", "backtest.engine", "卖出 000001 @ 12.45 vol=6700"),
        ("INFO", "backtest.engine", "回测完成: 最终资金 105,234.56"),
        ("INFO", "main", "BACKTEST_COMPLETE final_value=105234.56 trades=10"),
        ("WARNING", "data.manager", "网络连接超时，使用缓存数据"),
        ("ERROR", "data.manager", "AKShare 接口返回异常: HTTP 503"),
        ("INFO", "data.manager", "回退到 efinance 数据源"),
        ("INFO", "main", "系统关闭"),
    ]

    base_time = datetime(2025, 4, 1, 9, 0, 0)
    lines = []
    for i, (level, source, msg) in enumerate(entries):
        ts = base_time + timedelta(minutes=i * 3)
        lines.append(f"{ts.strftime('%Y-%m-%d %H:%M:%S')}|{level}|{source}|{msg}")

    LOG_PATH.write_text("\n".join(lines), encoding="utf-8")
    print(f"  生成 {len(lines)} 条日志 → {LOG_PATH}")


def main():
    print("=" * 50)
    print("生成测试数据")
    print("=" * 50)

    # 1. 生成行情数据
    print("\n[1/3] 生成行情数据...")
    all_data = []
    for i, (symbol, base, vol, drift) in enumerate(TEST_STOCKS):
        df = generate_ohlcv(symbol, base, vol, drift, START_DATE, END_DATE, seed=42 + i)
        all_data.append(df)
        print(f"  {symbol}: {len(df)} 天, 价格范围 {df['close'].min():.2f} ~ {df['close'].max():.2f}")

    # 2. 运行回测 & 保存
    print("\n[2/3] 运行回测 & 保存到数据库...")
    DB_PATH.parent.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    try:
        init_database(conn)
        insert_daily_data(conn, all_data)

        # 对 000001 运行回测
        bt_data = all_data[0].copy()
        bt_data["date"] = pd.to_datetime(bt_data["date"])
        bt_data = bt_data.set_index("date")
        bt = run_backtest(bt_data)

        dates = all_data[0]["date"].tolist()
        save_backtest_result(conn, bt, dates)
    finally:
        conn.close()

    # 3. 生成日志
    print("\n[3/3] 生成日志文件...")
    generate_log_file()

    print("\n" + "=" * 50)
    print("测试数据生成完成!")
    print(f"  数据库: {DB_PATH}")
    print(f"  日志:   {LOG_PATH}")
    print("=" * 50)


if __name__ == "__main__":
    main()
