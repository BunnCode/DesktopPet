"""Generates the default rabbit sprite sheet (src/DesktopPet.App/SpriteSheet/rabbit.png) and the default furniture
art (src/DesktopPet.App/Furniture/Art/<kind>.png).

Pixel art is composed from ASCII parts (head, face, body poses, props) so frames stay consistent and the art can be
tweaked in one place. One row per animation in the game's order (Animations.All), 32x32 cells, art facing right,
feet on the bottom row.

    python tools/sprites/make_rabbit.py [--preview out.png] [--furniture-preview out.png]
"""
import argparse
import os
import sys

from PIL import Image

CELL = 32

# Palette. The jacket colours (J, j, k) are swapped per pet by the app, so keep them unique in the sheet.
PAL = {
    '.': None,
    'K': (0x26, 0x22, 0x2B),  # outline
    'F': (0xC4, 0xC1, 0xCA),  # fur
    'f': (0x9C, 0x98, 0xA4),  # fur shade
    'W': (0xEC, 0xEA, 0xEF),  # muzzle / light fur
    'H': (0x55, 0x50, 0x5E),  # hair
    'h': (0x76, 0x70, 0x80),  # hair highlight
    'P': (0xEC, 0xB6, 0xC0),  # inner ear
    'J': (0x3E, 0x45, 0x5A),  # jacket
    'j': (0x2C, 0x31, 0x42),  # jacket shade
    'k': (0x58, 0x61, 0x7A),  # jacket highlight
    'S': (0xE2, 0xE0, 0xE6),  # shirt
    'E': (0x15, 0x13, 0x19),  # eye
    'w': (0xFF, 0xFF, 0xFF),  # eye shine
    'B': (0xF2, 0x9C, 0xAA),  # blush
    'N': (0x6E, 0x55, 0x60),  # nose
    'M': (0x7A, 0x30, 0x3C),  # open mouth
    'T': (0xE8, 0x7C, 0x8C),  # tongue
    'R': (0xD8, 0x35, 0x2B),  # apple
    'r': (0xF2, 0x7A, 0x6E),  # apple shine
    'L': (0x4C, 0xAF, 0x50),  # leaf
    'Z': (0x9F, 0xC5, 0xFF),  # snooze
    'X': (0xD8, 0x50, 0x50),  # anger mark
    # Furniture and props (none of these may equal a jacket colour).
    'G': (0x5C, 0x5E, 0x6E),  # grey plastic / metal (TV, controller, trampoline)
    'V': (0x1E, 0x26, 0x34),  # dark screen / trampoline mat
    'O': (0xB8, 0x82, 0x4E),  # wood
    'o': (0x8C, 0x5E, 0x36),  # wood shade
    'C': (0xC8, 0x5C, 0x6C),  # couch fabric
    'c': (0x9A, 0x42, 0x52),  # couch shade
    'U': (0x5C, 0x8E, 0xD6),  # blanket
    'u': (0x41, 0x6A, 0xAE),  # blanket shade
    'Y': (0xF2, 0xC8, 0x4A),  # yellow (game pixels)
    'Q': (0xE8, 0x4A, 0x6A),  # hearts
    'q': (0xFF, 0x9E, 0xB4),  # heart shine
    'A': (0xF4, 0xEC, 0xD8),  # egg shell
    'a': (0xD8, 0xCC, 0xB0),  # egg shell shade
}


def grid(rows):
    w = max(len(r) for r in rows)
    return [r.ljust(w, '.') for r in rows]


def blank():
    return [['.'] * CELL for _ in range(CELL)]


def stamp(canvas, part, x0, y0, flip=False):
    """Draws a part with its top-left at (x0, y0); '.' is transparent."""
    for y, row in enumerate(part):
        if flip:
            row = row[::-1]
        for x, c in enumerate(row):
            if c == '.':
                continue
            cx, cy = x0 + x, y0 + y
            if 0 <= cx < CELL and 0 <= cy < CELL:
                canvas[cy][cx] = c


# ---------------------------------------------------------------- head
# 22 wide. Rows 0-8 are ears, 9-21 the head. Facing right: the face sits right of centre.
EARS_UP = grid([
    "....KK................",
    "...KFFK..........KKK..",
    "...KFPFK........KFFFK.",
    "...KFPFK.......KFPPFK.",
    "...KFPFK......KFPPFK..",
    "...KFPFK.....KFPPFK...",
    "...KFPFK....KFPPFK....",
    "....KFPFK..KFPFFK.....",
    "....KFFFKKKFFFFK......",
])

# Ears blown straight up (falling, surprised).
EARS_TALL = grid([
    "....KK.......KKK......",
    "...KFFK.....KFFFK.....",
    "...KFPFK....KFPPFK....",
    "...KFPFK....KFPPFK....",
    "...KFPFK....KFPPFK....",
    "...KFPFK....KFPPFK....",
    "...KFPFK....KFPPFK....",
    "....KFPFK..KFPPFK.....",
    "....KFFFKKKFFFFK......",
])

