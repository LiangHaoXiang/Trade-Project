import argparse
import base64
import os
import sys
from pathlib import Path
from datetime import datetime

from openai import OpenAI

from config.secrets import get_secret

API_KEY = get_secret("DASHSCOPE_API_KEY")
BASE_URL = "https://dashscope.aliyuncs.com/compatible-mode/v1"
MODEL = "qwen3.5-flash"

REVIEW_PROMPT = """你是WPF应用的UI视觉审核专家。请对以下应用截图进行详细的视觉质量审查。

你必须从以下六个维度逐一审查：

1. **布局**：控件对齐、间距均匀性、拉伸适配性、元素分布合理性。
2. **文本**：文本截断/重叠可能性、字体一致性、行高、对比度、可读性。
3. **色彩与主题**：配色协调性、深色/浅色模式一致性、数据强调色的使用。
4. **视觉层次**：信息优先级（标题、数据、辅助文字）、同类元素视觉权重是否一致。
5. **图标与装饰**：图标风格统一性、装饰元素是否恰当。
6. **异常状态**：空数据、错误提示等非理想状态的视觉表现。

## ⚠️ 项目 UI 规范（必须作为审查标准）

以下是本项目的 UI 规范，审查时必须逐条检查是否违反：

### 全中文显示
- 所有面向用户的文本必须使用中文。不允许出现英文文本（如 BUY、SELL、filled、rejected等）。
- 方向列必须显示"买入"/"卖出"，状态列必须显示"已成交"/"已拒绝"。
- 筛选下拉框选项也必须使用中文（如"全部"/"买入"/"卖出"）。

### DataGrid 列对齐
- 列标题文字和数据行文字必须在同一垂直线上对齐。这是最基本的要求。
- 每列的标题对齐方式必须与数据对齐方式一致。
- 数值列（价格、数量、金额、盈亏等）必须右对齐。
- 代码列、时间列、方向列、状态列居中对齐。
- 原因列左对齐。原因列的标题也必须左对齐。

### 颜色规范（A股惯例）
- 买入/正收益/涨 → 红色
- 卖出/负收益/跌 → 绿色
- 零值/中性 → 黄色或中性色，不能偏红或偏绿

### 布局一致性
- 侧边栏/面板宽度在不同状态下（如有无进度条）必须保持一致，不能因内容变化而抖动。
- 固定宽度的面板不能使用 Auto 宽度。

### 空状态
- 有数据时不得显示"暂无数据"类提示。
- 无数据时提示文本必须居中显示。

请严格按以下格式输出：

## 审查结果

### 审查范围
（本次审查涉及的页面/控件）

### 审查方式
视觉模型分析（{model}）

### 🔴 严重问题（必须修复）
（每个问题包含：问题描述 → 截图中位置说明 → 违反了哪条UI规范 → 修改建议）

### 🟠 一般问题（建议修复）
（每个问题包含：问题描述 → 截图中位置说明 → 修改建议）

### 🟡 优化建议
（锦上添花的改进建议）

### 🟢 良好之处
（值得肯定的设计点）

### 📊 整体美观度评分
（1-5分，并给出简要总结）"""


def encode_image(image_path: str) -> str:
    with open(image_path, "rb") as f:
        return base64.b64encode(f.read()).decode("utf-8")


def get_image_mime(image_path: str) -> str:
    ext = Path(image_path).suffix.lower()
    mime_map = {".png": "image/png", ".jpg": "image/jpeg", ".jpeg": "image/jpeg", ".bmp": "image/bmp"}
    return mime_map.get(ext, "image/png")


def review_single(image_path: str, page_name: str = "", extra_prompt: str = "") -> str:
    print(f"正在审查: {image_path} ...")
    client = OpenAI(api_key=API_KEY, base_url=BASE_URL)

    b64 = encode_image(image_path)
    mime = get_image_mime(image_path)

    prompt = REVIEW_PROMPT.replace("{model}", MODEL)
    page_info = f"\n\n当前截图对应的页面是: {page_name}" if page_name else ""
    extra_info = f"\n\n补充审查要求: {extra_prompt}" if extra_prompt else ""

    content = [
        {"type": "text", "text": prompt + page_info + extra_info},
        {"type": "image_url", "image_url": {"url": f"data:{mime};base64,{b64}"}},
    ]

    completion = client.chat.completions.create(
        model=MODEL,
        messages=[{"role": "user", "content": content}],
    )

    return completion.choices[0].message.content


