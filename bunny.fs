\ bunny.fs - LPC bunny demo for the CoCo (CG3, 128x96, 4 colors)
\
\ Art: PixelFarm, Stephen 'Redshrike' Challener (see CREDITS.md).
\ Design notes: PLAN.md. Work tracking: issues.jsonl.

INCLUDE build/coco-libs.fs
INCLUDE build/sprites.fs

\ CG3 with CSS=1: buff, cyan, magenta, orange.
\ SAM V=4 (100); $FF22 bits 7-3 = A*/G=1 GM=100 CSS=1.
4    CONSTANT cg3-sam-v
$C8  CONSTANT cg3-pia
3072 CONSTANT cg3-size

VARIABLE vram       \ base address of the displayed CG3 page
VARIABLE dbl-tab    \ address of dbl-table, read by blit2x
VARIABLE blt-dst    \ blit2x scratch: current destination row
VARIABLE blt-w      \ blit2x / clear-rect scratch: bytes per row
VARIABLE blt-h      \ blit2x / clear-rect scratch: rows left

\ cg3-init - point the SAM at vram-base, select CG3 CSS=1, clear to buff.
: cg3-init  ( -- )
  vram-base vram !
  dbl-table dbl-tab !
  cg3-sam-v set-sam-v
  vram-base 9 RSHIFT set-sam-f
  cg3-pia set-pia
  vram-base cg3-size 0 FILL ;

