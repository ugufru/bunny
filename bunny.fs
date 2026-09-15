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
  vram-base cg3-size 0 FILL
  grass  vram-base 80 32 * +  512 CMOVE ;

\ blit2x - draw a sprite record opaque at 2x with its anchor at screen x,y.
\ The record's top-left lands at (x + 2*ox, y + 2*oy); x must be a multiple
\ of 4 (ox is always even) and the sprite must fit on screen: no clipping.
\ Each source row is written to two screen rows, each source byte to two
\ screen bytes through dbl-table. flags is unused: left-facing frames are
\ separate mirrored records made by tools/png2cg.py.
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

\ ---- Sound ---------------------------------------------------------------
\ lib/async-sound.fs voice with a sine wavetable, plus noise bursts. The
\ main loop's sound-step plays a noise burst while one is running, otherwise
\ snd-fill samples for the voice. Anim events trigger the effects.

$7000 CONSTANT wave-base   \ sine wavetable, in the heap below the data stack

: snd-setup  ( -- )
  snd-async-init
  wave-base DUP gen-sine-hq snd-waveform ;

VARIABLE noise-left   \ frames left in the current noise burst, 0 = none
VARIABLE noise-n      \ noise samples per frame
VARIABLE noise-fade   \ attenuation added per frame (fade out)

\ voice - get the tone voice ready: cancel any noise burst, sine wavetable,
\ ring mod off. snd-noise-fill shares snd-amp with the voice, so only one
\ of them may run at a time; ring mod and waveform persist in the library.
: voice  ( -- )  0 noise-left !  wave-base snd-waveform  0 snd-ringmod! ;

\ noise - start a noise burst; stops the voice.
\   frames  burst length   n     samples per frame   div  lines per sample
\   amp     attenuation    fade  attenuation added per frame
: noise  ( frames n div amp fade -- )
  snd-stop  noise-fade !  snd-amp !  snd-noise-div !  noise-n !  noise-left ! ;

\ sound-step - once per frame: noise burst if running, else the voice.
: sound-step  ( -- )
  noise-left @ IF
    noise-n @ snd-noise-fill
    snd-amp @ noise-fade @ + 255 MIN snd-amp !
    noise-left @ 1 - noise-left !
  ELSE
    snd-playing? IF 80 snd-fill THEN
  THEN ;

\ All effects are quiet: attenuation 140..190 of 255.

\ boing - springy metallic sproing on hop takeoff: high sine sweeping up
\ fast, ring modulated, fading out.
: boing  ( -- )
  voice  1400 170 10 snd-note  220 snd-slide!  6 snd-env!  9 snd-ringmod! ;

\ snore - low triangle that fades, with each new z.
: snore  ( -- )
  voice  3 snd-shape  110 190 24 snd-note  2 snd-slide!  3 snd-env! ;

\ chirp - quick rising sine when the bunny wakes.
: chirp  ( -- )  voice  1800 180 8 snd-note  160 snd-slide!  8 snd-env! ;

\ sniff - short bright noise tick on a nose wiggle.
: sniff  ( -- )  3 24 1 190 10 noise ;

\ crunch - lower noise burst that fades, on a carrot bite.
: crunch  ( -- )  6 40 2 140 18 noise ;

\ ---- Scene ---------------------------------------------------------------
\ At 2x the anchor can range 28..84 before a frame leaves the screen. The
\ bunny sits at start-x, hops once to 56 and eats the carrot at carrot-x,
\ whose tip is just past the munching nose, stepping right after each bite
\ (grass fills rows 80-95, below every sprite). Bunny frames are padded 4 px
\ past their pixels on the right, so their rect overlaps the carrot's by one
\ byte column; the carrot is redrawn after every bunny draw to cover that.

28 CONSTANT start-x
80 CONSTANT ground-y
88 CONSTANT carrot-x

VARIABLE bx  VARIABLE by   \ bunny anchor (screen pixels)
VARIABLE drawn             \ true once a frame is on screen
VARIABLE shown             \ sprite currently on screen
VARIABLE pending           \ sprite to draw after the next vsync, 0 = none
VARIABLE aptr              \ current anim entry
VARIABLE ahold             \ frames left on the current entry
VARIABLE carrot            \ carrot sprite on screen, 0 = none
VARIABLE carrot-dirty      \ carrot needs a redraw after vsync
VARIABLE zstep             \ z shown at position 1..3, 0 = none

VARIABLE carrot-was        \ carrot sprite before the last change, 0 = none

\ carrot-show - put carrot sprite spr on screen after the next vsync.
: carrot-show  ( spr -- )  carrot @ carrot-was !  carrot !  1 carrot-dirty ! ;

\ bite - step the carrot to its next eaten stage.
: bite  ( -- )
  carrot @ spr-carrot3 = IF spr-carrot2 ELSE
  carrot @ spr-carrot2 = IF spr-carrot1 ELSE spr-carrot0 THEN THEN
  carrot-show ;

