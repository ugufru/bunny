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
VARIABLE blt-w      \ blit2x scratch: source bytes per row
VARIABLE blt-h      \ blit2x scratch: source rows left

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

: main  ( -- )
  cg3-init
  seq-hop spr    32 80 0 blit2x
  seq-munch spr  96 80 0 blit2x
  BEGIN vsync KEY? 3 = UNTIL
  exit-basic ;

main
