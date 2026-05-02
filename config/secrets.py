"""
统一敏感信息管理模块

所有 API Key、Token、账户密码等敏感信息统一通过环境变量获取。
推荐使用项目根目录的 .env 文件管理环境变量（已加入 .gitignore）。

使用方式：
    from config.secrets import get_secret

    token = get_secret("TUSHARE_TOKEN")
    api_key = get_secret("DASHSCOPE_API_KEY")
"""
import os
from pathlib import Path
from typing import Optional

try:
    from dotenv import load_dotenv
    _DOTENV_AVAILABLE = True
except ImportError:
    _DOTENV_AVAILABLE = False

_ENV_LOADED = False


def _ensure_env_loaded():
    global _ENV_LOADED
    if _ENV_LOADED:
        return
    if _DOTENV_AVAILABLE:
        env_path = Path(__file__).parent.parent / ".env"
        load_dotenv(env_path)
    _ENV_LOADED = True


def get_secret(key: str, default: Optional[str] = None, required: bool = False) -> Optional[str]:
    """
    获取敏感配置值

    查找顺序：
    1. 系统环境变量
    2. .env 文件（如果安装了 python-dotenv）

    Args:
        key: 环境变量名
        default: 默认值（当值不存在时返回）
        required: 是否为必填项，为 True 时值不存在会抛出 ValueError

    Returns:
        配置值字符串，或 None / default
    """
    _ensure_env_loaded()
    value = os.environ.get(key, "")
    if not value:
        value = default if default is not None else None
    if required and not value:
        raise ValueError(
            f"缺少必填环境变量: {key}\n"
            f"请在项目根目录的 .env 文件中设置，或通过系统环境变量传入。\n"
            f"可参考 .env.example 文件获取模板。"
        )
    return value
