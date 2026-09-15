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

NAME = bunny
SRC  = $(NAME).fs
BIN  = $(NAME).bin
GEN  = build/coco-libs.fs

all: $(BIN)

build:
	mkdir -p build

# Absolute INCLUDEs keep COCO overridable; fc.py resolves INCLUDE paths
# relative to the including file.
$(GEN): Makefile | build
	printf 'INCLUDE $(COCO)/lib/%s\n' $(COCO_LIBS) > $@

$(BIN): $(SRC) $(GEN) $(KERNEL_MAP) $(KERNEL_BIN)
	$(FC) $(SRC) \
	    --kernel     $(KERNEL_MAP) \
	    --kernel-bin $(KERNEL_BIN) \
	    --output     $(BIN)

$(KERNEL_MAP) $(KERNEL_BIN):
	$(MAKE) -C $(KERNEL_DIR)

run: $(BIN)
	xroar -machine coco2bus -ram 32 $(XROAR_ROMS) $(XROAR_EXTRA) -run $(BIN)

cycles: $(SRC) $(GEN) $(KERNEL_MAP)
	$(FC) $(SRC) --kernel $(KERNEL_MAP) --cycles --output build/cycles.bin

clean:
	rm -rf build $(BIN)

.PHONY: all run cycles clean