# Ears flopped back and down behind the head (sad, sleepy, bracing). Taller than 9 rows: the extra rows only show
# where the head is transparent, so the tips hang down behind it.
EARS_DOWN = grid([
    "......................",
    "......................",
    "......................",
    "......................",
    "...KKKKKK.............",
    "..KFFFFFFKKK..........",
    ".KFPPPPPFFFFKK........",
    "KFPPKKKKPPFFFFK.......",
    "KFPK....KKKFFFFK......",
    "KFK...................",
    "KK....................",
])

HEAD = grid([
    "...KKHHHHHHHHHHHKK....",
    "..KHHhhhhHHHHHHHHHK...",
    "KKHhhHHHHHhhhHHHHHHK..",
    "KHHKHHHHHHHHHHHHHHHHK.",
    ".KKHHHHHHHHHHHHHHHHHHK",
    "..KHHHHHHHHHHHHHHHHHFK",
    "..KfHHHFHHHHHHHHHHFFFK",
    ".KfFFHFFFHHHHFHHHFFFFK",
    "KffFFFFFFFHHFFFHFFFFFK",
    ".KfFFFFFFFFFHFFFFFWWWK",
    "..KfFFFFFFFFFFFFWWWWK.",
    "...KKfFFFFFFFFFFWWWK..",
    ".....KKKKKKKKKKKKKK...",
])

EAR_ROWS = 9  # head rows start here

# Face features, relative to the head part's top-left (including the ear rows).
EYE_BACK = (7, 15)
EYE_FRONT = (14, 15)
NOSE = (19, 18)
MOUTH = (17, 19)


def eyes(kind, look=0):
    """Points for both eyes. kind: open, blink, happy, wide, squeeze, droopy, angry."""
    pts = []
    for i, (ex, ey) in enumerate((EYE_BACK, EYE_FRONT)):
        ex += look
        if kind == 'open':
            pts += [(ex - 1, ey - 1, 'K'), (ex, ey - 1, 'K'), (ex + 1, ey - 1, 'K'),
                    (ex, ey, 'w'), (ex + 1, ey, 'E'),
                    (ex, ey + 1, 'E'), (ex + 1, ey + 1, 'E'),
                    (ex, ey + 2, 'E'), (ex + 1, ey + 2, 'E')]
        elif kind == 'blink':
            pts += [(ex - 1, ey + 1, 'K'), (ex, ey + 2, 'K'), (ex + 1, ey + 2, 'K'), (ex + 2, ey + 1, 'K')]
        elif kind == 'happy':
            pts += [(ex - 1, ey + 1, 'K'), (ex, ey, 'K'), (ex + 1, ey, 'K'), (ex + 2, ey + 1, 'K')]
        elif kind == 'wide':
            pts += [(ex - 1, ey - 1, 'K'), (ex, ey - 1, 'E'), (ex + 1, ey - 1, 'E'), (ex + 2, ey - 1, 'K'),
                    (ex - 1, ey, 'E'), (ex, ey, 'w'), (ex + 1, ey, 'E'), (ex + 2, ey, 'E'),
                    (ex - 1, ey + 1, 'E'), (ex, ey + 1, 'E'), (ex + 1, ey + 1, 'E'), (ex + 2, ey + 1, 'E'),
                    (ex, ey + 2, 'E'), (ex + 1, ey + 2, 'E')]
        elif kind == 'squeeze':  # > <
            if i == 0:
                pts += [(ex - 1, ey, 'K'), (ex, ey + 1, 'K'), (ex + 1, ey + 1, 'K'), (ex - 1, ey + 2, 'K')]
            else:
                pts += [(ex + 2, ey, 'K'), (ex + 1, ey + 1, 'K'), (ex, ey + 1, 'K'), (ex + 2, ey + 2, 'K')]
        elif kind == 'droopy':  # heavy lids, looking down
            pts += [(ex - 1, ey, 'K'), (ex, ey, 'K'), (ex + 1, ey, 'K'), (ex + 2, ey + 1, 'K'),
                    (ex, ey + 1, 'E'), (ex + 1, ey + 1, 'E'),
                    (ex, ey + 2, 'E'), (ex + 1, ey + 2, 'w')]
        elif kind == 'angry':  # lids slanting down to the nose
            pts += [(ex - 1, ey - (0 if i == 0 else 1), 'K'), (ex, ey - 1 + (1 if i == 0 else 0), 'K'),
                    (ex + 1, ey, 'K'), (ex + 2, ey + (1 if i == 0 else -1), 'K'),
                    (ex, ey + 1, 'E'), (ex + 1, ey + 1, 'E'), (ex, ey + 2, 'E'), (ex + 1, ey + 2, 'w')]
    return pts


