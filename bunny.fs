\ bunny.fs - LPC bunny demo for the CoCo (CG3, 128x96, 4 colors)
\
\ Art: PixelFarm, Stephen 'Redshrike' Challener (see CREDITS.md).
\ Design notes: PLAN.md. Work tracking: issues.jsonl.

INCLUDE build/coco-libs.fs

\ CG3 with CSS=1: buff, cyan, magenta, orange.
\ SAM V=4 (100); $FF22 bits 7-3 = A*/G=1 GM=100 CSS=1.
4    CONSTANT cg3-sam-v
$C8  CONSTANT cg3-pia
3072 CONSTANT cg3-size

\ cg3-init - point the SAM at vram-base, select CG3 CSS=1, clear to buff.
: cg3-init  ( -- )
  cg3-sam-v set-sam-v
  vram-base 9 RSHIFT set-sam-f
  cg3-pia set-pia
  vram-base cg3-size 0 FILL ;

: main  ( -- )
  cg3-init
  BEGIN vsync KEY? 3 = UNTIL
  exit-basic ;

main