\ blit2x - draw a sprite record opaque at 2x with its anchor at screen x,y.
\ The record's top-left lands at (x + 2*ox, y + 2*oy); x must be a multiple
\ of 4 (ox is always even) and the sprite must fit on screen: no clipping.
\ Each source row is written to two screen rows, each source byte to two
\ screen bytes through dbl-table. flags is reserved for mirroring (#8).
CODE blit2x  \ ( spr x y flags -- )
        PSHS    X,U
        LDY     6,U             ; Y = sprite record
        LDB     3,Y             ; oy, signed
        SEX
        ASLB
        ROLA                    ; D = 2*oy
        ADDD    2,U             ; D = top row
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA                    ; D = top * 32
        ADDD    FVAR_vram
        STD     FVAR_blt_dst
        LDB     2,Y             ; ox, signed
        SEX
        ASLB
        ROLA                    ; D = 2*ox
        ADDD    4,U             ; D = left pixel
        ASRA
        RORB
        ASRA
        RORB                    ; D = left byte
        ADDD    FVAR_blt_dst
        STD     FVAR_blt_dst
        LDB     ,Y              ; w (multiple of 4)
        LSRB
        LSRB
        STB     FVAR_blt_w+1
        LDB     1,Y
        STB     FVAR_blt_h+1
        LEAY    4,Y             ; Y = pixel data
        LDX     FVAR_dbl_tab
@row    LDU     FVAR_blt_dst
        LDA     FVAR_blt_w+1
        PSHS    A               ; byte counter
@byte   CLRA
        LDB     ,Y+
        ASLB
        ROLA                    ; D = 2 * source byte
        LDD     D,X             ; doubled pixels
        STD     32,U            ; second screen row
        STD     ,U++            ; first screen row
        DEC     ,S
        BNE     @byte
        LEAS    1,S
        LDD     FVAR_blt_dst
        ADDD    #64             ; down two screen rows
        STD     FVAR_blt_dst
        DEC     FVAR_blt_h+1
        BNE     @row
        PULS    X,U
        LEAU    8,U
        ;NEXT
;CODE

\ clear-rect - fill screen pixels l..r-1 x t..b-1 with buff. l and r are
\ multiples of 4. Does nothing when r <= l or b <= t.
CODE clear-rect  \ ( l t r b -- )
        PSHS    X,U
        LDD     2,U             ; r
        SUBD    6,U             ; r - l
        BLE     @done
        ASRA
        RORB
        ASRA
        RORB                    ; bytes per row
        STB     FVAR_blt_w+1
        LDD     ,U              ; b
        SUBD    4,U             ; b - t
        BLE     @done
        STB     FVAR_blt_h+1
        LDD     4,U             ; t
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA
        ASLB
        ROLA                    ; D = t * 32
        ADDD    FVAR_vram
        TFR     D,X
        LDD     6,U             ; l
        ASRA
        RORB
        ASRA
        RORB
        LEAX    D,X             ; X = first byte of the first row
        CLRA
@row    LDB     FVAR_blt_w+1
        TFR     X,Y
@byte   STA     ,Y+
        DECB
        BNE     @byte
        LEAX    32,X
        DEC     FVAR_blt_h+1
        BNE     @row
@done   PULS    X,U
        LEAU    8,U
        ;NEXT
;CODE

\ ---- Sprite rectangles -------------------------------------------------

VARIABLE rs  VARIABLE rx  VARIABLE ry          \ rect! inputs
VARIABLE rl  VARIABLE rt  VARIABLE rr  VARIABLE rb   \ new frame rect
VARIABLE o-l VARIABLE o-t VARIABLE o-r VARIABLE o-b  \ drawn frame rect

\ sx8 - sign-extend a byte.
: sx8  ( c -- n )  DUP 127 > IF 256 - THEN ;

\ rect! - screen rectangle of sprite spr anchored at x,y, into rl rt rr rb.
: rect!  ( spr x y -- )
  ry ! rx ! rs !
  rs @ 2 + C@ sx8 2* rx @ + rl !
  rs @ 3 + C@ sx8 2* ry @ + rt !
  rs @ C@ 2* rl @ + rr !
  rs @ 1 + C@ 2* rt @ + rb ! ;

\ rect>old - remember the new rect as the drawn one.
: rect>old  ( -- )  rl @ o-l !  rt @ o-t !  rr @ o-r !  rb @ o-b ! ;

\ erase-uncovered - clear the parts of the drawn rect outside the new rect.
: erase-uncovered  ( -- )
  o-l @  o-t @  rl @ o-r @ MIN  o-b @  clear-rect
  rr @ o-l @ MAX  o-t @  o-r @  o-b @  clear-rect
  o-l @ rl @ MAX  o-t @  o-r @ rr @ MIN  rt @ o-b @ MIN  clear-rect
  o-l @ rl @ MAX  rb @ o-t @ MAX  o-r @ rr @ MIN  o-b @  clear-rect ;

\ ---- Bunny ---------------------------------------------------------------

28 CONSTANT hop-start-x    \ leftmost anchor with every hop frame on screen
84 CONSTANT hop-end-x      \ rightmost anchor where hop4 still fits
80 CONSTANT ground-y

VARIABLE bx  VARIABLE by   \ bunny anchor (screen pixels)
VARIABLE drawn             \ true once a frame is on screen
VARIABLE pending           \ sprite to draw after the next vsync, 0 = none
VARIABLE aptr              \ current anim entry
VARIABLE ahold             \ frames left on the current entry

\ Drawing is split around vsync so the blit starts as soon as the beam
\ leaves the screen: all Forth bookkeeping (entry, anchor, rect!) happens
\ before vsync in tick, and draw-pending only blits and erases after it.
\ The erase can follow the blit because it only touches pixels outside the
\ new frame.

\ queue - make spr the frame to draw at the current anchor.
: queue  ( spr -- )  DUP pending !  bx @ by @ rect! ;

\ draw-pending - right after vsync: blit the queued frame, erase leftovers.
: draw-pending  ( -- )
  pending @ ?DUP IF
    bx @ by @ 0 blit2x
    drawn @ IF erase-uncovered THEN
    rect>old  1 drawn !  0 pending !
  THEN ;

\ hop-start - sit the bunny at the left edge and rewind the hop anim.
: hop-start  ( -- )
  hop-start-x bx !  ground-y by !
  anim-hop aptr !  30 ahold !
  seq-hop spr queue ;

\ advance - read the anim entry at aptr: move the anchor and queue its frame.
: advance  ( -- )
  aptr @ C@ 255 = IF
    bx @ hop-end-x < IF anim-hop aptr ! ELSE hop-start EXIT THEN
  THEN
  aptr @ 1 + C@ sx8 bx +!
  aptr @ 2 + C@ sx8 by +!
  aptr @ 3 + C@ ahold !
  aptr @ C@ spr queue
  aptr @ 5 + aptr ! ;

\ tick - once per frame, before vsync: step the anim when its hold runs out.
: tick  ( -- )
  ahold @ 1 - DUP ahold !
  0= IF advance THEN ;

: main  ( -- )
  cg3-init
  0 drawn !  0 pending !
  hop-start
  BEGIN vsync draw-pending tick KEY? 3 = UNTIL
  exit-basic ;

main
