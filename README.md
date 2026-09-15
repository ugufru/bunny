# bunny

A Color Computer 1/2 demo: a white LPC-style bunny on a white CG3 screen
(128x96, 4 colors) that idles, wiggles its nose, hops, eats a carrot and
falls asleep, with sound effects playing while it animates.

Written in Forth using the kernel and cross-compiler from the coco project.

## Requirements

- A checkout of the coco project at `~/github/coco` (or set `COCO=...`)
- `lwasm` (to build the kernel if it isn't built yet)
- Python 3 with Pillow (sprite converter)
- XRoar with `bas12.rom` and `extbas11.rom` in `~/.xroar/roms/`

## Build and run

```sh
make          # builds bunny.bin
make run      # launches XRoar (32K CoCo 2)
```

Press BREAK to return to BASIC.

## Files

- `bunny.fs`: the demo
- `PLAN.md`: design notes (mode, palette, rendering, sound, memory budget)
- `issues.jsonl`, `roadmap.jsonl`, `issues.html`: work tracking
- `CREDITS.md`: art attribution and licenses
