# Bunny demo on the coco Forth platform

## Context
A new standalone demo in ~/github/bunny (empty today, not a git repo): an LPC-style bunny on a
4-color CoCo 1/2 screen, white background, white rabbit, animating through idle, nose wiggle,
hop, eat a carrot, and sleep, with sound effects that play asynchronously while it animates.
It is built with the Forth toolchain in ~/github/coco (6809 ITC kernel, `tools/fc.py`
cross-compiler, `lib/` words, XRoar for running).

## Decisions
- Location: ~/github/bunny, with `COCO ?= $(HOME)/github/coco` for kernel, fc.py and lib.
- Mode: CG3, 128x96, 4 colors, CSS=1. SAM V=4, `$FF22` = `$C8` (matches
  `coco/src/vdg-modes/vdg-modes.fs:78`; `coco/coco-guides/vdg-modes.md:92` lists V=011, which
  looks wrong, so this gets confirmed in XRoar in milestone 1).
- Bunny drawn at 2x (~52x56). Frames stored at 1x and doubled by the blitter.
- Palette: buff 00 = background + body + light grey; cyan 01 = outline + mid grey; magenta 10 =
  inner ear + browns; orange 11 = nose, eye glint, carrot; carrot top cyan. The 8 side-view frames
  were already test-rendered in this mapping and read well.
- ROM-mode kernel (32K). Estimated ~3.2K sprite data + ~4K code against a ~10K app budget.

