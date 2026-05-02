"""
新闻资讯聚合模块
多源获取财经热点新闻，支持东方财富、新浪财经等数据源
"""
import json
from datetime import datetime
from typing import Any

from monitor.logger import get_logger

logger = get_logger("news")


def fetch_news(max_items: int = 50) -> dict[str, Any]:
    """
    从多个数据源聚合财经新闻，按标题去重，按时间倒序排列

    Args:
        max_items: 最大返回条数

    Returns:
        dict: {"fetched_at": "...", "items": [...]}
    """
    all_items: list[dict] = []
    seen_titles: set[str] = set()

    sources = [
        ("东方财富", _fetch_from_em),
        ("新浪财经", _fetch_from_sina),
    ]

    for source_name, fetcher in sources:
        try:
            items = fetcher()
            for item in items:
                normalized_title = item["title"].strip()
                if normalized_title not in seen_titles:
                    seen_titles.add(normalized_title)
                    item["source"] = source_name
                    all_items.append(item)
        except Exception as e:
            logger.warning(f"获取{source_name}新闻失败: {e}")
            continue

    all_items.sort(key=lambda x: x.get("published_at", ""), reverse=True)
    all_items = all_items[:max_items]

    return {
        "fetched_at": datetime.now().strftime("%Y-%m-%dT%H:%M:%S"),
        "items": all_items,
    }


def _fetch_from_em() -> list[dict]:
    """从东方财富获取全球财经快讯"""
    import akshare as ak

    df = ak.stock_info_global_em()
    if df is None or df.empty:
        return []

    items = []
    for _, row in df.iterrows():
        title = str(row.get("标题", "")).strip()
        if not title:
            continue

        summary = str(row.get("摘要", "")).strip()
        published_at = str(row.get("发布时间", "")).strip()
        url = str(row.get("链接", "")).strip()

        item = {
            "title": title,
            "source": "东方财富",
            "url": url if url and url != "nan" else "",
            "published_at": _normalize_datetime(published_at),
            "summary": summary if summary and summary != "nan" else "",
        }
        items.append(item)

    return items


def _fetch_from_sina() -> list[dict]:
    """从新浪财经获取全球财经快讯"""
    import akshare as ak

    df = ak.stock_info_global_sina()
    if df is None or df.empty:
        return []

    items = []
    for _, row in df.iterrows():
        title = str(row.get("标题", row.get("title", ""))).strip()
        if not title or title == "nan":
            continue

        summary = str(row.get("摘要", row.get("content", row.get("摘要", "")))).strip()
        published_at = str(row.get("发布时间", row.get("pubtime", row.get("时间", "")))).strip()
        url = str(row.get("链接", row.get("url", ""))).strip()

        item = {
            "title": title,
            "source": "新浪财经",
            "url": url if url and url != "nan" else "",
            "published_at": _normalize_datetime(published_at),
            "summary": summary if summary and summary != "nan" else "",
        }
        items.append(item)

    return items


def _normalize_datetime(raw: str) -> str:
    """
    将各种日期时间格式统一为 ISO 8601 格式
    支持格式: "2025-05-01 09:15:00", "2025-05-01T09:15:00", "2025年05月01日 09:15" 等
    """
    if not raw or raw == "nan":
        return ""

    raw = raw.strip()

    for fmt in (
        "%Y-%m-%d %H:%M:%S",
        "%Y-%m-%dT%H:%M:%S",
        "%Y-%m-%d %H:%M",
        "%Y-%m-%dT%H:%M",
        "%Y年%m月%d日 %H:%M",
        "%Y年%m月%d日 %H:%M:%S",
        "%Y-%m-%d",
    ):
        try:
            dt = datetime.strptime(raw, fmt)
            return dt.strftime("%Y-%m-%dT%H:%M:%S")
        except ValueError:
            continue

    return raw