def review_batch(image_dir: str, output_path: str = "") -> str:
    image_dir = Path(image_dir)
    if not image_dir.is_dir():
        print(f"错误: 目录不存在 - {image_dir}")
        sys.exit(1)

    exts = {".png", ".jpg", ".jpeg", ".bmp"}
    images = sorted([f for f in image_dir.iterdir() if f.suffix.lower() in exts])

    if not images:
        print(f"错误: 目录中没有找到图片文件 - {image_dir}")
        sys.exit(1)

    print(f"找到 {len(images)} 张截图，开始批量审查...\n")

    results = []
    for img in images:
        page_name = img.stem
        result = review_single(str(img), page_name)
        results.append({"page": page_name, "image": str(img), "review": result})
        print(f"  {page_name} 审查完成\n")

    report_lines = [
        "# UI 视觉审查报告",
        "",
        f"**审查时间**: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}",
        f"**审查模型**: {MODEL}",
        f"**审查页面数**: {len(images)}",
        f"**审查方式**: 视觉模型分析",
        "",
    ]

    for i, r in enumerate(results, 1):
        report_lines.append("---")
        report_lines.append(f"## {i}. {r['page']}")
        report_lines.append(f"**截图**: `{r['image']}`")
        report_lines.append("")
        report_lines.append(r["review"])
        report_lines.append("")

    report = "\n".join(report_lines)

    if output_path:
        Path(output_path).parent.mkdir(parents=True, exist_ok=True)
        Path(output_path).write_text(report, encoding="utf-8")
        print(f"\n报告已保存到: {output_path}")
    else:
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        default_path = image_dir / f"visual_review_report_{timestamp}.md"
        default_path.write_text(report, encoding="utf-8")
        print(f"\n报告已保存到: {default_path}")

    return report


def main():
    parser = argparse.ArgumentParser(description="TradeDashboard UI 视觉审查工具（ui-visual-review Agent 配套脚本）")
    sub = parser.add_subparsers(dest="command")

    p_single = sub.add_parser("review", help="审查单张截图")
    p_single.add_argument("image", help="截图文件路径")
    p_single.add_argument("--name", default="", help="页面名称")
    p_single.add_argument("--extra", default="", help="补充审查要求")

    p_batch = sub.add_parser("batch", help="批量审查目录下所有截图")
    p_batch.add_argument("dir", help="截图目录路径")
    p_batch.add_argument("--output", default="", help="报告输出路径")

    p_test = sub.add_parser("test", help="测试 API 连接")

    args = parser.parse_args()

    if args.command == "review":
        if not Path(args.image).exists():
            print(f"错误: 文件不存在 - {args.image}")
            sys.exit(1)
        result = review_single(args.image, args.name, args.extra)
        print(result)

    elif args.command == "batch":
        review_batch(args.dir, args.output)

    elif args.command == "test":
        print(f"正在测试千问视觉大模型连接 ({MODEL})...")
        client = OpenAI(api_key=API_KEY, base_url=BASE_URL)
        completion = client.chat.completions.create(
            model=MODEL,
            messages=[{"role": "user", "content": "请回复'连接成功'四个字"}],
        )
        resp = completion.choices[0].message.content
        print(f"模型响应: {resp}")
        print(f"API 连接正常！模型: {MODEL}")

    else:
        parser.print_help()
        print("\n使用示例:")
        print("  python ui_visual_review.py test                                    # 测试连接")
        print("  python ui_visual_review.py review screenshot.png --name 总览       # 审查单张")
        print("  python ui_visual_review.py batch ./screenshots/                    # 批量审查")


if __name__ == "__main__":
    main()
