"""Crime Jr.: a little bear-cub-like thief (the user's reference art, by Devesa illustrations).

Defining features: a brown hood that covers the whole head like a robber's mask, with round bear ears and a pointed
tip on the crown; a pale face opening across the eyes, big white eyes with dark pupils; a toothy sly grin with a
gold coin clamped in the mouth; a ragged brown hood hem round the neck; a short white bell-shaped body with a dark
grey band and a jagged hem, a second gold coin on the band; thin pale grey arms with round hands; stubby dark grey
legs under the hem.
Won't fit at 64 px: the symbol stamped on the coins (only a dark mark), the single big eye of the 3/4 reference
(the rig's face has two eyes), the soft shading on the hood.

    python tools/sprites/make_character.py crimejr
"""


def apply(base):
    Layer, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.ellipse, base.polygon, base.stroke,
                                                          base.both, base.minus, base.math)
    base.PAL.update({
        'F': (0x8C, 0x76, 0x6A),   # hood: greyish brown
        'f': (0x6A, 0x56, 0x4E),   # hood shade
        'h': (0xA8, 0x94, 0x86),   # hood light
        'W': (0xD8, 0xD4, 0xE2),   # pale face (lavender white)
        'p': (0xB4, 0xAE, 0xC2),   # pale face shade
        'S': (0xFA, 0xF8, 0xFC),   # teeth
        'J': (0xF2, 0xF0, 0xF6),   # body: white
        'j': (0xC6, 0xC2, 0xD0),   # body shade
        'k': (0xFF, 0xFF, 0xFF),   # body light
        'n': (0x50, 0x4E, 0x62),   # dark grey band
        'I': (0xC8, 0xC4, 0xD4),   # arms: pale grey
        'i': (0xA0, 0x9C, 0xB0),   # arm shade
        's': (0x56, 0x54, 0x66),   # legs, underside: dark grey
        'g': (0x3E, 0x3C, 0x4C),   # leg shade (far leg)
        'Y': (0xEC, 0xB8, 0x3E),   # gold coin
        'y': (0xB0, 0x80, 0x26),   # coin rim / mark
        'A': (0xFF, 0xE6, 0x96),   # coin shine
    })
    base.SHOULDER_BACK, base.SHOULDER_FRONT = (25, 42), (39, 42)

    G = base.grid
    # White eyes with a dark pupil looking forward (5x7).
    base.EYES.update({
        'open': G([".KKK.", "KwwwK", "KwwEK", "KwEEK", "KwEEK", "KwwwK", ".KKK."]),
        'wide': G([".KKK.", "KwwwK", "KwwwK", "KwEwK", "KwwwK", "KwwwK", ".KKK."]),
        'droopy': G([".....", ".....", "KKKKK", "KwEEK", "KwEEK", "KwwwK", ".KKK."]),
        'angry': G(["K....", ".KK..", "KKKKK", "KwwEK", "KwEEK", "KwwwK", ".KKK."]),
    })
    base.MOUTHS.update({
        'none': G(["K.......", ".KSSSSSK", "..KSKSKK", "...KKK.."]),       # toothy sly grin, up toward the coin
        'smile': G(["K.......K", ".KSSSSSK.", "..KSKSK..", "...KKK..."]),
        'frown': G([".KKKKK.", "KSSKSSK", "K.....K"]),
        'pout': G(["KKKK"]),
    })

    # Round bear ears: (dx, dy of the centre from the head centre, rx, ry).
    EARS = {
        'up': [(-10, -9, 4.4, 4.4), (8, -11, 4.4, 4.4)],
        'tall': [(-9, -10.5, 4.2, 4.8), (7, -12, 4.2, 4.8)],
        'down': [(-12.5, -6.5, 4.4, 3.4), (4, -12, 4.6, 3.2)],
    }
    # The hood's pointed tip on the crown: base x offset, tip (dx, dy).
    TIP = {
        'up': (-1, (-6, -24)),
        'tall': (-1, (-3, -25)),
        'down': (-2, (-13, -21)),
    }

    def coin(L, x, y, r):
        L.fill(ellipse(x, y, r, r), 'Y')
        L.fill(minus(ellipse(x, y, r, r), ellipse(x - 0.7, y - 0.7, r, r)), 'y')
        L.fill(ellipse(x - r * 0.4, y - r * 0.45, r * 0.3, r * 0.3), 'A')
        L.fill(stroke([(x + 0.2, y - r * 0.45), (x + 0.2, y + r * 0.45)], [0.4, 0.4]), 'y')

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        bx, (tx, ty) = TIP[ears]
        tip = polygon([(cx + bx - 5, cy - 10), (cx + tx, cy + ty), (cx + bx + 4, cy - 10)])
        e.fill(tip, 'F')
        e.fill(both(tip, lambda x, y: x <= cx + bx + (tx - bx) * (cy - 10 - y) / (cy - 10 - (cy + ty)) - 0.5), 'f')
        for (dx, dy, rx, ry) in EARS[ears]:
            ox, oy = cx + dx, cy + dy
            e.fill(ellipse(ox, oy, rx, ry), 'F')
            e.fill(minus(ellipse(ox, oy, rx, ry), ellipse(ox + 1, oy - 1, rx, ry)), 'f')
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 14.5, 12.5)
        h.fill(skull, 'F')
        h.fill(minus(skull, ellipse(cx + 2.5, cy - 2.5, 15.5, 13)), 'f')                    # shade, lower back
        h.fill(both(skull, minus(ellipse(cx + 1, cy - 8, 9, 3.5), ellipse(cx + 1, cy - 7, 9, 3.5))), 'h')
        # The pale face opening across the eyes, like the eye hole of a mask.
        hole = ellipse(cx + 3 + look, cy + 1.5, 12, 5)
        h.fill(both(skull, hole), 'W')
        h.fill(both(both(skull, hole), lambda x, y: y >= cy + 5), 'p')
        h.outline()
        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        h.stamp(sq, cx - 6 + look, cy - 1).stamp(front, cx + 5 + look, cy - 1)
        m = base.MOUTHS[mouth_kind]
        mx, my = cx + 5 + look - len(m[0]) // 2, cy + 7
        h.stamp(m, mx, my)
        if blush:
            h.stamp(["BB"], cx - 8 + look, cy + 5).stamp(["BB"], cx + 11 + look, cy + 5)
        c = Layer()
        if mouth_kind in ('none', 'smile', 'frown', 'pout'):
            coin(c, cx + 11.5 + look, cy + 8.5, 3.2)                                              # coin in the mouth
        c.outline()
        return [e, h, c]

    def hoodie(ux, uy):
        """The white bell-shaped body with a dark band, a jagged hem, a coin, and the hood's ragged hem round the
        neck."""
        t = Layer()
        o = lambda pts: [(x + ux, y + uy) for (x, y) in pts]
        hem = [(45, 51), (43, 53.5), (41, 51.5), (38.5, 54), (36, 51.5), (33, 54), (30.5, 51.5), (28, 54),
               (25.5, 51.5), (23, 53.5), (21, 51), (19.5, 52.5)]
        body = polygon(o([(26.5, 38), (37.5, 38), (40.5, 42), (43, 47), (45.5, 50.5)] + hem +
                         [(19.5, 50), (21.5, 46), (23.5, 42)]))
        t.fill(body, 'J')
        t.fill(lambda x, y: body(x, y) and x <= 23.5 + ux + (y - 46 - uy) * -0.2, 'j')         # back in shade
        t.fill(lambda x, y: body(x, y) and 47.5 + uy <= y <= 49 + uy, 'n')                    # dark band
        t.fill(lambda x, y: body(x, y) and x >= 39 + ux and 40 + uy <= y <= 43.5 + uy, 'k')
        t.outline()
        coin(t, 37.5 + ux, 48 + uy, 2.6)
        t.outline()
        # The ragged hood hem round the neck, under the chin.
        c = Layer()
        c.fill(polygon(o([(23.5, 39), (40.5, 39), (40, 41.5), (38.5, 41), (37, 43), (35.5, 41.5), (33.5, 43),
                          (31.5, 41.5), (29.5, 43), (27.5, 41.5), (25.5, 42.5), (24, 41.5)])), 'F')
        c.fill(lambda x, y: c.g[y][x] == 'F' and x <= 26 + ux, 'f')
        c.outline()
        for y in range(base.CELL):
            for x in range(base.CELL):
                if c.g[y][x] is not None:
                    t.g[y][x] = c.g[y][x]
        return t

    def shorts(ux, uy, sit=False):
        """The dark grey underside showing below the jagged hem."""
        s = Layer()
        if sit:
            pts = [(20, 55), (43, 55), (47, 58), (48, 62.5), (20, 62.5)]
        else:
            pts = [(22, 48), (42, 48), (43.5, 54.5), (20.5, 54.5)]
        shape = polygon([(x + ux, y + uy) for (x, y) in pts])
        s.fill(shape, 's')
        s.fill(lambda x, y: shape(x, y) and x <= 23 + ux, 'g')
        s.outline()
        return s

    def tail(ux, uy):
        return Layer()

    def leg(path, fur='F', radii=(3.4, 3.0, 2.6, 2.4, 2.1)):
        """A stubby dark grey leg."""
        lg = Layer()
        c = 'g' if fur == 'f' else 's'
        lg.fill(stroke(path, list(radii[:len(path)])), c)
        lg.outline()
        return lg

    def arm(path, paw=True):
        """A thin pale grey arm ending in a round hand (the hands always show)."""
        a = Layer()
        n = len(path)
        a.fill(stroke(path, [1.7] * n), 'I')
        (x0, y0), (x1, y1) = path[-2], path[-1]
        d = math.hypot(x1 - x0, y1 - y0) or 1
        dx, dy = (x1 - x0) / d, (y1 - y0) / d
        hx, hy = x1 + 1.6 * dx, y1 + 1.6 * dy
        a.fill(ellipse(hx, hy, 2.7, 2.7), 'I')
        a.fill(minus(ellipse(hx, hy, 2.7, 2.7), ellipse(hx + 0.8, hy - 0.8, 2.7, 2.7)), 'i')
        a.outline()
        return a

    base.head = head
    base.hoodie = hoodie
    base.shorts = shorts
    base.leg = leg
    base.tail = tail
    base.arm = arm
