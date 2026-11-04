"""Generates the default rabbit sprite sheet (src/DesktopPet.App/SpriteSheet/rabbit.png) and the default furniture
art (src/DesktopPet.App/Furniture/Art/<kind>.png).

Pixel art is composed from ASCII parts (head, face, body poses, props) so frames stay consistent and the art can be
tweaked in one place. One row per animation in the game's order (Animations.All), 32x32 cells, art facing right,
feet on the bottom row.

    python tools/sprites/make_rabbit.py [--preview out.png] [--furniture-preview out.png]
"""
import argparse
import math
import os
import sys

from PIL import Image

CELL = 64

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
    'S': (0xE2, 0xE0, 0xE6),  # shirt / hoodie drawstrings
    's': (0x9A, 0x8C, 0x74),  # shorts
    'g': (0x7C, 0x70, 0x5C),  # shorts shade
    'E': (0x15, 0x13, 0x19),  # eye
    'e': (0x4A, 0x3E, 0x66),  # iris glint
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
    'D': (0xB4, 0xB2, 0xBC),  # gravestone
    'd': (0x8A, 0x88, 0x94),  # gravestone shade
}


def grid(rows):
    w = max(len(r) for r in rows)
    return [r.ljust(w, '.') for r in rows]


# ---------------------------------------------------------------- drawing
# The rabbit is drawn from shapes rather than typed pixel by pixel: each part (ears, head, hoodie, an arm, a leg) is a
# Layer filled from shape tests (ellipses, polygons, tapered strokes), auto-outlined in K, then painted onto the frame
# back to front. Small details (eyes, mouths, nose) are ASCII stamps.
def ellipse(cx, cy, rx, ry):
    return lambda x, y: ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0


def polygon(pts):
    def inside(x, y):
        hit = False
        for (x0, y0), (x1, y1) in zip(pts, pts[1:] + pts[:1]):
            if (y0 > y) != (y1 > y) and x < x0 + (y - y0) * (x1 - x0) / (y1 - y0):
                hit = not hit
        return hit
    return inside


def stroke(path, radii):
    """A tapered stroke along a polyline: radii[i] at path[i], interpolated along each segment."""
    def inside(x, y):
        for (a, b, ra, rb) in zip(path, path[1:], radii, radii[1:]):
            dx, dy = b[0] - a[0], b[1] - a[1]
            ll = dx * dx + dy * dy
            t = 0.0 if ll == 0 else max(0.0, min(1.0, ((x - a[0]) * dx + (y - a[1]) * dy) / ll))
            px, py = a[0] + t * dx - x, a[1] + t * dy - y
            r = ra + (rb - ra) * t
            if px * px + py * py <= r * r:
                return True
        return False
    return inside


def both(a, b):
    return lambda x, y: a(x, y) and b(x, y)


def minus(a, b):
    return lambda x, y: a(x, y) and not b(x, y)


class Layer:
    """One part: a CELL x CELL grid of palette keys (None = empty)."""

    def __init__(self):
        self.g = [[None] * CELL for _ in range(CELL)]

    def fill(self, shape, ch, only=None):
        """Paints ch where shape holds; with only, just over pixels already holding one of those keys."""
        for y in range(CELL):
            row = self.g[y]
            for x in range(CELL):
                if (only is None or (row[x] is not None and row[x] in only)) and shape(x, y):
                    row[x] = ch
        return self

    def has(self, x, y):
        return 0 <= x < CELL and 0 <= y < CELL and self.g[y][x] is not None

    def outline(self):
        hits = [(x + dx, y + dy) for y in range(CELL) for x in range(CELL) if self.g[y][x] is not None
                for (dx, dy) in ((1, 0), (-1, 0), (0, 1), (0, -1))]
        for (x, y) in hits:
            if 0 <= x < CELL and 0 <= y < CELL and self.g[y][x] is None:
                self.g[y][x] = 'K'
        return self

    def stamp(self, part, x0, y0):
        for y, row in enumerate(part):
            for x, c in enumerate(row):
                if c != '.' and 0 <= x0 + x < CELL and 0 <= y0 + y < CELL:
                    self.g[y0 + y][x0 + x] = c
        return self


