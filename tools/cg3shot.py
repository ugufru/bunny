#!/usr/bin/env python3
"""cg3shot.py - render the CG3 page from an XRoar RAM dump as a PNG.

Usage: python3 tools/cg3shot.py build/shot.ram build/shot.png [base] [scale]

base defaults to $0600 (vram-base). The dump is a raw image of RAM from
address 0. Colors are CSS=1 (buff, cyan, magenta, orange) as XRoar renders
them, matching tools/frames.json preview_rgb. A 4 px border is drawn in
buff, as on the real screen.
"""

import sys

from PIL import Image

RGB = [(255, 255, 255), (88, 170, 120), (236, 102, 248), (238, 121, 74)]
W, H, BYTES_PER_ROW = 128, 96, 32


def main():
    if len(sys.argv) < 3:
        sys.exit("usage: cg3shot.py dump.ram out.png [base] [scale]")
    ram = open(sys.argv[1], "rb").read()
    base = int(sys.argv[3], 0) if len(sys.argv) > 3 else 0x0600
    scale = int(sys.argv[4]) if len(sys.argv) > 4 else 4
    page = ram[base:base + W * H // 4]
    if len(page) < W * H // 4:
        sys.exit(f"dump is only {len(ram)} bytes, no CG3 page at ${base:04X}")

    border = 4
    img = Image.new("RGB", (W + 2 * border, H + 2 * border), RGB[0])
    px = img.load()
    for y in range(H):
        row = page[y * BYTES_PER_ROW:(y + 1) * BYTES_PER_ROW]
        for i, b in enumerate(row):
            for p in range(4):
                px[border + i * 4 + p, border + y] = RGB[(b >> (6 - 2 * p)) & 3]
    img = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    img.save(sys.argv[2])
    print(f"wrote {sys.argv[2]} from ${base:04X}")


if __name__ == "__main__":
    main()