def mouth(kind):
    mx, my = MOUTH
    if kind == 'none':
        return [(mx + 1, my, 'K')]
    if kind == 'smile':
        return [(mx - 1, my, 'K'), (mx, my + 1, 'K'), (mx + 1, my + 1, 'K'), (mx + 2, my, 'K')]
    if kind == 'open':
        return [(mx, my, 'K'), (mx + 1, my, 'K'), (mx, my + 1, 'M'), (mx + 1, my + 1, 'T')]
    if kind == 'shout':
        return [(mx - 1, my, 'K'), (mx, my, 'K'), (mx + 1, my, 'K'),
                (mx - 1, my + 1, 'M'), (mx, my + 1, 'M'), (mx + 1, my + 1, 'T')]
    if kind == 'frown':
        return [(mx - 1, my + 1, 'K'), (mx, my, 'K'), (mx + 1, my, 'K'), (mx + 2, my + 1, 'K')]
    if kind == 'pout':
        return [(mx, my, 'K'), (mx + 1, my, 'K')]
    if kind == 'munch':
        return [(mx, my, 'K'), (mx + 1, my, 'M'), (mx + 1, my + 1, 'K')]
    raise ValueError(kind)


def head(ears='up', eye='open', mouth_kind='none', look=0, blush=False):
    ear_part = {'up': EARS_UP, 'tall': EARS_TALL, 'down': EARS_DOWN}[ears]
    canvas = [list(r) for r in (['.' * len(HEAD[0])] * EAR_ROWS + HEAD)]
    for y, row in enumerate(ear_part):  # behind the head
        for x, c in enumerate(row):
            if c != '.' and canvas[y][x] == '.':
                canvas[y][x] = c
    # Pointed fringe over the face is part of HEAD; draw features on top.
    for (x, y, c) in eyes(eye, look) + mouth(mouth_kind) + [(NOSE[0], NOSE[1], 'N'), (NOSE[0] + 1, NOSE[1], 'N')]:
        canvas[y][x] = c
    if blush:
        for (x, y) in ((5, 18), (6, 18), (13, 18), (14, 18)):
            canvas[y][x] = 'B'
    return [''.join(r) for r in canvas]


# ---------------------------------------------------------------- bodies
# Torsos are 16 wide and 8 tall; legs 16 wide and 3 tall. Bodies are bottom-aligned (last row = ground).
TORSO_ARMS_DOWN = grid([
    "....KKKKKKKK....",
    "...KJJSSSJJJK...",
    "..KJjJJSSJJjJK..",
    "..KJjJJJSJJjJK..",
    "..KjKJJJJJJKjK..",
    "..KFKJJJJJJKFK..",
    "...KKjJJJJjKK...",
    "....KJJJJJJK....",
])

# No arms: arms are drawn separately with limb().
TORSO = grid([
    "....KKKKKKKK....",
    "...KJJSSSJJJK...",
    "...KJJJSSJJjK...",
    "...KjJJJSJJjK...",
    "...KjJJJJJJjK...",
    "...KjJJJJJJjK...",
    "...KKjJJJJjKK...",
    "....KJJJJJJK....",
])

LEGS = {
    'stand': grid([
        "....KFFKKFFK....",
        "...KFFFKKFFFFK..",
        "...KKKKK.KKKKK..",
    ]),
    'wide': grid([
        "...KFFK..KFFK...",
        "..KFFFK..KFFFFK.",
        "..KKKKK..KKKKKK.",
    ]),
    'lift': grid([
        "....KFFKKFFFFK..",
        "...KFFFKKKKKK...",
        "...KKKKK........",
    ]),
    'dangle': grid([
        "....KFFKKFFK....",
        "....KFFKKFFK....",
        "....KKK..KKK....",
    ]),
    'kick': grid([
        "....KFFKKFFK....",
        "...KFFK..KFFK...",
        "...KKK....KKK...",
    ]),
    'back': grid([  # only the back leg (the front one is a limb)
        "....KFFK........",
        "...KFFFK........",
        "...KKKKK........",
    ]),
}

# Sitting on the floor, legs out in front (to the right), feet up. 16 wide, 9 tall.
SIT = grid([
    "....KKKKKKKK....",
    "...KJJSSSJJJK...",
    "..KJjJJSSJJjJK..",
    "..KjKJJJSJJKjK..",
    "..KFKJJJJJJKFKK.",
    "...KjJJJJJJjKFFK",
    "..KfFFFFFFFFFFFK",
    "..KffFFFFFFFFFFK",
    "...KKKKKKKKKKKK.",
])