class F:
    """One frame being built: layers are painted back to front."""

    def __init__(self):
        self.c = [['.'] * CELL for _ in range(CELL)]

    def add(self, layer):
        for y in range(CELL):
            for x in range(CELL):
                if layer.g[y][x] is not None:
                    self.c[y][x] = layer.g[y][x]
        return self

    def grid(self):
        return self.c


# ---------------------------------------------------------------- head
# Chibi proportions: a big round head (about as tall as the body and legs together), a small pear-shaped body and
# stubby limbs, in a 3/4 view facing right. Standing layout: ears y 2-21, head y 18-42, hoodie y 38-51,
# shorts 48-54, hips at y 53, feet on row 63.
HX, HY = 32, 30  # head centre when standing

# Ears: (base dx, base dy from the head centre, angle in degrees from straight up, length, half width).
EAR_SETS = {
    'up': [(-6, -9, -11, 19, 4.8), (5, -10, 9, 19, 5.0)],      # both up, splayed a little
    'tall': [(-5, -9, -4, 19, 4.6), (4, -10, 3, 19, 4.8)],     # straight up (falling, surprised)
    'down': [(-5, -9, -104, 19, 4.8), (1, -10, -92, 20, 5.0)],  # flopped back behind the head
}


def ear(bx, by, angle, length, hw):
    """A long ear with a pointed tip, a pink inner stripe and a pale tuft at the base."""
    a = math.radians(angle)
    dx, dy = math.sin(a), -math.cos(a)        # along the ear, base to tip
    nx, ny = math.cos(a), math.sin(a)         # across

    def shape(scale_w, t0, t1, shift=0.0):
        def inside(x, y):
            rx, ry = x - bx, y - by
            u, v = (rx * dx + ry * dy) / length, rx * nx + ry * ny - shift
            if not (t0 <= u <= t1):
                return False
            t = (u - t0) / (t1 - t0) * 2 - 1     # -1 at the base, 1 at the tip
            w = hw * scale_w * math.sqrt(max(0.0, 1 - t * t)) * (1 - 0.3 * max(0.0, t))
            return abs(v) <= w
        return inside

    return shape(1.0, -0.2, 1.0), shape(0.45, 0.12, 0.88), shape(0.4, 0.05, 0.3, 0.5)


# Eyes, 5x7: tall dark ovals with a big shine. Top-left anchor; the same stamp for both eyes, except > <.
EYES = {
    'open': grid([".EEE.", "EwwEE", "EwwEE", "EEEEE", "EEEeE", "EEEEE", ".EEE."]),
    'blink': grid([".....", ".....", ".....", ".....", "K...K", ".KKK.", "....."]),
    'happy': grid([".....", ".....", ".KKK.", "K...K", ".....", ".....", "....."]),
    'wide': grid([".KKK.", "KwwwK", "KwEwK", "KwEwK", "KwwwK", ".KKK.", "....."]),
    'squeeze': grid(["K....", ".KK..", "...KK", ".KK..", "K....", ".....", "....."]),  # > (mirrored for the front eye)
    'droopy': grid([".....", ".....", "KKKKK", "EwwEE", "EEEeE", "EEEEE", ".EEE."]),
    'angry': grid(["K....", ".KK..", "EEEKK", "EwwEE", "EEEeE", "EEEEE", ".EEE."]),
}

MOUTHS = {
    'none': grid(["K.K.K", ".K.K."]),      # a small rabbit "w"
    'smile': grid(["K...K", ".KKK."]),
    'open': grid([".KKK.", "KMMMK", "KMTTK", ".KKK."]),
    'shout': grid([".KKKK.", "KMMMMK", "KMMMMK", "KMTTMK", ".KKKK."]),
    'frown': grid([".KKK.", "K...K"]),
    'pout': grid(["KKK"]),
    'munch': grid(["KKK", "KMK", ".K."]),
}
NOSE = grid(["NNN", ".N."])