## Source art (bunnysheet5.png, 321x327, not a uniform grid)
- Download from https://opengameart.org/sites/default/files/bunnysheet5.png into `assets/`.
- License CC-BY 3.0 / CC-BY-SA 3.0 / OGA-BY 3.0. CREDITS.md must carry: "PixelFarm
  (https://bitbucket.org/tebruno99/pixelfarm) Stephen 'Redshrike' Challener". Derived frames are
  released CC-BY-SA 3.0.
- Side-view boxes (x,y,w,h): hop cycle (28,218,25,27) (59,216,26,28) (91,217,33,28)
  (134,218,29,25); sit to munch (179,218,26,25) (210,221,27,22) (243,224,28,19) (275,224,28,19).
  Row bottoms share a baseline near y=245.
- Sleep, nose wiggle, yawn, carrot and "z" are not in the sheet and are hand-authored as small PNGs
  derived from those frames (wiggle and closed eyes as a few-pixel patch on a base frame).

## Layout
```
bunny/
  Makefile  bunny.fs  README.md  CREDITS.md  issues.jsonl
  tools/png2cg.py  tools/frames.json      crop boxes, anchor, color map, sequences
  assets/bunnysheet5.png  assets/extra/   carrot0-3, sleep, yawn, wiggle, z
  build/                                  sprites.fs, coco-libs.fs, preview.png (generated)
```

## Build (Makefile)
`coco/make/demo.mk` hardcodes `../../`, so mirror it rather than include it:
- `build/coco-libs.fs` is generated as `INCLUDE $(COCO)/lib/<x>.fs` lines (vdg, wavetable,
  async-sound, keyboard, rng, bye). INCLUDE resolves relative to the including file
  (`coco/tools/fc.py:255`) and absolute paths work, so this keeps `COCO` overridable.
- `build/sprites.fs` is produced by `tools/png2cg.py` and holds `DATA[PY name bytes.fromhex("...")
  ]DATA` blocks plus frame tables. Embedding literals avoids depending on fc.py's cwd for `exec`
  (`fc.py:366`).
- Targets: `all` (bunny.bin), `run` (xroar -machine coco2bus -ram 32 with bas12/extbas11 ROMs
  and `-kbd-translate`, same as demo.mk), `cycles` (`fc.py --cycles`), `preview`, `clean`.
  The kernel is built via `$(MAKE) -C $(COCO)/kernel` when its map is missing.

## Converter (tools/png2cg.py, PIL 11.3 is installed)
1. Crop each box; align on a shared anchor (cell centre x, sheet baseline y) and store `ox oy` so
   hop frames keep their lift without jitter.
2. Map RGB to 2-bit with an exact table from frames.json plus a nearest-color fallback.
3. Pad width to a multiple of 4, pack 2bpp MSB-first. Record = `w h ox oy` + `ceil(w/4)*h` bytes.
4. Emit patch records (`row col len bytes`) for wiggle and eye frames against a base frame.
5. Write `build/preview.png`: every frame at 4x in CG3 CSS=1 colors on buff, grouped by
   sequence, anchor marked. This is the asset check before touching the emulator.

## Rendering
- `CODE blit2x ( spr x y flags -- )`: opaque blit (kernel `spr-draw` treats 00 as transparent,
  and 00 is the body color, so it can't be used). Each source byte expands through a 256-entry
  16-bit doubling table and is written to two VRAM rows; flags bit 0 mirrors via a reverse-byte
  table so the return hop needs no extra art. Inside the byte loop it checks `$FF01` and runs an
  inline copy of the `snd-poll` sample code (`coco/lib/async-sound.fs:104`, which ends in `;NEXT`
  and cannot be called), so sound keeps running during blits.
- Single buffer, drawn right after VSYNC (70 blank lines before row 0, `kernel.asm` WAIT-PAST-ROW),
  with the scene placed low enough (ground ~row 88) that the blit finishes above the beam. Trails
  are cleared with `spr-erase-box` (clears to 00 = buff). Measure with `make cycles`; if it tears,
  fall back to a page flip using the second CG3 page already inside the reserved `$0600-$1DFF`
  (pattern: `coco/src/clock/clock.fs` `set-sam-f-fast` :74, flip-state :136).
- Main loop: `BEGIN fill-to-vsync draw-dirty snd-frame anim-tick key? UNTIL exit-basic`.
  `fill-to-vsync` is a CODE variant of `snd-fill` (`async-sound.fs:168`) that emits one sample per
  HSYNC until the `$FF03` VSYNC flag, giving ~262 samples per frame so `freq>inc` pitch is right,
  with a noise twin modeled on `snd-noise-fill` (:244).

## Animation engine
- Frame table entries (5 B): sprite, dx, dy, duration, sfx id; a terminator selects the next state.
- State loop: idle (RND 60-180 frames) -> nose wiggle x2 -> hop right (frames 1-4, dx per hop)
  -> carrot appears -> munch (frames 5-8) while the carrot steps through 3/2/1/0 bite states ->
  yawn -> sleep with "z" rising in 3 positions -> wake -> hop left (mirrored) -> repeat.
- Carrot and "z" are separate small blits, redrawn only when they change.

## Sound (async-sound, triggered by frame sfx ids)
- Boing on takeoff: sine `snd-note` with an upward `snd-slide!`, reversed at the apex frame.
- Sniff during wiggle: short noise bursts with rests.
- Crunch per bite: ~5 frames of noise with a decaying amp (pattern in
  `coco/src/sound-async/sound-async.fs` main loop).
- Snore: `3 snd-shape`, low note, `snd-env!` swell then fade, repeating in step with the "z".
- Wake chirp: short sine with a rising slide.

## Milestones (each becomes an issue in issues.jsonl, closed at ~80% with leftovers filed)
1. Scaffold: git init, Makefile, coco-libs shim, CG3 buff screen shown in XRoar, CREDITS, README.
2. png2cg.py + frames.json + preview.png for the 8 sheet frames.
3. blit2x (opaque, 2x, mirror, inline sampling): one static bunny on screen.
4. fill-to-vsync main loop + hop cycle across the screen with trail erase, no tearing.
5. Hand-authored extras: carrot0-3, sleep, yawn, wiggle patch, z.
6. Full animation state machine.
7. Sound events wired into the frame tables.
8. Polish: RND idle times, BREAK exits, README screenshot.
Upstream coco issues to file: the CG3 SAM V row in `vdg-modes.md`, and adding `fill-to-vsync`
to `lib/async-sound.fs`.

## Verification
- Assets: inspect `build/preview.png` after each converter or art change.
- Runtime: `pkill -9 xroar; make run`, then screenshot the XRoar window with the screen-capture
  MCP (several captures to spot tearing). Check the palette matches the preview.
- Budget: `make cycles` for blit2x and anim-tick; confirm the binary stays under the ROM-mode app
  limit (fc.py reports sizes).
- Sound: listen in XRoar for pitch stability while the bunny hops (compare a held 440 Hz note
  idle vs during blits) and for dropouts.
