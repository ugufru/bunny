#!/usr/bin/env python3
"""png2cg.py - convert bunny sprite sheet frames to CoCo CG3 2bpp sprite data.

Usage: python3 tools/png2cg.py tools/frames.json build

Reads the crop boxes, color map and sequences from frames.json and writes:
  build/sprites.fs    DATA[PY blocks, one per frame, plus a spr lookup word
  build/preview.png   every frame at 4x in CG3 CSS=1 colors, one row per sequence

Sprite record layout (all values in 1x source pixels):
  byte 0  w   width, padded to a multiple of 4
  byte 1  h   height
  byte 2  ox  signed x offset of the left edge from the anchor (always even)
  byte 3  oy  signed y offset of the top edge from the anchor (baseline)
  then ceil(w/4)*h bytes, 2 bits per pixel, MSB first, row by row

The anchor is the centre of the frame's sheet cell on the shared baseline, so
frames of different sizes line up and the hop keeps its lift. ox is kept even
so that at 2x the sprite lands on a byte boundary when the anchor x is a
multiple of 4.
"""

import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

SCALE = 4          # preview scale
CELL = 40          # preview cell size in 1x pixels
CELL_BASE = 34     # baseline row inside a preview cell


def build_color_map(colors):
    table = {}
    for code, rgbs in colors.items():
        for rgb in rgbs:
            table[tuple(rgb)] = int(code)
    return table


def nearest(rgb, table):
    return min(table.items(),
               key=lambda kv: sum((a - b) ** 2 for a, b in zip(kv[0], rgb)))[1]


ART_CODES = {".": 0, "w": 0, "c": 1, "m": 2, "o": 3}


def frame_by_name(cfg, name):
    for f in cfg["frames"]:
        if f["name"] == name:
            return f
    sys.exit(f"no frame named {name}")


