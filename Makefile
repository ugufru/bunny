# bunny: LPC bunny demo for the CoCo on the coco Forth toolchain.
#
# Mirrors ~/github/coco/make/demo.mk, which can't be included from here
# because it hardcodes ../../ paths. Override COCO to point elsewhere.

COCO       ?= $(HOME)/github/coco
FC          = python3 $(COCO)/tools/fc.py
KERNEL_DIR  = $(COCO)/kernel
KERNEL_MAP  = $(KERNEL_DIR)/build/kernel.map
KERNEL_BIN  = $(KERNEL_DIR)/build/kernel.bin
XROAR_ROMS  = -bas ~/.xroar/roms/bas12.rom -extbas ~/.xroar/roms/extbas11.rom
XROAR_EXTRA ?= -kbd-translate

# coco libraries pulled in through build/coco-libs.fs (bye.fs brings vdg.fs
# and screen.fs with it).
COCO_LIBS   = bye.fs

NAME    = bunny
SRC     = $(NAME).fs
BIN     = $(NAME).bin
GEN     = build/coco-libs.fs
SPRITES = build/sprites.fs
PREVIEW = build/preview.png

all: $(BIN)

build:
	mkdir -p build

# Absolute INCLUDEs keep COCO overridable; fc.py resolves INCLUDE paths
# relative to the including file.
$(GEN): Makefile | build
	printf 'INCLUDE $(COCO)/lib/%s\n' $(COCO_LIBS) > $@

# Sprite data and its preview image come from one converter run.
$(SPRITES): tools/png2cg.py tools/frames.json assets/bunnysheet5.png | build
	python3 tools/png2cg.py tools/frames.json build

$(PREVIEW): $(SPRITES)

preview: $(PREVIEW)
	open $(PREVIEW)

$(BIN): $(SRC) $(GEN) $(SPRITES) $(KERNEL_MAP) $(KERNEL_BIN)
	$(FC) $(SRC) \
	    --kernel     $(KERNEL_MAP) \
	    --kernel-bin $(KERNEL_BIN) \
	    --output     $(BIN)

$(KERNEL_MAP) $(KERNEL_BIN):
	$(MAKE) -C $(KERNEL_DIR)

run: $(BIN)
	xroar -machine coco2bus -ram 32 $(XROAR_ROMS) $(XROAR_EXTRA) -run $(BIN)

# Headless capture: run with no window or audio, trap at the first N-th call
# of the kernel vsync (SHOT_AT, default 1), dump RAM and render the CG3 page
# to build/shot.png. Works when the XRoar window is on another Space.
SHOT_AT ?= 1
VSYNC_PC = $(shell awk '/Symbol: CODE_VSYNC /{print $$NF}' $(KERNEL_MAP))

shot: $(BIN)
	rm -f build/shot.ram
	perl -e 'alarm 60; exec @ARGV' xroar -machine coco2bus -ram 32 $(XROAR_ROMS) \
	    -ui null -ao null -run $(BIN) \
	    -trap pc=0x$(VSYNC_PC) -trap-range $(SHOT_AT)-$(SHOT_AT) \
	    -trap-snap build/shot.ram -trap-timeout 1 -timeout 50 > build/shot.log 2>&1
	python3 tools/cg3shot.py build/shot.ram build/shot.png

cycles: $(SRC) $(GEN) $(SPRITES) $(KERNEL_MAP)
	$(FC) $(SRC) --kernel $(KERNEL_MAP) --cycles --output build/cycles.bin

clean:
	rm -rf build $(BIN)

.PHONY: all preview run cycles clean