\ z positions rise up and to the right, above the sleeping head.
: z-x  ( n -- x )  8 * bx @ + ;
: z-y  ( n -- y )  10 * 46 SWAP - ;

\ z-rect - screen rect of the z at position n.
: z-rect  ( n -- l t r b )  DUP z-x SWAP z-y >R  DUP R@ 10 -  ROT 16 +  R> ;

\ z-clear - erase the z on screen, if any.
: z-clear  ( -- )  zstep @ ?DUP IF z-rect clear-rect THEN  0 zstep ! ;

\ z-next - move the z to the next position, wrapping after 3.
: z-next  ( -- )
  zstep @ z-clear
  3 /MOD DROP 1 + DUP zstep !
  spr-z SWAP DUP z-x SWAP z-y 0 blit2x ;

\ scene-reset - clear the bunny, carrot and z; bunny back to start-x.
: scene-reset  ( -- )
  drawn @ IF o-l @ o-t @ o-r @ o-b @ clear-rect THEN
  0 drawn !  0 shown !
  carrot @ IF carrot-x ground-y 14 -  carrot-x 32 +  ground-y clear-rect THEN
  0 carrot !
  z-clear
  start-x bx !  ground-y by ! ;

\ carrot-clear - erase the carrot (or its greens) and any z.
: carrot-clear  ( -- )
  carrot @ IF carrot-x ground-y 14 -  carrot-x 32 +  ground-y clear-rect THEN
  0 carrot !  0 carrot-was !
  z-clear ;

\ event - run an anim entry's event id.
: event  ( id -- )
  DUP 1 = IF boing THEN
  DUP 2 = IF bite crunch THEN
  DUP 3 = IF spr-carrot3 carrot-show THEN
  DUP 4 = IF z-next snore THEN
  DUP 5 = IF z-clear THEN
  DUP 6 = IF scene-reset THEN
  DUP 7 = IF carrot-clear THEN
  DUP 8 = IF sniff THEN
  9 = IF chirp THEN ;

\ Drawing is split around vsync so the blit starts as soon as the beam
\ leaves the screen: all Forth bookkeeping (entry, anchor, rect!) happens
\ before vsync in tick, and draw-pending only blits and erases after it.
\ The erase can follow the blit because it only touches pixels outside the
\ new frame.

\ queue - make spr the frame to draw at the current anchor.
: queue  ( spr -- )  DUP pending !  bx @ by @ rect! ;

\ carrot-left - screen x of the left edge of carrot sprite spr. Eaten
\ stages are trimmed, so they start further right.
: carrot-left  ( spr -- x )  2 + C@ sx8 2* carrot-x + ;

\ draw-carrot - erase the strip the last bite removed, then blit the carrot.
: draw-carrot  ( -- )
  carrot-was @ ?DUP IF
    carrot-left  ground-y 14 -  carrot @ carrot-left  ground-y  clear-rect
    0 carrot-was !
  THEN
  carrot @ carrot-x ground-y 0 blit2x  0 carrot-dirty ! ;

\ draw-pending - right after vsync: blit the queued frame, erase leftovers,
\ then redraw the carrot if the bunny covered it or it changed.
: draw-pending  ( -- )
  pending @ ?DUP IF
    bx @ by @ 0 blit2x
    drawn @ IF erase-uncovered THEN
    rect>old  1 drawn !
    pending @ shown !  0 pending !
    1 carrot-dirty !
  THEN
  carrot @ IF carrot-dirty @ IF draw-carrot THEN THEN ;

\ same-frame? - true when the entry at aptr would redraw what is on screen.
: same-frame?  ( spr -- f )
  shown @ =
  aptr @ 1 + C@  aptr @ 2 + C@ OR 0=  AND
  drawn @ AND ;

\ advance - run the anim entry at aptr: event, move the anchor, queue its
\ frame unless it is already on screen. A hold of 0 means a random
\ 60..187 frames (kernel rnd wants a power of two). anim-show loops forever.
: advance  ( -- )
  aptr @ C@ 255 = IF anim-show aptr ! THEN
  aptr @ 4 + C@ ?DUP IF event THEN
  aptr @ 1 + C@ sx8 bx +!
  aptr @ 2 + C@ sx8 by +!
  aptr @ 3 + C@ ?DUP 0= IF 128 rnd 60 + THEN ahold !
  aptr @ C@ spr DUP same-frame? IF DROP ELSE queue THEN
  aptr @ 5 + aptr ! ;

\ tick - once per frame, before vsync: step the anim when its hold runs out.
: tick  ( -- )
  ahold @ 1 - DUP ahold !
  0= IF advance THEN ;

: main  ( -- )
  cg3-init
  snd-setup
  0 drawn !  0 shown !  0 pending !  0 carrot !  0 zstep !  0 noise-left !
  start-x bx !  ground-y by !
  anim-show aptr !  1 ahold !
  BEGIN
    vsync draw-pending snd-frame tick
    sound-step
    KEY? 3 =
  UNTIL
  snd-stop
  exit-basic ;

main
