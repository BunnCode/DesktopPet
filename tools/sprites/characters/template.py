"""Starting point for a new character: copy this file to characters/<name>.py and edit.

apply(base) receives make_rabbit and overrides only what differs from the built-in rabbit. Every override is
optional; this template just recolours the rabbit so it runs as-is:

    python tools/sprites/make_character.py template

Things you can replace (see docs/SPRITE_GENERATOR.md for what each does and docs/CHARACTER_SPRITES.md for the
process):
- base.PAL[key] = (r, g, b)                  colours: fur F/f/W, hair H/h, inner ear P, outfit J/j/k, shorts s/g, ...
- base.EAR_SETS = {...}                      ear placement per ear pose ('up', 'tall', 'down')
- base.ear = def ear(bx, by, angle, length, hw) -> (outer, inner, tuft) shapes   ear shape (e.g. round, floppy)
- base.EYES / base.MOUTHS / base.NOSE         face stamps (keep every key; eyes stay 5x7)
- base.head = def head(cx, cy, ears, eye, mouth_kind, look, blush) -> [ears layer, head layer]
- base.hoodie = def hoodie(ux, uy) -> layer  the top (keep the body polygon's size so poses still fit)
- base.shorts = def shorts(ux, uy, sit=False) -> layer
- base.tail = def tail(ux, uy) -> layer
- base.arm / base.leg                        sleeve and leg drawing (keep the signatures)

- base.LEGS / base.SIT_LEGS / base.FOOT       leg poses (joint positions)

Height, build and proportions can change too. The rig's layout lives in base.HX, base.HY (head centre),
base.SHOULDER_BACK / base.SHOULDER_FRONT, the hips (first point of every LEGS path) and the body polygons in
hoodie()/shorts(). Change those together. Then, because the arm paths in frames() are absolute cell coordinates,
copy base.frames into this file as frames(), retune its coordinates and assign base.frames = frames. Keep the
feet on row 63, everything inside the 64x64 cell, and the pose conventions (sprite_preview.py check).

Write new functions in this file, using base's helpers: base.Layer, base.F, base.ellipse, base.polygon,
base.stroke, base.both, base.minus, base.off, base.grid, base.math, base.rabbit, base.arm, base.leg.
"""


def apply(base):
    base.PAL.update({
        'F': (0xD8, 0xB4, 0x8C),   # fur: tan
        'f': (0xB0, 0x8A, 0x64),   # fur shade
        'W': (0xF4, 0xE8, 0xD8),   # muzzle, tail
        'H': (0x6A, 0x3E, 0x28),   # hair: brown
        'h': (0x8E, 0x5A, 0x3C),   # hair shine
        'J': (0x5A, 0x8A, 0x4A),   # hoodie: green
        'j': (0x42, 0x66, 0x36),
        'k': (0x7A, 0xAA, 0x66),
    })