def sheet_offset(frame, cfg):
    """Top-left of a sheet frame relative to its anchor, in 1x pixels."""
    x, y = frame["box"][0], frame["box"][1]
    return x - (frame["cell_x"] + cfg["cell_w"] // 2), y - cfg["baseline_y"]


def read_art(path):
    rows = [line.rstrip("\n") for line in open(path)
            if line.strip() and not line.startswith("#")]
    w = max(len(r) for r in rows)
    try:
        return [[ART_CODES[ch] for ch in r.ljust(w, ".")] for r in rows]
    except KeyError as e:
        sys.exit(f"{path}: unknown pixel character {e}")


def trim_columns(rows, ox):
    """Drop empty (code 0) columns at both ends; move ox by the left trim."""
    used = [x for x in range(len(rows[0])) if any(r[x] for r in rows)]
    if not used:
        return rows, ox
    left, right = used[0], used[-1] + 1
    return [r[left:right] for r in rows], ox + left


def source_pixels(sheet, frame, cfg, table, unmapped):
    """Rows of 2-bit codes plus the unpadded ox, oy for a sheet or art frame."""
    if "art" in frame:
        rows = read_art(frame["art"])
        if "like" in frame:
            ox, oy = sheet_offset(frame_by_name(cfg, frame["like"]), cfg)
        else:
            ox, oy = frame["ox"], frame["oy"]
        if frame.get("trim"):
            rows, ox = trim_columns(rows, ox)
        return rows, ox, oy
    x, y, w, h = frame["box"]
    rows = []
    for yy in range(h):
        row = []
        for xx in range(w):
            r, g, b, a = sheet.getpixel((x + xx, y + yy))
            code = 0
            if a:
                code = table.get((r, g, b))
                if code is None:
                    unmapped.add((r, g, b))
                    code = nearest((r, g, b), table)
            row.append(code)
        rows.append(row)
    ox, oy = sheet_offset(frame, cfg)
    return rows, ox, oy


def convert_frame(sheet, frame, cfg, table, unmapped):
    src, ox, oy = source_pixels(sheet, frame, cfg, table, unmapped)
    h, w = len(src), len(src[0])
    lead = 0
    if ox % 2:
        ox -= 1
        lead = 1
    pw = w + lead
    pw += (-pw) % 4

    pixels = [[0] * lead + row + [0] * (pw - w - lead) for row in src]

    data = bytearray([pw, h, ox & 0xFF, oy & 0xFF])
    for row in pixels:
        for i in range(0, pw, 4):
            c = row[i:i + 4]
            data.append((c[0] << 6) | (c[1] << 4) | (c[2] << 2) | c[3])
    return {"name": frame["name"], "w": pw, "h": h, "ox": ox, "oy": oy,
            "pixels": pixels, "data": bytes(data)}


def mirror_frame(base, name):
    """base flipped left to right; the anchor stays at the same screen x."""
    pixels = [row[::-1] for row in base["pixels"]]
    w, h = base["w"], base["h"]
    ox, oy = -(base["ox"] + w), base["oy"]
    data = bytearray([w, h, ox & 0xFF, oy & 0xFF]) + pack_rows(pixels)
    return {"name": name, "w": w, "h": h, "ox": ox, "oy": oy,
            "pixels": pixels, "data": bytes(data)}


def grass_rows(spec):
    """Rows of 2-bit codes, 128 wide: solid cyan base rows plus random blades."""
    import random
    rng = random.Random(spec.get("seed", 1))
    h, w = spec["rows"], 128
    base = spec.get("base", 2)
    rows = [[0] * w for _ in range(h - base)] + [[1] * w for _ in range(base)]
    x = 0
    while x < w:
        if rng.random() < spec.get("density", 0.5):
            top = rng.randint(1, h - base - 2)
            lean = rng.choice((-1, 0, 1))
            for y in range(top, h - base):
                xx = x + (lean if y < top + 2 else 0)
                if 0 <= xx < w:
                    rows[y][xx] = 1
        x += rng.choice((1, 2, 2, 3))
    return rows


def pack_rows(rows):
    data = bytearray()
    for row in rows:
        for i in range(0, len(row), 4):
            c = row[i:i + 4]
            data.append((c[0] << 6) | (c[1] << 4) | (c[2] << 2) | c[3])
    return bytes(data)


def double_table():
    table = bytearray()
    for b in range(256):
        p = [(b >> s) & 3 for s in (6, 4, 2, 0)]
        table.append((p[0] << 6) | (p[0] << 4) | (p[1] << 2) | p[1])
        table.append((p[2] << 6) | (p[2] << 4) | (p[3] << 2) | p[3])
    return bytes(table)


def write_forth(path, frames, sequences, anims, grass):
    out = [
        "\\ sprites.fs - generated by tools/png2cg.py from tools/frames.json.",
        "\\ Do not edit. Record: w h ox oy (signed, 1x px from the anchor),",
        "\\ then ceil(w/4)*h bytes of 2bpp pixels, MSB first.",
        "",
    ]
    for f in frames:
        out += [f"DATA[PY spr-{f['name']}",
                f"bytes.fromhex(\"{f['data'].hex()}\")",
                "]DATA", ""]
    out += ["\\ dbl-table - 256 x 16-bit big-endian: each 2bpp byte with every",
            "\\ pixel doubled, for the 2x blitter.",
            "DATA[PY dbl-table",
            f"bytes.fromhex(\"{double_table().hex()}\")",
            "]DATA", ""]
    if grass:
        out += ["\\ grass - bottom screen rows of grass, 32 bytes per row.",
                "DATA[PY grass",
                f"bytes.fromhex(\"{pack_rows(grass_rows(grass)).hex()}\")",
                "]DATA", ""]
    names = [f["name"] for f in frames]
    out.append(f"{len(frames)} CONSTANT spr-count")
    for seq, members in sequences.items():
        out.append(f"{names.index(members[0])} CONSTANT seq-{seq}")
        out.append(f"{len(members)} CONSTANT seq-{seq}-len")
    def expand(name, seen=()):
        if name not in anims:
            sys.exit(f"anim {name} is not defined")
        if name in seen:
            sys.exit(f"anim {name} includes itself")
        flat = []
        for e in anims[name]:
            if isinstance(e, str):
                flat += expand(e.lstrip("@"), seen + (name,))
            else:
                flat.append(e)
        return flat

    for anim in anims:
        entries = expand(anim)
        blob = bytearray()
        for frame, dx, dy, hold, sfx in entries:
            if dx % 4:
                sys.exit(f"anim {anim}: dx {dx} is not a multiple of 4")
            blob += bytes([names.index(frame), dx & 0xFF, dy & 0xFF, hold, sfx])
        blob.append(255)
        out += ["", f"\\ anim-{anim} - entries of frame dx dy hold sfx, ended by 255.",
                f"DATA[PY anim-{anim}", f"bytes.fromhex(\"{blob.hex()}\")", "]DATA"]
    out += ["", "\\ spr - frame index (0..spr-count-1) to sprite record address.",
            ": spr  ( n -- addr )"]
    for i, n in enumerate(names[1:], 1):
        out.append(f"  DUP {i} = IF DROP spr-{n} EXIT THEN")
    out += [f"  DROP spr-{names[0]} ;", ""]
    path.write_text("\n".join(out))


def write_preview(path, frames, sequences, cfg):
    rgb = [tuple(c) for c in cfg["preview_rgb"]]
    by_name = {f["name"]: f for f in frames}
    cols = max(len(m) for m in sequences.values())
    # size cells from the frame extents so no frame is clipped
    left = max(-f["ox"] for f in frames) + 2
    cell_w = left + max(f["ox"] + f["w"] for f in frames) + 2
    img = Image.new("RGB", (cols * cell_w * SCALE, len(sequences) * CELL * SCALE), rgb[0])
    draw = ImageDraw.Draw(img)
    for row, members in enumerate(sequences.values()):
        for col, name in enumerate(members):
            f = by_name[name]
            cx = col * cell_w + left
            cy = row * CELL + CELL_BASE
            # baseline and anchor marks, outside the sprite palette on purpose
            draw.line([(col * cell_w * SCALE, cy * SCALE),
                       ((col + 1) * cell_w * SCALE - 1, cy * SCALE)], fill=(225, 210, 160))
            draw.rectangle([cx * SCALE - 1, cy * SCALE, cx * SCALE + 1, cy * SCALE + 6],
                           fill=(200, 40, 40))
            for yy, line in enumerate(f["pixels"]):
                for xx, code in enumerate(line):
                    if code:
                        px = (cx + f["ox"] + xx) * SCALE
                        py = (cy + f["oy"] + yy) * SCALE
                        draw.rectangle([px, py, px + SCALE - 1, py + SCALE - 1], fill=rgb[code])
    img.save(path)


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: png2cg.py frames.json outdir")
    cfg_path = Path(sys.argv[1])
    outdir = Path(sys.argv[2])
    cfg = json.loads(cfg_path.read_text())
    sheet = Image.open(cfg["sheet"]).convert("RGBA")
    table = build_color_map(cfg["colors"])
    unmapped = set()
    frames = []
    for f in cfg["frames"]:
        if "mirror" in f:
            base = next((c for c in frames if c["name"] == f["mirror"]), None)
            if base is None:
                sys.exit(f"mirror {f['name']}: {f['mirror']} must be listed earlier")
            frames.append(mirror_frame(base, f["name"]))
        else:
            frames.append(convert_frame(sheet, f, cfg, table, unmapped))

    outdir.mkdir(parents=True, exist_ok=True)
    write_forth(outdir / "sprites.fs", frames, cfg["sequences"], cfg.get("anims", {}), cfg.get("grass"))
    write_preview(outdir / "preview.png", frames, cfg["sequences"], cfg)

    total = 0
    for f in frames:
        total += len(f["data"])
        print(f"  {f['name']:8} {f['w']:2}x{f['h']:<2} ox={f['ox']:+3} oy={f['oy']:+3} {len(f['data'])} B")
    print(f"  {len(frames)} frames, {total} B")
    if unmapped:
        print(f"  warning: unmapped colors used nearest match: {sorted(unmapped)}")


if __name__ == "__main__":
    main()
