import argparse
import ctypes
import os
import sys
import time
from datetime import datetime
from pathlib import Path

import ctypes.wintypes
import uiautomation as auto
from PIL import Image

user32 = ctypes.windll.user32
gdi32 = ctypes.windll.gdi32

user32.GetWindowDC.restype = ctypes.c_void_p
user32.GetWindowDC.argtypes = [ctypes.c_void_p]
gdi32.CreateCompatibleDC.restype = ctypes.c_void_p
gdi32.CreateCompatibleDC.argtypes = [ctypes.c_void_p]
gdi32.CreateCompatibleBitmap.restype = ctypes.c_void_p
gdi32.CreateCompatibleBitmap.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int]
gdi32.SelectObject.restype = ctypes.c_void_p
gdi32.SelectObject.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
gdi32.DeleteObject.argtypes = [ctypes.c_void_p]
gdi32.DeleteDC.argtypes = [ctypes.c_void_p]
user32.ReleaseDC.argtypes = [ctypes.c_void_p, ctypes.c_void_p]
user32.PrintWindow.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_uint]
user32.PrintWindow.restype = ctypes.c_bool
user32.SetForegroundWindow.argtypes = [ctypes.c_void_p]
user32.GetWindowRect.argtypes = [ctypes.c_void_p, ctypes.POINTER(ctypes.wintypes.RECT)]
gdi32.BitBlt.argtypes = [ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_int, ctypes.c_int,
                         ctypes.c_void_p, ctypes.c_int, ctypes.c_int, ctypes.c_ulong]
gdi32.GetDIBits.argtypes = [ctypes.c_void_p, ctypes.c_void_p, ctypes.c_uint, ctypes.c_uint,
                            ctypes.c_void_p, ctypes.c_void_p, ctypes.c_uint]

SCREENSHOT_DIR = Path(__file__).parent / "screenshots"
MAX_SCREENSHOTS = 1000

TAB_PAGES = [
    ("DashboardView", "总览"),
    ("MarketDataView", "行情"),
    ("TradingView", "交易"),
    ("TradeHistoryView", "交易记录"),
    ("EquityCurveView", "资金曲线"),
    ("BacktestView", "回测"),
    ("LogViewerView", "日志"),
    ("ConfigurationView", "配置"),
]

SRCCOPY = 0x00CC0020
CAPTUREBLT = 0x40000000
DIB_RGB_COLORS = 0
BI_RGB = 0


class BITMAPINFOHEADER(ctypes.Structure):
    _fields_ = [
        ("biSize", ctypes.wintypes.DWORD),
        ("biWidth", ctypes.wintypes.LONG),
        ("biHeight", ctypes.wintypes.LONG),
        ("biPlanes", ctypes.wintypes.WORD),
        ("biBitCount", ctypes.wintypes.WORD),
        ("biCompression", ctypes.wintypes.DWORD),
        ("biSizeImage", ctypes.wintypes.DWORD),
        ("biXPelsPerMeter", ctypes.wintypes.LONG),
        ("biYPelsPerMeter", ctypes.wintypes.LONG),
        ("biClrUsed", ctypes.wintypes.DWORD),
        ("biClrImportant", ctypes.wintypes.DWORD),
    ]


class BITMAPINFO(ctypes.Structure):
    _fields_ = [
        ("bmiHeader", BITMAPINFOHEADER),
        ("bmiColors", ctypes.wintypes.DWORD * 3),
    ]


def find_window_handle(title_keyword: str) -> int:
    result = ctypes.c_void_p(0)

    def enum_callback(hwnd, _):
        if not user32.IsWindowVisible(hwnd):
            return True
        length = user32.GetWindowTextLengthW(hwnd)
        if length == 0:
            return True
        buf = ctypes.create_unicode_buffer(length + 1)
        user32.GetWindowTextW(hwnd, buf, length + 1)
        if title_keyword.lower() in buf.value.lower():
            result.value = hwnd
            return False
        return True

    WNDENUMPROC = ctypes.WINFUNCTYPE(ctypes.c_bool, ctypes.wintypes.HWND, ctypes.wintypes.LPARAM)
    user32.EnumWindows(WNDENUMPROC(enum_callback), 0)
    return result.value or 0


def capture_window(hwnd: int) -> Image.Image | None:
    if not hwnd:
        return None

    user32.SetForegroundWindow(hwnd)
    time.sleep(0.3)

    rect = ctypes.wintypes.RECT()
    user32.GetWindowRect(hwnd, ctypes.byref(rect))
    width = rect.right - rect.left
    height = rect.bottom - rect.top

    if width <= 0 or height <= 0:
        return None

    hwnd_dc = user32.GetWindowDC(hwnd)
    mem_dc = gdi32.CreateCompatibleDC(hwnd_dc)
    bitmap = gdi32.CreateCompatibleBitmap(hwnd_dc, width, height)
    gdi32.SelectObject(mem_dc, bitmap)

    succeeded = user32.PrintWindow(hwnd, mem_dc, 3)
    if not succeeded:
        gdi32.BitBlt(mem_dc, 0, 0, width, height, hwnd_dc, 0, 0, SRCCOPY | CAPTUREBLT)

    bmi = BITMAPINFO()
    bmi.bmiHeader.biSize = ctypes.sizeof(BITMAPINFOHEADER)
    bmi.bmiHeader.biWidth = width
    bmi.bmiHeader.biHeight = -height
    bmi.bmiHeader.biPlanes = 1
    bmi.bmiHeader.biBitCount = 32
    bmi.bmiHeader.biCompression = BI_RGB

    buf_size = width * height * 4
    buf = ctypes.create_string_buffer(buf_size)
    gdi32.GetDIBits(mem_dc, bitmap, 0, height, buf, ctypes.byref(bmi), DIB_RGB_COLORS)

    img = Image.frombytes("RGBA", (width, height), buf, "raw", "BGRA", width * 4)

    gdi32.DeleteObject(bitmap)
    gdi32.DeleteDC(mem_dc)
    user32.ReleaseDC(hwnd, hwnd_dc)

    return img


