"""
数据管理模块 - 负责行情数据的下载、存储和读取
支持 Tushare、efinance、AKShare 多数据源自动切换
"""
import os
import sqlite3
from datetime import datetime
from pathlib import Path
from time import sleep

import pandas as pd
import yaml

from config.secrets import get_secret

DB_DIR = Path(__file__).parent / "cache"
DB_PATH = DB_DIR / "market.db"

CONFIG_PATH = Path(__file__).parent.parent / "config" / "settings.yaml"


def _load_config() -> dict:
    with open(CONFIG_PATH) as f:
        return yaml.safe_load(f)


def _ensure_db():
    DB_DIR.mkdir(parents=True, exist_ok=True)
    conn = sqlite3.connect(DB_PATH)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS daily (
            symbol TEXT,
            date TEXT,
            open REAL,
            high REAL,
            low REAL,
            close REAL,
            volume REAL,
            amount REAL,
            PRIMARY KEY (symbol, date)
        )
    """)
    conn.execute("""
        CREATE TABLE IF NOT EXISTS stock_basic (
            symbol TEXT PRIMARY KEY,
            name TEXT
        )
    """)
    conn.commit()
    conn.close()


def _to_ts_code(symbol: str) -> str:
    """纯数字代码转 Tushare ts_code 格式（如 000001 → 000001.SZ）"""
    if "." in symbol:
        return symbol
    if symbol.startswith("6"):
        return f"{symbol}.SH"
    if symbol.startswith("0") or symbol.startswith("3"):
        return f"{symbol}.SZ"
    return f"{symbol}.BJ"


def _get_tushare_token() -> str:
    """从环境变量或配置文件获取 Tushare token"""
    token = get_secret("TUSHARE_TOKEN")
    if not token:
        config = _load_config()
        token = config.get("data", {}).get("tushare_token", "")
    if not token:
        raise ValueError(
            "请先设置 Tushare token：\n"
            "  方式一：在 .env 文件中设置 TUSHARE_TOKEN=xxx\n"
            "  方式二：编辑 config/settings.yaml 中 tushare_token 字段\n"
            "  方式三：设置系统环境变量 TUSHARE_TOKEN"
        )
    return token


def _download_tushare(symbol: str, start: str, end: str) -> pd.DataFrame:
    """通过 Tushare 下载 A 股日线行情（未复权）"""
    import tushare as ts

    token = _get_tushare_token()
    pro = ts.pro_api(token)

    ts_code = _to_ts_code(symbol)
    start_fmt = start.replace("-", "")
    end_fmt = end.replace("-", "")

    df = pro.daily(ts_code=ts_code, start_date=start_fmt, end_date=end_fmt)

    if df is None or df.empty:
        raise ValueError(f"Tushare 未返回数据: {ts_code} [{start} ~ {end}]")

    # 字段映射与单位转换
    df = df.rename(columns={"vol": "volume"})
    df["symbol"] = symbol
    df["date"] = pd.to_datetime(df["trade_date"], format="%Y%m%d").dt.strftime("%Y-%m-%d")
    df["volume"] = df["volume"] * 100      # 手 → 股
    df["amount"] = df["amount"] * 1000     # 千元 → 元

    df = df.sort_values("date")
    df = df[["symbol", "date", "open", "high", "low", "close", "volume", "amount"]]
    df = df.reset_index(drop=True)
    return df


def _download_efinance(symbol: str, start: str, end: str) -> pd.DataFrame:
    """通过 efinance（腾讯接口）下载"""
    import efinance as ef

    df = ef.stock.get_quote_history(symbol, beg=start.replace("-", ""), end=end.replace("-", ""), klt=101, fqt=1)
    rename = {"股票名称": "name", "股票代码": "code", "日期": "date", "开盘": "open",
              "收盘": "close", "最高": "high", "最低": "low", "成交量": "volume", "成交额": "amount"}
    df = df.rename(columns={k: v for k, v in rename.items() if k in df.columns})
    df["symbol"] = symbol
    df = df[["symbol", "date", "open", "high", "low", "close", "volume", "amount"]]
    return df


def _download_akshare(symbol: str, start: str, end: str) -> pd.DataFrame:
    """通过 AKShare（东方财富接口）下载"""
    import akshare as ak

    df = ak.stock_zh_a_hist(
        symbol=symbol, period="daily",
        start_date=start.replace("-", ""), end_date=end.replace("-", ""),
        adjust="qfq",
    )
    rename = {"日期": "date", "开盘": "open", "收盘": "close", "最高": "high",
              "最低": "low", "成交量": "volume", "成交额": "amount"}
    df = df.rename(columns={k: v for k, v in rename.items() if k in df.columns})
    df["symbol"] = symbol
    df = df[["symbol", "date", "open", "high", "low", "close", "volume", "amount"]]
    return df


_SOURCE_ORDER = {
    "tushare": _download_tushare,
    "efinance": _download_efinance,
    "akshare": _download_akshare,
}


class DataManager:
    """行情数据管理器"""

    def download(self, symbol: str, start: str, end: str) -> pd.DataFrame:
        """下载日线数据，优先使用配置的数据源，失败自动切换"""
        config = _load_config()
        primary = config.get("data", {}).get("source", "tushare")

        # 构建尝试顺序：配置的源优先
        order = [primary] + [s for s in _SOURCE_ORDER if s != primary]
        downloaders = [(name, _SOURCE_ORDER[name]) for name in order]

        errors = []
        for name, downloader in downloaders:
            for attempt in range(2):
                try:
                    print(f"尝试 {name} 下载 {symbol}...")
                    df = downloader(symbol, start, end)
                    break
                except Exception as e:
                    errors.append(f"{name}: {e}")
                    if attempt < 1:
                        sleep(2)
            else:
                continue
            break
        else:
            raise RuntimeError("所有数据源均失败:\n" + "\n".join(f"  - {e}" for e in errors))

        _ensure_db()
        conn = sqlite3.connect(DB_PATH)
        conn.execute("DELETE FROM daily WHERE symbol = ? AND date BETWEEN ? AND ?", (symbol, start, end))
        df.to_sql("daily", conn, if_exists="append", index=False)
        conn.close()

        print(f"已下载 {len(df)} 条数据: {symbol} [{start} ~ {end}]")
        return df

    def load(self, symbol: str, start: str, end: str) -> pd.DataFrame:
        """从 SQLite 读取日线数据"""
        _ensure_db()
        conn = sqlite3.connect(DB_PATH)
        df = pd.read_sql(
            "SELECT * FROM daily WHERE symbol = ? AND date BETWEEN ? AND ? ORDER BY date",
            conn, params=(symbol, start, end),
        )
        conn.close()
        if not df.empty:
            df["date"] = pd.to_datetime(df["date"])
            df = df.set_index("date")
        return df

    def update_all(self) -> None:
        """增量更新所有已存储的股票数据（待实现）"""
        pass

    def fetch_stock_list(self) -> list[dict]:
        """通过 akshare/efinance 免费接口获取全部 A 股代码+名称，存入 stock_basic 表"""
        stocks = []
        for name, fn in [("_akshare", self._fetch_stock_list_akshare),
                         ("_efinance", self._fetch_stock_list_efinance)]:
            try:
                stocks = fn()
                break
            except Exception:
                continue
        if not stocks:
            return []

        _ensure_db()
        conn = sqlite3.connect(DB_PATH)
        conn.execute("DELETE FROM stock_basic")
        conn.executemany("INSERT INTO stock_basic (symbol, name) VALUES (?, ?)", stocks)
        conn.commit()
        conn.close()
        return [{"symbol": s[0], "name": s[1]} for s in stocks]

    def get_stock_list(self) -> list[dict]:
        """从 SQLite 读取股票列表，为空时自动拉取"""
        _ensure_db()
        conn = sqlite3.connect(DB_PATH)
        rows = conn.execute("SELECT symbol, name FROM stock_basic ORDER BY symbol").fetchall()
        conn.close()
        if not rows:
            return self.fetch_stock_list()
        return [{"symbol": r[0], "name": r[1]} for r in rows]

    def check_and_update(self) -> str:
        """检查最新交易日数据是否完整，缺失则自动补齐。返回 JSON 状态信息"""
        import tushare as ts
        import json

        _ensure_db()
        conn = sqlite3.connect(DB_PATH)

        # 查询已有数据的最新日期
        row = conn.execute("SELECT MAX(date) FROM daily").fetchone()
        latest_date = row[0] if row and row[0] else None

        # 获取 000001 最近交易日
        try:
            token = _get_tushare_token()
            pro = ts.pro_api(token)
            ref_df = pro.daily(ts_code="000001.SZ", start_date="20260101",
                               end_date=datetime.now().strftime("%Y%m%d"))
            if ref_df is None or ref_df.empty:
                conn.close()
                return json.dumps({"status": "no_data", "message": "无法获取最新交易日"}, ensure_ascii=False)
            last_trade = sorted(ref_df["trade_date"].tolist())[-1]
        except Exception as e:
            conn.close()
            return json.dumps({"status": "error", "message": str(e)}, ensure_ascii=False)

        last_trade_fmt = f"{last_trade[:4]}-{last_trade[4:6]}-{last_trade[6:]}"

        if latest_date and latest_date >= last_trade_fmt:
            conn.close()
            return json.dumps({"status": "up_to_date", "latest": latest_date}, ensure_ascii=False)

        # 需要更新：下载缺失的交易日
        conn.close()
        self.download_all_history(start=latest_date or "2009-01-01")
        return json.dumps({"status": "updated", "latest": last_trade_fmt}, ensure_ascii=False)

    @staticmethod
    def _fetch_stock_list_akshare() -> list[tuple[str, str]]:
        import akshare as ak
        df = ak.stock_zh_a_spot_em()
        return [(str(row["代码"]), str(row["名称"])) for _, row in df.iterrows()]

    @staticmethod
    def _fetch_stock_list_efinance() -> list[tuple[str, str]]:
        import efinance as ef
        df = ef.stock.get_realtime_quotes()
        return [(str(row["股票代码"]), str(row["股票名称"])) for _, row in df.iterrows()]

    def download_all_history(self, start: str = "2009-01-01") -> None:
        """下载全市场 A 股日线历史数据（按交易日遍历，支持断点续传）

        策略：用 trade_date 按日拉取全市场，比按股票循环高效得多
        （~5000 股 vs ~220 交易日/年）。120 积分限频 50次/分钟。
        """
        import tushare as ts

        token = _get_tushare_token()
        pro = ts.pro_api(token)

        # 获取交易日历：通过查询 000001（1991 年上市）全历史来推导交易日
        end = datetime.now().strftime("%Y%m%d")
        start_fmt = start.replace("-", "")
        print("正在获取交易日历...", flush=True)
        ref_df = pro.daily(ts_code="000001.SZ", start_date=start_fmt, end_date=end)
        if ref_df is None or ref_df.empty:
            print("无法获取交易日历")
            return
        trading_dates = sorted(ref_df["trade_date"].unique().tolist())
        total = len(trading_dates)
        print(f"交易日历: {total} 个交易日 [{start} ~ 今日]", flush=True)

        # 查询已完整下载的日期（单日 >= 1000 条说明是全市场下载的）
        _ensure_db()
        conn = sqlite3.connect(DB_PATH)
        conn.execute("PRAGMA journal_mode=WAL")
        conn.execute("PRAGMA synchronous=NORMAL")
        done_dates = set()
        for row in conn.execute(
            "SELECT date, COUNT(*) as cnt FROM daily GROUP BY date HAVING cnt >= 1000"
        ):
            done_dates.add(row[0].replace("-", ""))

        dates_to_download = [d for d in trading_dates if d not in done_dates]

        if not dates_to_download:
            conn.close()
            print("所有数据已是最新，无需下载", flush=True)
            return

        print(f"需下载 {len(dates_to_download)} 个交易日（已完成 {total - len(dates_to_download)} 个）", flush=True)
        print(f"预计耗时 ~{len(dates_to_download) * 1.2 / 60:.0f} 分钟（50次/分钟限频）", flush=True)

        total_rows = 0
        failed_dates = []

        for i, trade_date in enumerate(dates_to_download):
            df = None
            for attempt in range(3):
                try:
                    df = pro.daily(trade_date=trade_date)
                    break
                except Exception as e:
                    if attempt < 2:
                        sleep(2)
                    else:
                        failed_dates.append(trade_date)
                        print(f"  跳过 {trade_date}: {e}", flush=True)

            if df is None or df.empty:
                continue

            # 字段转换
            df["symbol"] = df["ts_code"].str.split(".").str[0]
            df["date"] = f"{trade_date[:4]}-{trade_date[4:6]}-{trade_date[6:]}"
            df["volume"] = df["vol"] * 100       # 手 → 股
            df["amount"] = df["amount"] * 1000    # 千元 → 元
            df = df[["symbol", "date", "open", "high", "low", "close", "volume", "amount"]]

            conn.execute("DELETE FROM daily WHERE date = ?", (df["date"].iloc[0],))
            df.to_sql("daily", conn, if_exists="append", index=False)
            conn.commit()

            total_rows += len(df)

            if (i + 1) % 10 == 0 or i == len(dates_to_download) - 1:
                pct = (i + 1) / len(dates_to_download) * 100
                print(f"  [{pct:5.1f}%] {i+1}/{len(dates_to_download)} | "
                      f"{trade_date} {len(df)} 只 | 累计 {total_rows:,} 条", flush=True)

            # 频率控制: 120 积分限 50 次/分钟
            sleep(1.2)

        conn.close()

        if failed_dates:
            print(f"\n{len(failed_dates)} 个交易日下载失败: {failed_dates[:10]}{'...' if len(failed_dates) > 10 else ''}", flush=True)
        print(f"下载完成，共写入 {total_rows:,} 条数据", flush=True)