# ---------------------------------------------------------------- limbs
def line(x0, y0, x1, y1):
    pts = []
    dx, dy = abs(x1 - x0), -abs(y1 - y0)
    sx, sy = (1 if x0 < x1 else -1), (1 if y0 < y1 else -1)
    err = dx + dy
    while True:
        pts.append((x0, y0))
        if x0 == x1 and y0 == y1:
            return pts
        e2 = 2 * err
        if e2 >= dy:
            err += dy
            x0 += sx
        if e2 <= dx:
            err += dx
            y0 += sy


def limb(path, fill='J', end='F', end_size=2):
    """A 2 px thick limb along a polyline, with a paw at the end, outlined. Returns a full-cell layer."""
    layer = blank()
    for (a, b) in zip(path, path[1:]):
        for (x, y) in line(*a, *b):
            for (ox, oy) in ((0, 0), (1, 0), (0, 1), (1, 1)):
                if 0 <= x + ox < CELL and 0 <= y + oy < CELL:
                    layer[y + oy][x + ox] = fill
    ex, ey = path[-1]
    for oy in range(end_size):
        for ox in range(end_size):
            if 0 <= ex + ox < CELL and 0 <= ey + oy < CELL:
                layer[ey + oy][ex + ox] = end
    outline(layer)
    return layer


def outline(layer):
    filled = {(x, y) for y in range(CELL) for x in range(CELL) if layer[y][x] != '.'}
    for (x, y) in filled:
        for (nx, ny) in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < CELL and 0 <= ny < CELL and (nx, ny) not in filled:
                layer[ny][nx] = 'K'


def stamp_layer(canvas, layer):
    for y in range(CELL):
        for x in range(CELL):
            if layer[y][x] != '.':
                canvas[y][x] = layer[y][x]


# ---------------------------------------------------------------- props
APPLE = grid([
    "...KK.",
    "..KLLK",
    ".KKKK.",
    "KRrRRK",
    "KRRRRK",
    "KRRRRK",
    ".KKKK.",
])
APPLE_BITTEN = grid([
    "...KK.",
    "..KLLK",
    ".KKKK.",
    "..KrRK",
    "...KRK",
    "..KRRK",
    ".KKKK.",
])
Z_SMALL = grid([
    "ZZZ",
    "..Z",
    ".Z.",
    "ZZZ",
])
Z_BIG = grid([
    "ZZZZ",
    "..Z.",
    ".Z..",
    "ZZZZ",
])
HEART = grid([
    ".KK.KK.",
    "KQqKQQK",
    "KQQQQQK",
    ".KQQQK.",
    "..KQK..",
    "...K...",
])
HEART_SMALL = grid([
    "KK.KK",
    "KQKQK",
    ".KQK.",
    "..K..",
])
PAD = grid([  # game controller
    ".KKKKKK.",
    "KGRGGLGK",
    ".KKKKKK.",
])
PAD_PRESSED = grid([
    ".KKKKKK.",
    "KGLGGRGK",
    ".KKKKKK.",
])


# ---------------------------------------------------------------- frames
class F:
    """One frame being built: parts and limbs are drawn back to front."""

    def __init__(self):
        self.c = blank()

    def part(self, part, x, y):
        stamp(self.c, part, x, y)
        return self

    def limb(self, path, **kw):
        stamp_layer(self.c, limb(path, **kw))
        return self

    def grid(self):
        return self.c


HX, HY = 5, 0  # head position when standing
BX = 8         # body x


def standing(head_part, legs='stand', torso=TORSO_ARMS_DOWN, head_dy=0, head_dx=0):
    b = torso + LEGS[legs]
    return F().part(b, BX, CELL - len(b)).part(head_part, HX + head_dx, HY + head_dy)


