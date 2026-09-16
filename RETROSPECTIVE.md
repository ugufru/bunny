# Retrospective: building the bunny demo in one session

Written from the console session that produced this repo, start to finish:
from "can you make a decent plan" to a public GitHub repo with an animated
README.

## What happened

The session ran in three phases.

**Planning.** I explored the coco Forth platform with a subagent, measured the
OpenGameArt sprite sheet directly (321x327, no uniform grid, 8 colors, side
view rows at y~216-245), and asked three questions that shaped everything
after: standalone repo vs a demo inside coco, which 4-color mode and scale,
and which color carries the outline. The answers (standalone, CG3 at 2x, cyan
outline) were settled before any code existed.

**Eleven issues, one at a time.** The plan became issues.jsonl and a ranked
roadmap. Each issue: set in-progress, build it, verify, show you the result,
and close only after you confirmed. Every close was a commit. The order put
the two unknowns early (does single-buffer drawing tear, does sound survive
drawing) rather than leaving them to the end.

**Release.** License, a pinned coco commit, a public repo, then a GIF from
your screen recording.

## What worked

**Data-driven from the start.** The whole show lives in tools/frames.json:
crop boxes, color mapping, animation entries, events, the grass spec. Retiming
the nose wiggle or reordering the show never needed a Forth change. When you
asked for the bunny to move toward the carrot while eating, that was three dx
numbers in a JSON list plus a converter flag.

**Verification that did not depend on a window.** The screen-capture tool
could not see XRoar (macOS Screen Recording permission, then XRoar sitting on
another Space behind a full-screen terminal). Instead of waiting on that, I
made `make shot`: XRoar runs headless, traps at the Nth vsync, dumps RAM, and
a Python script renders the CG3 page to a PNG. That turned out better than
window captures for everything except tearing: exact frames, repeatable, no
window needed. Most of the checks in this session came from it.

**Closing at about 80%.** Patch records for the art frames, mirroring inside
the assembly blitter, a lib proposal for fill-to-vsync: all dropped once the
cheaper path was good enough. The mirrored-frame decision (generate flipped
copies in the converter, 850 bytes, no asm change) is the clearest example.

**Checking the subagent.** The design agent returned a detailed plan with
file:line references and zero tool calls, meaning it had not opened anything.
I verified its claims before building on them. Most held; the one that
mattered (CG3 SAM bits) turned up a real documentation error in coco, filed
there as issue 572.

## What went wrong

**I over-engineered the sound.** Asked to make sound work during drawing, I
built a shared sample routine, a HSYNC-line counter, a RAM log with a magic
marker, and two tools to read it back, then started looking for a way to
record XRoar's audio to measure pitch drift. You stopped it: "just use the
async sound library for sound and be done." That was correct. The measurement
was real (idle frames caught ~100 of 262 scan lines, blit passes ran denser,
so pitch would warble) but the demo did not need that precision. I deleted it
all. The lesson is to match effort to what the thing is: a bunny demo, not a
music engine, and to surface the tradeoff in one line instead of building the
instrument first.

**Misread an emulator flag.** `-trap-range N` means "from the Nth trigger on",
not "at N", so early captures kept being overwritten until the emulator quit.
Several shots showed a later moment than their labels claimed, and I reasoned
about the wrong frames before noticing. `N-N` fixed it.

**Timing predictions drifted.** I traced the animation script by hand to pick
capture points. They were consistently close but about ten frames early once
the script grew, and one capture landed in the single blank frame between the
scene clear and the redraw, which briefly looked like a bug.

**Small friction.** Headless Chrome printed the DOM but never exited (twice,
once needing a kill, then wrapped in a hard timeout). A zsh loop over a
variable did not word-split, so a whole shot batch failed silently. Each
XRoar relaunch produced a "failed exit 2" notification from the previous
session I had killed, which I had to explain repeatedly so it did not read as
a real failure.

## Numbers

- Final app: 8244 bytes of the roughly 10K ROM-mode budget.
- 20 sprite frames, 2997 bytes, from 8 sheet crops, 8 hand-drawn ASCII frames
  and 4 mirrored copies.
- One loop of the show: about 15 seconds.
- blit2x: about 261 cycles of setup, 92 per source row, 44 per source byte.
  A full bunny frame is roughly 11,000 cycles, about 74% of a frame.

## Open items

- Never run on real hardware, only in XRoar.
- No tagged release or prebuilt binary for people who do not want the
  toolchain.
- screenshot.png is still in the repo but no longer shown in the README.
- The coco documentation fix (issue 572 there) is filed but uncommitted in
  that checkout.

## What I would do differently

1. Ask about the quality bar before building measurement infrastructure. One
   question ("do you care about exact pitch, or is a bleep fine?") would have
   saved the sound detour entirely.
2. Read the flag documentation for anything whose output I plan to reason
   about. The trap-range misread cost more than reading one help line.
3. Sort out capture permissions at the start of a visual project, rather than
   discovering them at the first screenshot.