def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
    """The ears layer (behind) and the head layer, for a head centred at (cx, cy)."""
    e = Layer()
    for (bx, by, ang, ln, hw) in EAR_SETS[ears]:
        outer, inner, tuft = ear(cx + bx, cy + by, ang, ln, hw)
        e.fill(outer, 'F')
        e.fill(minus(outer, lambda x, y, _o=outer: _o(x + 1, y - 1)), 'f')   # shade along the back edge
        e.fill(inner, 'P')
        e.fill(tuft, 'W')
    e.outline()

    h = Layer()
    skull = ellipse(cx, cy, 14.5, 12.5)
    h.fill(skull, 'F')
    h.fill(polygon([(cx - 12, cy + 2), (cx - 17, cy + 5), (cx - 13, cy + 6), (cx - 15.5, cy + 9),
                    (cx - 10, cy + 9)]), 'F')                                   # cheek fluff at the back
    h.fill(polygon([(cx + 11, cy + 6), (cx + 15.5, cy + 8), (cx + 11, cy + 10)]), 'F')   # and at the front
    h.fill(minus(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 2.5, cy - 2.5, 15.5, 13)), 'f')   # shade, lower back
    h.fill(ellipse(cx + 4.5, cy + 7.5, 6, 3.6), 'W')                           # muzzle
    # Three chunky locks swept forward: above the back eye, a big one between the eyes, one at the front temple.
    hair = polygon([(cx - 15, cy - 20), (cx + 17, cy - 20), (cx + 16, cy - 6), (cx + 14, cy - 2), (cx + 10, cy - 7),
                    (cx + 7, cy - 8), (cx + 4, cy - 1), (cx + 1, cy - 7), (cx - 2, cy - 9), (cx - 5, cy - 3),
                    (cx - 8, cy - 8), (cx - 15, cy - 6)])
    h.fill(both(hair, ellipse(cx + 0.3, cy - 0.6, 15.2, 13.2)), 'H')
    h.fill(polygon([(cx - 3, cy - 11), (cx - 1, cy - 17), (cx + 2, cy - 13), (cx + 5, cy - 16), (cx + 5, cy - 11)]),
           'H')                                                                 # cowlick on the crown
    h.fill(minus(ellipse(cx + 2, cy - 8, 9, 3.5), ellipse(cx + 2, cy - 7, 9, 3.5)), 'h', only='H')   # shine
    h.outline()

    sq = EYES[eye]
    back = sq
    front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
    h.stamp(back, cx - 7 + look, cy - 1).stamp(front, cx + 5 + look, cy - 1)
    h.stamp(NOSE, cx + 4 + look, cy + 5)
    m = MOUTHS[mouth_kind]
    h.stamp(m, cx + 5 + look - len(m[0]) // 2, cy + 7)
    if blush:
        h.stamp(["BBB"], cx - 7, cy + 5).stamp(["BBB"], cx + 9, cy + 5)
    return [e, h]


# ---------------------------------------------------------------- body
SHOULDER_BACK, SHOULDER_FRONT = (24, 41), (40, 41)


def hoodie(ux, uy):
    h = Layer()
    o = lambda pts: [(x + ux, y + uy) for (x, y) in pts]
    body = polygon(o([(25, 38), (39, 38), (42, 42), (43.5, 47), (44.5, 51.5), (19.5, 51.5), (20.5, 47), (22, 42)]))
    h.fill(body, 'J')
    h.fill(lambda x, y: body(x, y) and x <= 22 + ux, 'j')                 # back side in shade
    h.fill(lambda x, y: body(x, y) and x >= 40 + ux and y <= 46 + uy, 'k')  # light on the front
    h.fill(lambda x, y: body(x, y) and y >= 49 + uy, 'j')                 # ribbed hem
    for x in range(20 + ux, 45 + ux, 3):
        h.fill(lambda px, py, _x=x: px == _x and body(px, py) and py >= 49 + uy, 'J')
    h.fill(polygon(o([(25, 44.5), (39, 44.5), (40.5, 49), (23.5, 49)])), 'j')  # front pocket
    h.fill(polygon(o([(26, 45.5), (38, 45.5), (39, 49), (25, 49)])), 'J')
    h.fill(ellipse(32.5 + ux, 42.5 + uy, 9.5, 2.6), 'k')                   # the hood rolled round the neck
    h.fill(polygon(o([(28.5, 42), (36.5, 42), (32.5, 46.5)])), 'K')        # dark inside of the hood
    for (x0, x1, y1) in ((30, 29.5, 47), (35, 35.5, 48)):                  # drawstrings with tips
        h.fill(stroke([(x0 + ux, 44 + uy), (x1 + ux, y1 + uy)], [0.5, 0.5]), 'S')
        h.fill(ellipse(x1 + ux, y1 + uy, 0.8, 1.0), 'S')
    h.outline()
    return h


def shorts(ux, uy, sit=False):
    s = Layer()
    if sit:
        pts = [(20, 55), (43, 55), (47, 58), (48, 62.5), (20, 62.5)]
    else:
        pts = [(21, 48), (43, 48), (44.5, 54.5), (33.5, 54.5), (32, 52.5), (30.5, 54.5), (19.5, 54.5)]
    shape = polygon([(x + ux, y + uy) for (x, y) in pts])
    s.fill(shape, 's')
    s.fill(lambda x, y: shape(x, y) and x <= 22 + ux, 'g')
    s.outline()
    return s


def tail(ux, uy):
    t = Layer()
    for (dx, dy, r) in ((0, 0, 2.8), (-2, -1.2, 1.8), (-1.6, 1.6, 1.8), (0.4, -2.3, 1.6)):   # a puff of tufts
        t.fill(ellipse(19 + dx + ux, 50 + dy + uy, r, r), 'W')
    t.outline()
    return t


def arm(path, paw=True):
    """A chubby oversized sleeve along path with a cuff; a paw sticks out only when the arm is raised (paw=True)."""
    a = Layer()
    a.fill(stroke(path, [3.0] * (len(path) - 1) + [3.4]), 'J')
    (x0, y0), (x1, y1) = path[-2], path[-1]
    d = math.hypot(x1 - x0, y1 - y0) or 1
    ux_, uy_ = (x1 - x0) / d, (y1 - y0) / d
    a.fill(stroke([(x1 - 1.2 * ux_, y1 - 1.2 * uy_), (x1 + 0.6 * ux_, y1 + 0.6 * uy_)], [3.6, 3.6]), 'j')   # cuff
    if paw:
        a.fill(ellipse(x1 + 3 * ux_, y1 + 3 * uy_, 2.4, 2.4), 'F')
    a.outline()
    return a


def leg(path, fur='F', radii=(3.8, 3.0, 2.2, 2.2, 1.9)):
    """A stubby digitigrade leg: hip, knee (forward), hock (back, off the ground), ball of the foot, toe; spiky fur
    at the back of the hock."""
    lg = Layer()
    lg.fill(stroke(path, list(radii[:len(path)])), fur)
    hx, hy = path[2]
    lg.fill(polygon([(hx - 0.5, hy - 3), (hx - 4.5, hy - 1.5), (hx - 2, hy - 0.5), (hx - 4, hy + 1),
                     (hx, hy + 1)]), fur)                                         # hock tuft
    lg.outline()
    return lg


def off(path, dx, dy):
    return [(x + dx, y + dy) for (x, y) in path]


# Leg pairs (back, front) in standing coordinates. Feet: ball and toe on y 60.6 (radius 2 fills to row 62, outline 63).
FOOT = 60.6
LEGS = {
    'stand': ([(28, 53), (29.5, 56.5), (27, 59), (30, FOOT), (33, FOOT)],
              [(35, 53), (37, 56.5), (34.5, 59), (38, FOOT), (41, FOOT)]),
    'stride': ([(28, 53), (26, 56.5), (22.5, 58.5), (24.5, FOOT), (27.5, FOOT)],
               [(35, 53), (38.5, 56), (38, 58.5), (41.5, FOOT), (44.5, FOOT)]),
    'stride2': ([(28, 53), (31, 56), (30.5, 58.5), (34, FOOT), (37, FOOT)],
                [(35, 53), (34, 56.5), (30, 58.5), (32, FOOT), (35, FOOT)]),
    'pass': ([(28, 53), (29.5, 56.5), (27, 59), (30, FOOT), (33, FOOT)],
             [(35, 53), (38.5, 55), (36, 57.5), (39.5, 58), (42.5, 58)]),
    'pass2': ([(28, 53), (31.5, 55), (29, 57.5), (32.5, 58), (35.5, 58)],
              [(35, 53), (37, 56.5), (34.5, 59), (38, FOOT), (41, FOOT)]),
    'dangle': ([(28, 53), (28.5, 57), (27.5, 59.5), (29, 61), (30, 61.5)],
               [(35, 53), (35.5, 57), (34.5, 59.5), (36, 61), (37, 61.5)]),
    'kick': ([(28, 53), (26.5, 57), (25, 59.5), (25.5, 61.5), (26, 62)],
             [(35, 53), (38.5, 56), (38, 59), (40.5, 60), (42.5, 60.5)]),
    'crouch': ([(28, 55.5), (31.5, 58), (28, 59.5), (30.5, FOOT), (33.5, FOOT)],
               [(35, 55.5), (39, 58), (35.5, 59.5), (38.5, FOOT), (41.5, FOOT)]),
    'kick_out': ([(28, 53), (29.5, 56.5), (27, 59), (30, FOOT), (33, FOOT)],
                 [(35, 53), (40, 55), (45, 56.5), (49, 56), (52, 55.5)]),
    'kick_up': ([(28, 53), (29.5, 56.5), (27, 59), (30, FOOT), (33, FOOT)],
                [(35, 53), (39.5, 52), (43.5, 51), (46.5, 49), (48.5, 47.5)]),
}
SIT_LEGS = ([(31, 60), (37, 56.5), (40.5, 59.5), (44, FOOT), (47, FOOT)],
            [(35, 60), (42, 56.5), (45.5, 59.5), (49, FOOT), (52, FOOT)])


def rabbit(ux=0, uy=0, hdx=0, hdy=0, ears='up', eye='open', mouth_kind='none', look=0, blush=False,
           arms=None, legs='stand', lx=0, ly=0, sit=False, props_mid=(), props_front=(), front_arm_last=False):
    """A whole rabbit. arms: None (hanging at the sides, hands in the sleeves) or (left path, right path) in cell
    coordinates, with paws showing.

    Depth in the 3/4 view: the pet faces right and toward the viewer, so its NEAR arm is the one on the image's
    left (drawn over the body) and its FAR arm the one on the right (drawn behind the body, mostly hidden).
    front_arm_last=True draws the right arm over everything instead, for holding a prop out in front."""
    f = F()
    paws = arms is not None
    if arms is None:
        bs, fs = off([SHOULDER_BACK], ux, uy)[0], off([SHOULDER_FRONT], ux, uy)[0]
        arms = ([bs, (bs[0] - 2.5, bs[1] + 5), (bs[0] - 3, bs[1] + 8)],
                [fs, (fs[0] + 2.5, fs[1] + 5), (fs[0] + 3, fs[1] + 8)])
    near_arm, far_arm = arms
    if not front_arm_last:
        f.add(arm(far_arm, paws))
    f.add(tail(ux, uy))
    if sit:
        f.add(shorts(ux, uy, sit=True))
        f.add(leg(SIT_LEGS[0], fur='f')).add(leg(SIT_LEGS[1]))
    else:
        back_leg, front_leg = LEGS[legs]
        f.add(leg(off(back_leg, lx, ly), fur='f')).add(leg(off(front_leg, lx, ly)))
        f.add(shorts(ux, uy))
    f.add(hoodie(ux, uy))
    for layer in props_mid:
        f.add(layer)
    f.add(arm(near_arm, paws))                   # over the body, but under the head: the shoulder is below the chin
    for layer in head(HX + ux + hdx, HY + uy + hdy, ears, eye, mouth_kind, look, blush):
        f.add(layer)
    for layer in props_front:
        f.add(layer)
    if front_arm_last:
        f.add(arm(far_arm, paws))
    return f


# ---------------------------------------------------------------- props
def apple(x, y, bitten=False):
    a = Layer()
    body = ellipse(x, y, 4.6, 4.2)
    if bitten:
        body = minus(body, ellipse(x - 5, y - 1, 3, 3.2))
    a.fill(body, 'R')
    a.fill(both(body, ellipse(x + 2, y - 2, 1.3, 1.3)), 'r')
    a.fill(stroke([(x, y - 4), (x + 1, y - 6)], [0.5, 0.5]), 'o')
    a.fill(ellipse(x + 3, y - 6, 2, 1), 'L')
    return a.outline()


def heart(x, y, r):
    h = Layer()
    h.fill(ellipse(x - r, y, r, r), 'Q').fill(ellipse(x + r, y, r, r), 'Q')
    h.fill(polygon([(x - 2 * r, y + 0.4), (x + 2 * r, y + 0.4), (x, y + 2.4 * r)]), 'Q')
    h.fill(ellipse(x - r - 0.5, y - 0.5, r * 0.4, r * 0.4), 'q')
    return h.outline()


def pad(x, y, pressed):
    p = Layer()
    p.fill(stroke([(x - 5, y), (x + 5, y)], [2.3, 2.3]), 'G')
    p.stamp(['R' if not pressed else 'L'], x + 3, y - 1).stamp(['L' if not pressed else 'R'], x + 4, y)
    p.stamp(['.V.', 'VVV', '.V.'], x - 5, y - 1)                           # d-pad
    return p.outline()


def zee(x, y, big):
    z = Layer()
    s = 7 if big else 5
    z.fill(stroke([(x, y), (x + s, y), (x, y + s), (x + s, y + s)], [0.7] * 4), 'Z')
    return z


# ---------------------------------------------------------------- frames
def frames():
    """Animation id -> list of CELL x CELL char grids."""
    a = {}
    a['idle'] = [rabbit(hdy=1 if (i // 4) % 2 else 0, eye='blink' if i == 13 else 'open').grid() for i in range(16)]

    # Contact, then the trailing foot swings forward (lifted), contact with the other foot ahead, and so on.
    a['walk'] = [rabbit(legs=l, hdy=dy, look=1).grid()
                 for (l, dy) in (('stride', 0), ('pass2', -1), ('stride2', 0), ('pass', -1))]

    a['sit'] = [rabbit(uy=5, sit=True, eye='happy', mouth_kind='smile', hdy=dy).grid() for dy in (-1, 0)]

    def fall(i):
        lift = 0 if i == 0 else 3
        return rabbit(ears='tall', eye='wide', mouth_kind='open', legs='dangle' if i == 0 else 'kick',
                      arms=([(24, 42), (15, 37 + lift), (10, 33 + lift)], [(40, 42), (49, 37 + lift), (54, 33 + lift)]))

    a['fall'] = [fall(0).grid(), fall(1).grid()]

    a['land'] = [rabbit(uy=dy, legs='crouch' if dy >= 2 else 'stand', hdy=hdy, ears=e, eye=ey).grid()
                 for (dy, hdy, e, ey) in ((3, 3, 'down', 'squeeze'), (2, 2, 'down', 'squeeze'), (0, 1, 'up', 'open'),
                                          (0, 0, 'up', 'open'))]

    def held(i):
        b, fr = (((15, 36), (10, 31)), ((49, 46), (53, 50))) if i == 0 else (((15, 46), (11, 50)), ((49, 36), (54, 31)))
        return rabbit(ears='tall', eye='squeeze', mouth_kind='shout', legs='kick' if i == 0 else 'dangle',
                      arms=([(24, 42), *b], [(40, 42), *fr]))

    a['held'] = [held(0).grid(), held(1).grid()]

    def happy(i):
        if i % 2 == 1:
            return rabbit(uy=-3, ly=-3, legs='dangle', eye='happy', mouth_kind='open', blush=True, hdy=2,
                          arms=([(24, 39), (17, 32), (13, 28)], [(40, 39), (47, 32), (51, 28)]))
        return rabbit(eye='happy', mouth_kind='smile', blush=True, hdy=1)

    a['happy'] = [happy(i).grid() for i in range(4)]

    def hang(i, shimmy=False):
        back, front = (16, 1), (48, 1)
        if shimmy:
            back, front = ((14, 2), (51, 1)) if i == 0 else ((18, 1), (46, 2))
        return rabbit(uy=-2, ly=-2, legs='dangle' if i == 0 else 'kick', ears='down', look=1 if shimmy else 0,
                      arms=([(25, 40), (19, 21), back], [(39, 40), (45, 21), front]))

    a['hang'] = [hang(0).grid(), hang(1).grid()]
    a['shimmy'] = [hang(0, True).grid(), hang(1, True).grid()]

    a['idle_sad'] = [rabbit(ears='down', eye='droopy', mouth_kind='frown', hdy=2 + i).grid() for i in range(2)]

    a['sleep'] = [rabbit(uy=5, sit=True, eye='blink', mouth_kind='none', ears='down', hdy=1 + i,
                         props_front=[zee(50, 14, False) if i == 0 else zee(52, 6, True)]).grid() for i in range(2)]

    def eat(i):
        return rabbit(eye='happy', mouth_kind='munch' if i == 0 else 'smile', blush=True, hdy=1,
                      arms=([(25, 44), (35, 48), (42, 46)], [(40, 45), (45, 49), (47, 46)]),
                      props_front=[apple(47, 41, bitten=i == 1)], front_arm_last=True)

    a['eat'] = [eat(0).grid(), eat(1).grid()]

    # Climbing a wall on the right. The body is pressed up against the wall so that limbs of their normal length reach
    # it (the paws and the raised foot touch column 63); the head is turned back a little so the raised arm doesn't
    # cover the face. Diagonal pairs alternate: back hand and front foot up, then front hand and back foot up.
    def climb(i, blink=False):
        u = 15                                     # hoodie x 34-60
        up = [(49, 40), (54, 35), (57.5, 30)]      # shoulder, elbow, cuff: about the length of a hanging arm
        low = [(50, 43), (54, 46), (57.5, 46.5)]
        foot_up = [(50, 53), (54.5, 54.5), (54, 57.5), (58, 57.5), (61, 56)]     # same joint spacing as walking
        foot_low = [(50, 53), (54, 55.5), (53.5, 59), (57.5, FOOT), (60.5, 59.5)]
        back_arm, front_arm = (up, low) if i == 0 else (low, up)
        back_leg, front_leg = (off(foot_low, -4, 0), foot_up) if i == 0 else (off(foot_up, -4, 0), foot_low)
        f = F()
        f.add(arm(back_arm))
        f.add(leg(back_leg, fur='f'))
        f.add(tail(u, 0)).add(shorts(u, 0)).add(hoodie(u, 0))
        f.add(leg(front_leg))
        for layer in head(HX + u - 5, HY, 'up', 'blink' if blink else 'open', 'none', 1):
            f.add(layer)
        f.add(arm(front_arm))
        return f.grid()

    a['climb'] = [climb(0), climb(1)]
    a['cling'] = [climb(0), climb(0, blink=True)]

    def push(i):
        return rabbit(ux=4, eye='squeeze', mouth_kind='pout', ears='down', hdx=1, hdy=2 + i % 2,
                      legs='stride' if i % 2 == 0 else 'stride2', lx=2,
                      arms=([(42, 43), (51, 42 + i % 2), (59, 41 + i % 2)], [(43, 47), (51, 48), (59, 47)]))

    a['push'] = [push(i).grid() for i in range(4)]

    def ledge(i):
        u = -9                                    # the shorts' bottom outline (the seat) lands on row 46
        sw = (0, 2)[i]
        f = F()
        f.add(arm([(40, 41 + u), (42.5, 46 + u), (43, 49 + u)], paw=False))   # far arm, behind
        f.add(tail(0, u))
        f.add(leg([(28, 44), (32, 46.5), (31 - sw, 51), (33 - sw, 54.5), (35 - sw, 55.5)], fur='f'))
        f.add(leg([(35, 44), (39, 46.5), (39 + sw, 51), (41 + sw, 54.5), (43 + sw, 55.5)]))
        f.add(shorts(0, u)).add(hoodie(0, u))
        f.add(arm([(24, 41 + u), (21.5, 46 + u), (21, 49 + u)], paw=False))   # near arm, over the body
        for layer in head(HX, HY + u, 'down', 'happy', 'smile'):
            f.add(layer)
        return f.grid()

    a['sit_ledge'] = [ledge(0), ledge(1)]

    a['kick'] = [rabbit(legs='pass', look=1).grid(),
                 rabbit(legs='kick_out', eye='squeeze', mouth_kind='open', look=1).grid(),
                 rabbit(legs='kick_up', eye='happy', mouth_kind='smile', look=1).grid(),
                 rabbit(look=1).grid()]

    def game(i):
        return rabbit(uy=5, sit=True, eye='open', mouth_kind='pout' if i == 0 else 'smile', look=1, hdy=i,
                      arms=([(25, 47), (35, 52), (40, 51)], [(40, 48), (45, 53), (47, 51)]),
                      props_front=[pad(45, 54 + i, i == 1)], front_arm_last=True).grid()

    a['game'] = [game(0), game(1)]

    a['bounce'] = [rabbit(uy=3, legs='crouch', eye='happy', mouth_kind='smile', blush=True, hdy=2).grid(),
                   rabbit(uy=-3, ly=-3, legs='dangle', ears='tall', eye='happy', mouth_kind='open', blush=True, hdy=2,
                          arms=([(24, 39), (13, 31), (8, 27)], [(40, 39), (51, 31), (56, 27)])).grid()]

    a['love'] = [rabbit(eye='happy', mouth_kind='smile', blush=True, look=1, hdy=i,
                        props_front=[heart(54, 16 - 4 * i, 2.6), heart(58, 6 + 8 * i, 1.6)]).grid()
                 for i in range(2)]
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


def pill():
    """10x6: a blue breeding pill, a capsule in two shades of blue with a shine."""
    c = canvas(10, 6)
    fill(c, 2, 1, 7, 1, 'U')
    fill(c, 1, 2, 8, 3, 'U')
    fill(c, 2, 4, 7, 4, 'U')
    for y in range(1, 5):
        for x in range(5, 9):
            if c[y][x] == 'U':
                c[y][x] = 'Z'           # the lighter half
    outlined(c)
    fill(c, 2, 4, 4, 4, 'u')            # shade underneath
    fill(c, 5, 4, 7, 4, 'U')
    fill(c, 2, 2, 3, 2, 'w')            # shine
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


def grave():
    """32x30: a rounded headstone on a plinth and a grassy mound, a flower each side. The face (x 7-24, y 4-22) is
    left plain: the app writes the pet's name and dates there (GraveArt), so keep the two in step."""
    c = canvas(32, 30)
    for y, (x0, x1) in enumerate([(11, 20), (8, 23), (7, 24), (6, 25)], start=1):
        fill(c, x0, y, x1, y, 'D')   # rounded top
    fill(c, 5, 5, 26, 23, 'D')       # stone
    fill(c, 3, 24, 28, 25, 'd')      # plinth
    fill(c, 1, 26, 30, 28, 'L')      # grass mound
    for x in (2, 29):
        fill(c, x, 22, x, 25, 'L')   # flower stems
        fill(c, x - 1, 20, x + 1, 20, 'Q')
        fill(c, x, 19, x, 21, 'Q')
        c[20][x] = 'Y'
    outlined(c)
    for y in range(5, 24):
        c[y][26] = 'd'               # shade on the right
    for (x, y) in ((25, 4), (24, 3), (23, 2)):
        c[y][x] = 'd'
    fill(c, 3, 24, 28, 24, 'D')      # the plinth's top catches the light
    return [c]


FURNITURE = {
    'ball': ball, 'tv': tv, 'couch': couch, 'trampoline': trampoline, 'bed': bed, 'playpen': playpen, 'trough': trough, 'pill': pill,
    'egg': egg, 'grave': grave,
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