def ensure_dir():
    SCREENSHOT_DIR.mkdir(parents=True, exist_ok=True)


def cleanup_old_screenshots():
    ensure_dir()
    files = sorted(SCREENSHOT_DIR.glob("*.png"), key=lambda f: f.stat().st_ctime)
    while len(files) >= MAX_SCREENSHOTS:
        oldest = files.pop(0)
        oldest.unlink(missing_ok=True)
        print(f"  清理旧截图: {oldest.name}")


def generate_filename(page_key: str, page_label: str) -> str:
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    return f"{timestamp}_{page_key}_{page_label}.png"


def capture_all_tabs():
    hwnd = find_window_handle("交易仪表盘")
    if not hwnd:
        print("错误: 未找到'交易仪表盘'窗口，请先启动应用。")
        sys.exit(1)

    print(f"找到窗口句柄: {hwnd}")

    main_window = auto.WindowControl(searchFromControl=auto.GetRootControl(),
                                     Name="交易仪表盘", searchDepth=1)
    if not main_window.Exists(3):
        print("错误: UIAutomation 未找到主窗口")
        sys.exit(1)

    tab_control = main_window.TabControl(searchDepth=5)
    if not tab_control.Exists(3):
        print("错误: 未找到 TabControl")
        sys.exit(1)

    tab_items = tab_control.GetChildren()
    print(f"找到 {len(tab_items)} 个 Tab 页签\n")

    ensure_dir()
    cleanup_old_screenshots()

    saved = []
    for i, (page_key, page_label) in enumerate(TAB_PAGES):
        if i >= len(tab_items):
            print(f"跳过: {page_label} (Tab 索引超出范围)")
            continue

        print(f"切换到: {page_label} ({page_key}) ...")
        try:
            tab_items[i].Click()
            time.sleep(1.5)
        except Exception as e:
            print(f"  点击失败: {e}，尝试 GetNativeWindowHandle + SetFocus")
            try:
                tab_items[i].SetFocus()
                time.sleep(1.5)
            except Exception as e2:
                print(f"  跳过: {page_label} ({e2})")
                continue

        img = capture_window(hwnd)
        if img:
            filename = generate_filename(page_key, page_label)
            filepath = SCREENSHOT_DIR / filename
            img.save(str(filepath), "PNG")
            print(f"  已保存: {filename}")
            saved.append((page_key, page_label, filepath))
        else:
            print(f"  跳过: {page_label} (截图失败)")

    print(f"\n截图完成！共保存 {len(saved)} 张截图到: {SCREENSHOT_DIR}")
    return saved


def capture_single(page_key: str, page_label: str):
    hwnd = find_window_handle("交易仪表盘")
    if not hwnd:
        print("错误: 未找到'交易仪表盘'窗口")
        sys.exit(1)

    ensure_dir()
    cleanup_old_screenshots()

    img = capture_window(hwnd)
    if img:
        filename = generate_filename(page_key, page_label)
        filepath = SCREENSHOT_DIR / filename
        img.save(str(filepath), "PNG")
        print(f"已保存: {filename}")
        return filepath
    else:
        print("截图失败")
        sys.exit(1)


def capture_and_review():
    saved = capture_all_tabs()
    if not saved:
        print("没有截图可用于审查")
        sys.exit(1)

    print("\n开始调用视觉模型审查...\n")
    sys.path.insert(0, str(Path(__file__).parent))
    from ui_visual_review import review_batch

    report = review_batch(str(SCREENSHOT_DIR))
    return report


def main():
    parser = argparse.ArgumentParser(description="TradeDashboard 自动截图工具（带 Tab 自动切换）")
    sub = parser.add_subparsers(dest="command")

    sub.add_parser("all", help="自动切换 Tab 页并逐一截图")
    sub.add_parser("capture_and_review", help="截图后立即调用视觉模型审查")

    p_single = sub.add_parser("single", help="对当前窗口截图")
    p_single.add_argument("--key", default="CustomPage", help="页面英文标识")
    p_single.add_argument("--label", default="自定义页面", help="页面中文名称")

    sub.add_parser("clean", help="清理旧截图（保留最新1000张）")
    sub.add_parser("status", help="查看截图目录状态")

    args = parser.parse_args()

    if args.command == "all":
        capture_all_tabs()

    elif args.command == "single":
        capture_single(args.key, args.label)

    elif args.command == "capture_and_review":
        capture_and_review()

    elif args.command == "clean":
        cleanup_old_screenshots()
        print("清理完成")

    elif args.command == "status":
        ensure_dir()
        files = list(SCREENSHOT_DIR.glob("*.png"))
        total_size = sum(f.stat().st_size for f in files) / (1024 * 1024)
        print(f"截图目录: {SCREENSHOT_DIR}")
        print(f"截图数量: {len(files)} / {MAX_SCREENSHOTS}")
        print(f"占用空间: {total_size:.1f} MB")
        if files:
            oldest = min(files, key=lambda f: f.stat().st_ctime)
            newest = max(files, key=lambda f: f.stat().st_ctime)
            print(f"最早截图: {oldest.name}")
            print(f"最新截图: {newest.name}")

    else:
        parser.print_help()
        print("\n使用示例:")
        print("  python ui_screenshot.py all                   # 自动切换Tab并截图")
        print("  python ui_screenshot.py capture_and_review     # 截图 + 视觉审查")
        print("  python ui_screenshot.py status                 # 查看目录状态")


if __name__ == "__main__":
    main()
