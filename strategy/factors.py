"""
通用因子库
提供常用技术指标计算，供策略模块调用
所有函数接受 pandas DataFrame（需包含 close/high/low/volume 列），返回 pandas Series
"""
import numpy as np
import pandas as pd


def ma(series: pd.Series, window: int) -> pd.Series:
    """简单移动平均线"""
    return series.rolling(window=window, min_periods=window).mean()


def ema(series: pd.Series, window: int) -> pd.Series:
    """指数移动平均线"""
    return series.ewm(span=window, adjust=False).mean()


def rsi(series: pd.Series, window: int = 14) -> pd.Series:
    """相对强弱指标 (RSI)

    参数 series: 收盘价序列
    参数 window: 回望窗口（默认 14）
    """
    delta = series.diff()
    gain = delta.clip(lower=0)
    loss = (-delta).clip(lower=0)

    avg_gain = gain.ewm(alpha=1 / window, min_periods=window, adjust=False).mean()
    avg_loss = loss.ewm(alpha=1 / window, min_periods=window, adjust=False).mean()

    rs = avg_gain / avg_loss.replace(0, np.nan)
    return 100 - 100 / (1 + rs)


def macd(
    series: pd.Series,
    fast: int = 12,
    slow: int = 26,
    signal: int = 9,
) -> tuple[pd.Series, pd.Series, pd.Series]:
    """MACD 指标

    返回 (macd_line, signal_line, histogram)
    """
    ema_fast = ema(series, fast)
    ema_slow = ema(series, slow)
    macd_line = ema_fast - ema_slow
    signal_line = ema(macd_line, signal)
    histogram = macd_line - signal_line
    return macd_line, signal_line, histogram


def bollinger(
    series: pd.Series,
    window: int = 20,
    num_std: float = 2.0,
) -> tuple[pd.Series, pd.Series, pd.Series]:
    """布林带

    返回 (upper, middle, lower)
    """
    middle = ma(series, window)
    std = series.rolling(window=window, min_periods=window).std()
    upper = middle + num_std * std
    lower = middle - num_std * std
    return upper, middle, lower


def kdj(
    high: pd.Series,
    low: pd.Series,
    close: pd.Series,
    n: int = 9,
    m1: int = 3,
    m2: int = 3,
) -> tuple[pd.Series, pd.Series, pd.Series]:
    """KDJ 随机指标

    返回 (K, D, J)
    """
    lowest_low = low.rolling(window=n, min_periods=n).min()
    highest_high = high.rolling(window=n, min_periods=n).max()

    rsv = (close - lowest_low) / (highest_high - lowest_low).replace(0, np.nan) * 100

    k = rsv.ewm(alpha=1 / m1, adjust=False).mean()
    d = k.ewm(alpha=1 / m2, adjust=False).mean()
    j = 3 * k - 2 * d

    return k, d, j


def atr(
    high: pd.Series,
    low: pd.Series,
    close: pd.Series,
    window: int = 14,
) -> pd.Series:
    """平均真实波幅 (ATR)"""
    prev_close = close.shift(1)
    tr1 = high - low
    tr2 = (high - prev_close).abs()
    tr3 = (low - prev_close).abs()
    true_range = pd.concat([tr1, tr2, tr3], axis=1).max(axis=1)
    return true_range.rolling(window=window, min_periods=window).mean()


def turnover_rate(volume: pd.Series, total_shares: float) -> pd.Series:
    """换手率

    参数 volume: 成交量序列（股）
    参数 total_shares: 总股本（股）
    """
    return volume / total_shares * 100


def volatility(series: pd.Series, window: int = 20) -> pd.Series:
    """历史波动率（年化）

    参数 series: 收盘价序列
    参数 window: 回望窗口
    """
    returns = series.pct_change()
    return returns.rolling(window=window, min_periods=window).std() * np.sqrt(242)


def williams_r(
    high: pd.Series,
    low: pd.Series,
    close: pd.Series,
    window: int = 14,
) -> pd.Series:
    """威廉指标 (Williams %R)"""
    highest = high.rolling(window=window, min_periods=window).max()
    lowest = low.rolling(window=window, min_periods=window).min()
    return (highest - close) / (highest - lowest).replace(0, np.nan) * -100


def obv(close: pd.Series, volume: pd.Series) -> pd.Series:
    """能量潮指标 (OBV)"""
    direction = close.diff().apply(np.sign)
    return (volume * direction).cumsum()


def cci(
    high: pd.Series,
    low: pd.Series,
    close: pd.Series,
    window: int = 14,
) -> pd.Series:
    """商品通道指标 (CCI)

    参数 high: 最高价序列
    参数 low: 最低价序列
    参数 close: 收盘价序列
    参数 window: 回望窗口（默认 14）
    """
    tp = (high + low + close) / 3
    ma_tp = tp.rolling(window=window, min_periods=window).mean()
    md = tp.rolling(window=window, min_periods=window).apply(
        lambda x: np.abs(x - x.mean()).mean(), raw=True
    )
    return (tp - ma_tp) / (0.015 * md.replace(0, np.nan))


