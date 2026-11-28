#!/usr/bin/env python3
"""Generates the FinanceManager favicon assets (PNG + multi-size ICO).

Motif: rounded dark tile with a stylized "F" whose vertical bar reads as a
chart axis and whose two horizontal bars read as grid lines, crossed by an
ascending trend line in the Teal -> Violet accent gradient that ends in a
glowing node. Mirrors wwwroot/icons/finance_manager.svg (vector master).

Usage: python scripts/icons/generate_favicon.py
Outputs: FinanceManager.Web/wwwroot/icons/finance_manager.png (1024x1024)
         FinanceManager.Web/wwwroot/icons/finance_manager-64x64.ico (16/32/48/64)
"""

from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

REPO_ROOT = Path(__file__).resolve().parents[2]
OUT_DIR = REPO_ROOT / "FinanceManager.Web" / "wwwroot" / "icons"

CANVAS = 1024
BG_TOP = (11, 17, 32)      # #0b1120
BG_BOTTOM = (22, 32, 58)   # #16203a
F_COLOR = (100, 116, 139)  # #64748b
TREND_START = (45, 212, 191)   # #2dd4bf (--accent)
TREND_END = (139, 92, 246)     # #8b5cf6 (--accent-2)
NODE_COLOR = (196, 181, 253)   # #c4b5fd


def lerp(a, b, t):
    return tuple(int(round(a[i] + (b[i] - a[i]) * t)) for i in range(3))


def make_tile(size):
    """Rounded dark gradient tile as RGBA image."""
    grad = Image.new("RGB", (1, size))
    px = grad.load()
    for y in range(size):
        px[0, y] = lerp(BG_TOP, BG_BOTTOM, y / max(1, size - 1))
    grad = grad.resize((size, size))

    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    radius = int(size * 110 / 512)
    margin = int(size * 8 / 512)
    d.rounded_rectangle([margin, margin, size - margin, size - margin],
                        radius=radius, fill=255)

    tile = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    tile.paste(grad, (0, 0), mask)
    return tile


def draw_f(img, size):
    """Draws the stylized F (chart axis + grid lines)."""
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    s = size / 512.0
    r = 14 * s
    d.rounded_rectangle([140 * s, 120 * s, 192 * s, 392 * s], radius=r, fill=F_COLOR + (255,))
    d.rounded_rectangle([140 * s, 120 * s, 356 * s, 168 * s], radius=r, fill=F_COLOR + (255,))
    d.rounded_rectangle([140 * s, 232 * s, 308 * s, 276 * s], radius=r, fill=F_COLOR + (255,))
    img.alpha_composite(layer)


def draw_trend(img, size):
    """Draws the ascending gradient trend line and the glowing end node."""
    s = size / 512.0
    points = [(150 * s, 352 * s), (232 * s, 296 * s), (300 * s, 318 * s), (384 * s, 232 * s)]

    # path length for gradient interpolation along the line
    seglens = []
    total = 0.0
    for i in range(len(points) - 1):
        (x1, y1), (x2, y2) = points[i], points[i + 1]
        seg = ((x2 - x1) ** 2 + (y2 - y1) ** 2) ** 0.5
        seglens.append(seg)
        total += seg

    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    width = int(round(26 * s))
    dist = 0.0
    step = max(1.0, s)  # ~2px steps on the 1024 canvas
    for i in range(len(points) - 1):
        (x1, y1), (x2, y2) = points[i], points[i + 1]
        seg = seglens[i]
        t0 = 0.0
        while t0 < seg:
            t1 = min(seg, t0 + step)
            c0 = lerp(TREND_START, TREND_END, (dist + t0) / total)
            c1 = lerp(TREND_START, TREND_END, (dist + t1) / total)
            cm = lerp(c0, c1, 0.5)
            xa, ya = x1 + (x2 - x1) * (t0 / seg), y1 + (y2 - y1) * (t0 / seg)
            xb, yb = x1 + (x2 - x1) * (t1 / seg), y1 + (y2 - y1) * (t1 / seg)
            d.line([(xa, ya), (xb, yb)], fill=cm + (255,), width=width)
            d.ellipse([xa - width / 2, ya - width / 2, xa + width / 2, ya + width / 2],
                      fill=cm + (255,))
            t0 = t1
        dist += seg

    # round caps / joints
    for j, (x, y) in enumerate(points):
        c = lerp(TREND_START, TREND_END,
                 (sum(seglens[:j]) / total) if total else 0)
        d.ellipse([x - width / 2, y - width / 2, x + width / 2, y + width / 2],
                  fill=c + (255,))
    img.alpha_composite(layer)

    # glowing end node
    ex, ey = points[-1]
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gr = 52 * s
    gd.ellipse([ex - gr, ey - gr, ex + gr, ey + gr], fill=TREND_END + (216,))
    glow = glow.filter(ImageFilter.GaussianBlur(radius=26 * s))
    img.alpha_composite(glow)

    d2 = ImageDraw.Draw(img)
    nr = 20 * s
    d2.ellipse([ex - nr, ey - nr, ex + nr, ey + nr], fill=NODE_COLOR + (255,))


def main():
    OUT_DIR.mkdir(parents=True, exist_ok=True)

    master = make_tile(CANVAS)
    draw_f(master, CANVAS)
    draw_trend(master, CANVAS)

    png_path = OUT_DIR / "finance_manager.png"
    master.save(png_path, format="PNG")

    ico_base = master.resize((64, 64), Image.LANCZOS)
    ico_path = OUT_DIR / "finance_manager-64x64.ico"
    ico_base.save(ico_path, format="ICO", sizes=[(16, 16), (32, 32), (48, 48), (64, 64)])

    print(f"Wrote {png_path}")
    print(f"Wrote {ico_path}")


if __name__ == "__main__":
    main()