def frames():
    """Animation id -> list of 32x32 char grids."""
    a = {}

    a['idle'] = [standing(head(eye='blink' if i == 13 else 'open'), head_dy=1 if (i // 4) % 2 else 0).grid()
                 for i in range(16)]

    a['walk'] = [
        standing(head(look=1), legs='wide').grid(),
        standing(head(look=1), legs='lift', head_dy=-1).grid(),
        standing(head(look=1), legs='stand').grid(),
        standing(head(look=1), legs='lift', head_dy=-1).grid(),
    ]

    def sit(eye, mouth_kind='smile', dy=0, ears='up', extras=()):
        f = F().part(SIT, BX, CELL - len(SIT)).part(head(ears=ears, eye=eye, mouth_kind=mouth_kind), HX, 2 + dy)
        for (p, x, y) in extras:
            f.part(p, x, y)
        return f.grid()

    a['sit'] = [sit('happy', dy=-1), sit('happy', dy=0)]

    # Falling: ears up, arms flung out, feet fluttering.
    def fall(i):
        b = TORSO + LEGS['dangle' if i == 0 else 'kick']
        f = F().part(b, BX, CELL - len(b))
        f.limb([(13, 23), (2, 18)] if i == 0 else [(13, 23), (2, 20)])
        f.limb([(18, 23), (28, 17)] if i == 0 else [(18, 23), (28, 19)])
        f.part(head(ears='tall', eye='wide', mouth_kind='open'), HX, HY)
        return f.grid()

    a['fall'] = [fall(0), fall(1)]

    a['land'] = [
        standing(head(ears='down', eye='squeeze'), legs='wide', head_dy=5).grid(),
        standing(head(ears='down', eye='squeeze'), legs='wide', head_dy=3).grid(),
        standing(head(ears='up', eye='open'), legs='stand', head_dy=1).grid(),
        standing(head(ears='up', eye='open'), legs='stand', head_dy=0).grid(),
    ]

    # Held by the scruff: arms flailing, feet kicking, protesting.
    def held(i):
        b = TORSO + LEGS['kick' if i == 0 else 'dangle']
        f = F().part(b, BX, CELL - len(b))
        f.limb([(13, 23), (3, 21)] if i == 0 else [(13, 23), (3, 25)])
        f.limb([(18, 23), (27, 25)] if i == 0 else [(18, 23), (27, 21)])
        f.part(head(ears='tall', eye='squeeze', mouth_kind='shout'), HX, HY)
        return f.grid()

    a['held'] = [held(0), held(1)]

    # Happy: bouncing, arms up on the high frames.
    def happy(i):
        f = F()
        if i % 2 == 1:
            b = TORSO + LEGS['dangle']
            f.part(b, BX, CELL - len(b) - 1)
            f.limb([(13, 22), (3, 15)])
            f.limb([(18, 22), (28, 14)])
            f.part(head(eye='happy', mouth_kind='open', blush=True), HX, HY - 1)
        else:
            b = TORSO_ARMS_DOWN + LEGS['stand']
            f.part(b, BX, CELL - len(b))
            f.part(head(eye='happy', mouth_kind='smile', blush=True), HX, HY + 2)
        return f.grid()

    a['happy'] = [happy(i) for i in range(4)]

    # Hanging from a bar along the top edge: hands at row 0, ears flopped back.
    def hang(i, shimmy=False):
        f = F()
        back, front = (8, 0), (23, 0)
        if shimmy:
            back, front = ((7, 1), (25, 0)) if i == 0 else ((9, 0), (22, 1))
        f.limb([(12, 20), back])
        f.limb([(20, 20), front])
        b = TORSO + LEGS['dangle' if i == 0 else 'kick']
        f.part(b, BX, CELL - len(b) - 1)
        f.part(head(ears='down', eye='open', look=1 if shimmy else 0), HX, 1)
        return f.grid()

    a['hang'] = [hang(0), hang(1)]
    a['shimmy'] = [hang(0, True), hang(1, True)]

    a['idle_sad'] = [standing(head(ears='down', eye='droopy', mouth_kind='frown'), head_dy=1 + i).grid()
                     for i in range(2)]

    a['sleep'] = [sit('blink', mouth_kind='none', dy=1 + i, ears='down',
                      extras=[(Z_SMALL, 25, 6)] if i == 0 else [(Z_BIG, 26, 2)]) for i in range(2)]

    # Eating an apple held up to the mouth.
    def eat(i):
        f = standing(head(eye='happy', mouth_kind='munch' if i == 0 else 'smile', blush=True),
                     torso=TORSO, head_dy=1)
        f.part(APPLE if i == 0 else APPLE_BITTEN, 22, 18)
        f.limb([(18, 24), (21, 23)])
        f.limb([(14, 24), (24, 25)])
        return f.grid()

    a['eat'] = [eat(0), eat(1)]

    # Climbing a wall on the right: hands and feet on columns 30-31, alternating.
    def climb(i, blink=False):
        # One arm reaches up past the face while the other grips lower; the feet push off the wall.
        f = F()
        up, low = (30, 12), (30, 22)
        back_hand, front_hand = (up, low) if i == 1 else (low, up)
        back_foot, front_foot = ((30, 26), (30, 29)) if i == 0 else ((30, 29), (30, 26))
        f.limb([(22, 23), back_hand])
        f.limb([(20, 28), back_foot], fill='F')
        f.part(TORSO, BX + 3, 21)
        f.limb([(22, 28), front_foot], fill='F')
        f.part(head(eye='blink' if blink else 'open', look=1), HX + 3, HY)
        f.limb([(23, 23), front_hand])
        return f.grid()

    a['climb'] = [climb(0), climb(1)]
    a['cling'] = [climb(0), climb(0, blink=True)]

    # Pushing: leaning into a window on the right, arms out straight, feet shuffling.
    def push(i):
        b = TORSO + LEGS[['wide', 'stand', 'wide', 'stand'][i]]
        f = F().part(b, BX + 1, CELL - len(b))
        f.part(head(eye='squeeze', mouth_kind='pout', ears='down'), HX + 2, HY + 2 + (i % 2))
        f.limb([(20, 23 + (i % 2)), (30, 21 + (i % 2))])
        f.limb([(19, 25), (30, 25)])
        return f.grid()

    a['push'] = [push(i) for i in range(4)]

    # Sitting on a title bar: seat on row 23, legs dangling below and swinging.
    def ledge(i):
        f = F()
        f.limb([(13, 22), (12 - i, 26)], fill='F', end_size=3)
        f.limb([(18, 22), (19 + i, 26)], fill='F', end_size=3)
        f.part(TORSO_ARMS_DOWN[:7] + ["....KKKKKKKK...."], BX, 16)
        f.part(head(ears='down', eye='happy', mouth_kind='smile'), HX, -4)
        return f.grid()

    a['sit_ledge'] = [ledge(0), ledge(1)]

    # Kicking a ball on the right: foot up, kick out, follow through, back on both feet.
    def kick(i):
        if i == 0:
            return standing(head(look=1), legs='lift').grid()
        if i == 3:
            return standing(head(look=1)).grid()
        b = TORSO_ARMS_DOWN + LEGS['back']
        f = F().part(b, BX, CELL - len(b))
        f.limb([(16, 27), (26, 28) if i == 1 else (25, 25)], fill='F', end_size=3)
        f.part(head(eye='squeeze' if i == 1 else 'happy', mouth_kind='open' if i == 1 else 'smile', look=1), HX, HY)
        return f.grid()

    a['kick'] = [kick(i) for i in range(4)]

    # Sitting with a controller, playing games: thumbs on the buttons, head bobbing.
    def game(i):
        f = F().part(SIT, BX, CELL - len(SIT))
        f.part(head(eye='open', mouth_kind='pout' if i == 0 else 'smile', look=1), HX, 2 + i)
        f.limb([(11, 26), (19, 25)])
        f.limb([(17, 27), (24, 25)])
        f.part(PAD if i == 0 else PAD_PRESSED, 18, 23 + i)
        return f.grid()

    a['game'] = [game(0), game(1)]

    # Bouncing: squashed on landing, then stretched up in the air with arms raised.
    def bounce(i):
        if i == 0:
            return standing(head(eye='happy', mouth_kind='smile', blush=True), legs='wide', head_dy=3).grid()
        f = F()
        f.limb([(13, 22), (1, 14)])
        f.limb([(18, 22), (29, 14)])
        b = TORSO + LEGS['dangle']
        f.part(b, BX, CELL - len(b) - 1)
        f.part(head(ears='tall', eye='happy', mouth_kind='open', blush=True), HX, HY - 1)
        return f.grid()

    a['bounce'] = [bounce(0), bounce(1)]

    # Courting: gazing ahead (at its partner, on the right), blushing, hearts floating up in front of its face.
    def love(i):
        f = standing(head(eye='happy', mouth_kind='smile', blush=True, look=1), head_dy=i)
        if i == 0:
            f.part(HEART, 25, 6).part(HEART_SMALL, 27, 0)
        else:
            f.part(HEART, 25, 3).part(HEART_SMALL, 26, 11)
        return f.grid()

    a['love'] = [love(0), love(1)]
    return a


# ---------------------------------------------------------------- furniture
# Each piece is a strip of frames, drawn with fills and then auto-outlined, at 1 art pixel = 2 DIP (like the rabbit).
# Sizes must match the defaults in Core (DefaultFurniture): width and height in DIP = art pixels x 2.
def canvas(w, h):
    return [['.'] * w for _ in range(h)]


def fill(c, x0, y0, x1, y1, ch):
    """Fills the inclusive rectangle (x0, y0)-(x1, y1)."""
    for y in range(y0, y1 + 1):
        for x in range(x0, x1 + 1):
            if 0 <= y < len(c) and 0 <= x < len(c[0]):
                c[y][x] = ch


def border(c, x0, y0, x1, y1, ch='K'):
    for x in range(x0, x1 + 1):
        c[y0][x] = c[y1][x] = ch
    for y in range(y0, y1 + 1):
        c[y][x0] = c[y][x1] = ch


def outlined(c):
    """Outlines every filled pixel in K (into transparent neighbours only)."""
    h, w = len(c), len(c[0])
    filled = {(x, y) for y in range(h) for x in range(w) if c[y][x] != '.'}
    for (x, y) in filled:
        for (nx, ny) in ((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)):
            if 0 <= nx < w and 0 <= ny < h and (nx, ny) not in filled:
                c[ny][nx] = 'K'
    return c


def ball():
    """8x8, 4 frames: a red ball with a white band rolling round."""
    out = []
    for i in range(4):
        c = canvas(8, 8)
        for (x0, x1, y) in ((2, 5, 1), (1, 6, 2), (1, 6, 3), (1, 6, 4), (1, 6, 5), (2, 5, 6)):
            fill(c, x0, y, x1, y, 'R')
        band = 2 + i
        for y in range(1, 7):
            if c[y][band] == 'R':
                c[y][band] = 'W'
        c[2][2] = 'r' if band != 2 else 'W'
        out.append(outlined(c))
    return out


def tv():
    """24x20, 3 frames: off, then two frames of a little game on screen."""
    out = []
    for i in range(3):
        c = canvas(24, 20)
        fill(c, 1, 1, 22, 15, 'G')   # body
        fill(c, 3, 16, 20, 16, 'o')  # shelf
        fill(c, 5, 17, 6, 19, 'o')   # legs
        fill(c, 17, 17, 18, 19, 'o')
        outlined(c)
        border(c, 2, 2, 21, 13)
        fill(c, 3, 3, 20, 12, 'V' if i == 0 else 'Z')
        if i == 0:
            fill(c, 4, 4, 5, 5, 'h')  # reflection
        else:
            fill(c, 3, 11, 20, 12, 'L')            # ground
            fill(c, 13, 9, 15, 10, 'L')            # a hill
            x = 6 if i == 1 else 9
            fill(c, x, 9 - (i - 1), x + 1, 10 - (i - 1), 'R')  # the hero jumps
            c[5][16] = c[5][17] = 'Y'              # a coin
        c[14][18] = 'L'
        c[14][20] = 'R'
        out.append(c)
    return out


def couch():
    """40x18: back, two cushions, arms, short legs. The seat top is row 9 (18 DIP up)."""
    c = canvas(40, 18)
    fill(c, 3, 1, 36, 8, 'C')     # back
    fill(c, 1, 5, 5, 13, 'C')     # arms
    fill(c, 34, 5, 38, 13, 'C')
    fill(c, 6, 9, 33, 12, 'C')    # seat
    fill(c, 1, 13, 38, 14, 'c')   # base
    fill(c, 3, 15, 4, 17, 'o')    # legs
    fill(c, 35, 15, 36, 17, 'o')
    outlined(c)
    for y in range(2, 8):
        c[y][20] = 'c'            # back cushion seam
    for y in range(9, 13):
        c[y][20] = 'K'            # seat cushion seam
    fill(c, 6, 12, 33, 12, 'c')   # seat shade
    fill(c, 1, 5, 5, 5, 'c')      # arm tops
    fill(c, 34, 5, 38, 5, 'c')
    return [c]


def bed():
    """40x16: headboard, pillow, blanket, footboard. The mattress top is row 7 (18 DIP up)."""
    c = canvas(40, 16)
    fill(c, 1, 1, 4, 15, 'O')     # headboard
    fill(c, 35, 6, 38, 15, 'O')   # footboard
    fill(c, 5, 11, 34, 13, 'o')   # frame
    fill(c, 5, 7, 34, 10, 'S')    # sheet
    fill(c, 15, 6, 34, 10, 'U')   # blanket
    fill(c, 6, 5, 13, 7, 'W')     # pillow
    outlined(c)
    fill(c, 15, 10, 34, 10, 'u')  # blanket shade
    fill(c, 2, 3, 3, 3, 'o')      # headboard detail
    return [c]


def trampoline():
    """32x10: a dark mat on a grey frame with springs and legs. The mat top is row 1 (18 DIP up)."""
    c = canvas(32, 10)
    fill(c, 2, 1, 29, 2, 'V')     # mat
    fill(c, 1, 3, 30, 3, 'G')     # frame
    fill(c, 3, 4, 4, 9, 'G')      # legs
    fill(c, 27, 4, 28, 9, 'G')
    outlined(c)
    for x in range(4, 28, 4):
        c[3][x] = 'w'             # springs
    return [c]


def playpen():
    """56x20: wooden posts and two rails. The inside is transparent (pets stand in it, clicks pass through)."""
    c = canvas(56, 20)
    for x in (1, 10, 19, 27, 36, 45, 53):
        fill(c, x, 1, x + 1, 19, 'O')
    fill(c, 1, 3, 54, 4, 'o')     # top rail
    fill(c, 1, 14, 54, 15, 'o')   # bottom rail
    for x in (1, 10, 19, 27, 36, 45, 53):
        c[1][x] = c[1][x + 1] = 'O'
    return [outlined(c)]


def trough():
    """24x10: a wooden box heaped with greens and apples."""
    c = canvas(24, 10)
    fill(c, 3, 2, 20, 3, 'L')     # greens
    fill(c, 1, 4, 22, 9, 'O')     # box
    for x in (5, 11, 17):
        fill(c, x, 1, x + 1, 2, 'R')  # apples
    outlined(c)
    fill(c, 1, 6, 22, 6, 'o')     # plank line
    c[1][5] = 'r'
    return [c]


def egg():
    """12x13, 4 frames: a speckled egg standing still, then wobbling left and right (its top half shifted)."""
    rows = [(5, 6), (4, 7), (3, 8), (3, 8), (2, 9), (2, 9), (2, 9), (2, 9), (2, 9), (3, 8), (4, 7)]  # rows 1-11
    out = []
    for i in range(4):
        tilt = [0, 0, -1, 1][i]
        c = canvas(12, 13)

        def shift(y):
            return tilt if y <= 5 else 0

        for y, (x0, x1) in enumerate(rows, start=1):
            fill(c, x0 + shift(y), y, x1 + shift(y), y, 'A')
        for y, (x0, x1) in enumerate(rows, start=1):
            if y >= 6:
                c[y][x1] = 'a'                   # shade on the right
        for (x, y) in ((6, 4), (4, 6), (7, 8), (5, 10)):
            c[y][x + shift(y)] = 'a'             # speckles
        outlined(c)
        c[3][5 + tilt] = 'w'                     # shine
        out.append(c)
    return out


FURNITURE = {
    'ball': ball, 'tv': tv, 'couch': couch, 'trampoline': trampoline, 'bed': bed, 'playpen': playpen, 'trough': trough,
    'egg': egg,
}


def render_strip(frames_):
    h, w = len(frames_[0]), len(frames_[0][0])
    img = Image.new('RGBA', (w * len(frames_), h), (0, 0, 0, 0))
    for i, c in enumerate(frames_):
        for y in range(h):
            for x in range(w):
                col = PAL[c[y][x]]
                if col is not None:
                    img.putpixel((i * w + x, y), col + (255,))
    return img


ORDER = ['idle', 'walk', 'sit', 'fall', 'land', 'held', 'happy', 'hang', 'shimmy',
         'idle_sad', 'sleep', 'eat', 'climb', 'cling', 'push', 'sit_ledge', 'kick', 'game', 'bounce', 'love']


def render(anims):
    cols = 16
    sheet = Image.new('RGBA', (cols * CELL, len(ORDER) * CELL), (0, 0, 0, 0))
    for row, name in enumerate(ORDER):
        for col, g in enumerate(anims.get(name, [])):
            for y in range(CELL):
                for x in range(CELL):
                    c = PAL[g[y][x]]
                    if c is not None:
                        sheet.putpixel((col * CELL + x, row * CELL + y), c + (255,))
    return sheet


def preview(img, path, scale=8):
    """An upscaled copy on a white/blue checkerboard (4-pixel squares)."""
    big = img.resize((img.width * scale, img.height * scale), Image.NEAREST)
    bg = Image.new('RGBA', big.size, (255, 255, 255, 255))
    for y in range(0, big.height, scale * 4):
        for x in range(0, big.width, scale * 4):
            if (x // (scale * 4) + y // (scale * 4)) % 2:
                bg.paste((228, 236, 244, 255), (x, y, x + scale * 4, y + scale * 4))
    bg.alpha_composite(big)
    bg.convert('RGB').save(path)


def main():
    app = os.path.join(os.path.dirname(__file__), '..', '..', 'src', 'DesktopPet.App')
    ap = argparse.ArgumentParser()
    ap.add_argument('--out', default=os.path.join(app, 'SpriteSheet', 'rabbit.png'))
    ap.add_argument('--furniture-out', default=os.path.join(app, 'Furniture', 'Art'),
                    help='folder for the furniture PNGs (one per kind)')
    ap.add_argument('--preview', help='also write an 8x upscaled preview of the sheet with a checkerboard')
    ap.add_argument('--furniture-preview', help='also write an 8x preview of all furniture, one strip per row')
    args = ap.parse_args()
    sheet = render(frames())
    sheet.save(args.out)
    if args.preview:
        preview(sheet, args.preview)
    print('wrote', os.path.normpath(args.out), sheet.size)

    os.makedirs(args.furniture_out, exist_ok=True)
    strips = []
    for kind, make in FURNITURE.items():
        strip = render_strip(make())
        strip.save(os.path.join(args.furniture_out, kind + '.png'))
        strips.append(strip)
        print('wrote', os.path.normpath(os.path.join(args.furniture_out, kind + '.png')), strip.size)
    if args.furniture_preview:
        gap = 2
        board = Image.new('RGBA', (max(s.width for s in strips), sum(s.height + gap for s in strips)), (0, 0, 0, 0))
        y = 0
        for s_ in strips:
            board.paste(s_, (0, y))
            y += s_.height + gap
        preview(board, args.furniture_preview)


if __name__ == '__main__':
    sys.exit(main())