def roc(series: pd.Series, window: int = 12) -> pd.Series:
    """变动率指标 (ROC)

    参数 series: 收盘价序列
    参数 window: 回望窗口（默认 12）
    """
    prev = series.shift(window)
    return (series - prev) / prev.replace(0, np.nan) * 100


def trix(series: pd.Series, window: int = 12) -> pd.Series:
    """三重指数平滑移动平均变化率 (TRIX)

    参数 series: 收盘价序列
    参数 window: 回望窗口（默认 12）
    """
    ema1 = ema(series, window)
    ema2 = ema(ema1, window)
    ema3 = ema(ema2, window)
    return (ema3 - ema3.shift(1)) / ema3.shift(1).replace(0, np.nan) * 100


def dmi(
    high: pd.Series,
    low: pd.Series,
    close: pd.Series,
    window: int = 14,
) -> tuple[pd.Series, pd.Series, pd.Series]:
    """趋向指标 (DMI)

    返回 (+DI, -DI, ADX)
    """
    prev_high = high.shift(1)
    prev_low = low.shift(1)
    prev_close = close.shift(1)

    plus_dm = ((high - prev_high) > (prev_low - low)).astype(float)
    plus_dm = plus_dm * (high - prev_high).clip(lower=0)
    minus_dm = ((prev_low - low) > (high - prev_high)).astype(float)
    minus_dm = minus_dm * (prev_low - low).clip(lower=0)

    atr_val = atr(high, low, close, window)

    plus_di = 100 * plus_dm.rolling(window=window, min_periods=window).sum() / atr_val.replace(0, np.nan)
    minus_di = 100 * minus_dm.rolling(window=window, min_periods=window).sum() / atr_val.replace(0, np.nan)

    dx = (plus_di - minus_di).abs() / (plus_di + minus_di).replace(0, np.nan) * 100
    adx = dx.rolling(window=window, min_periods=window).mean()

    return plus_di, minus_di, adx


def bias(series: pd.Series, window: int = 12) -> pd.Series:
    """乖离率 (BIAS)

    参数 series: 收盘价序列
    参数 window: 回望窗口（默认 12）
    """
    ma_val = ma(series, window)
    return (series - ma_val) / ma_val.replace(0, np.nan) * 100


def wad(high: pd.Series, low: pd.Series, close: pd.Series) -> pd.Series:
    """威廉累积分配指标 (WAD)"""
    prev_close = close.shift(1)
    trh = pd.concat([high, prev_close], axis=1).max(axis=1)
    trl = pd.concat([low, prev_close], axis=1).min(axis=1)

    ad = pd.Series(0.0, index=close.index)
    for i in range(1, len(close)):
        if close.iloc[i] > close.iloc[i - 1]:
            ad.iloc[i] = close.iloc[i] - trl.iloc[i]
        elif close.iloc[i] < close.iloc[i - 1]:
            ad.iloc[i] = close.iloc[i] - trh.iloc[i]
        else:
            ad.iloc[i] = 0.0

    return ad.cumsum()


def volume_ma(volume: pd.Series, window: int = 20) -> pd.Series:
    """成交量移动平均线"""
    return volume.rolling(window=window, min_periods=window).mean()


def add_all_factors(df: pd.DataFrame) -> pd.DataFrame:
    """为 DataFrame 添加所有常用因子列

    参数 df: 需包含 close, high, low, volume 列
    返回 添加了因子列的 DataFrame（不修改原始数据）
    """
    result = df.copy()

    if "close" in result.columns:
        result["ma5"] = ma(result["close"], 5)
        result["ma10"] = ma(result["close"], 10)
        result["ma20"] = ma(result["close"], 20)
        result["ma60"] = ma(result["close"], 60)
        result["ema12"] = ema(result["close"], 12)
        result["ema26"] = ema(result["close"], 26)
        result["rsi14"] = rsi(result["close"], 14)

        macd_line, signal_line, histogram = macd(result["close"])
        result["macd"] = macd_line
        result["macd_signal"] = signal_line
        result["macd_hist"] = histogram

        upper, middle, lower = bollinger(result["close"])
        result["boll_upper"] = upper
        result["boll_middle"] = middle
        result["boll_lower"] = lower

        result["volatility20"] = volatility(result["close"], 20)
        result["roc12"] = roc(result["close"], 12)
        result["trix12"] = trix(result["close"], 12)
        result["bias12"] = bias(result["close"], 12)

    if all(c in result.columns for c in ("high", "low", "close")):
        k, d, j = kdj(result["high"], result["low"], result["close"])
        result["kdj_k"] = k
        result["kdj_d"] = d
        result["kdj_j"] = j

        result["atr14"] = atr(result["high"], result["low"], result["close"], 14)
        result["williams_r"] = williams_r(result["high"], result["low"], result["close"])
        result["cci14"] = cci(result["high"], result["low"], result["close"], 14)
        result["wad"] = wad(result["high"], result["low"], result["close"])

        plus_di, minus_di, adx = dmi(result["high"], result["low"], result["close"], 14)
        result["plus_di14"] = plus_di
        result["minus_di14"] = minus_di
        result["adx14"] = adx

    if all(c in result.columns for c in ("close", "volume")):
        result["obv"] = obv(result["close"], result["volume"])
        result["volume_ma20"] = volume_ma(result["volume"], 20)

    return result
