"""Kyra: a lioness in designer clothes (the user's OC sheet).

Defining features: round lion ears, golden fur with a cream muzzle, messy side-swept brown hair that falls past the
shoulders like a mane, a smug smirk with lashes; a cropped jacket with a big fluffy fur collar ("like a male lion's
mane") open over a black crop top, a belt with a heart buckle, wide-leg ripped pants and platform shoes; a long
tail with a dark tuft. Broad shoulders over narrow hips. The sketch is greyscale: the colours are guesses.

    python tools/sprites/make_character.py kyra
"""


def apply(base):
    Layer, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.ellipse, base.polygon, base.stroke,
                                                          base.both, base.minus, base.math)
    base.PAL.update({
        'F': (0xE4, 0xB8, 0x72),   # fur: golden
        'f': (0xC0, 0x8E, 0x4E),   # fur shade
        'W': (0xF7, 0xE7, 0xC6),   # muzzle
        'H': (0x8E, 0x52, 0x2A),   # hair, tail tuft: warm brown
        'h': (0xB8, 0x74, 0x3E),   # hair shine
        'P': (0xF2, 0xD2, 0xAE),   # inner ear
        'N': (0x9A, 0x58, 0x4C),   # nose
        'J': (0x7C, 0x2C, 0x4C),   # jacket: plum leather
        'j': (0x58, 0x1C, 0x36),
        'k': (0xA4, 0x46, 0x6C),
        'I': (0xF5, 0xEC, 0xDC),   # fur collar
        'i': (0xD6, 0xC4, 0xA6),   # fur collar shade
        'n': (0x3C, 0x34, 0x44),   # crop top, belt, shoes
        'Y': (0xF2, 0xC8, 0x4A),   # gold buckle
        's': (0xA6, 0xBE, 0xDC),   # pants: light denim
        'g': (0x7E, 0x96, 0xBA),   # pants shade
    })
    base.SHOULDER_BACK, base.SHOULDER_FRONT = (23, 41), (41, 41)

    # Round ears: (dx, dy of the centre from the head centre, rx, ry).
    ears_by_pose = {
        'up': [(-9, -11, 4.6, 4.6), (7, -12.5, 4.6, 4.6)],
        'tall': [(-8.5, -12, 4.4, 4.9), (6.5, -13.5, 4.4, 4.9)],
        'down': [(-12, -8.5, 4.6, 3.6), (3, -12.5, 4.8, 3.4)],   # flattened back
    }
    base.EAR_SETS = ears_by_pose
    base.MOUTHS['none'] = base.grid(["....K", "KKKK."])          # smirk
    base.MOUTHS['smile'] = base.grid(["K...K", ".KKK."])
    base.NOSE = base.grid(["NNN", ".N."])

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        # Long hair behind the head, falling past the shoulders in mane-like locks.
        e.fill(polygon([(cx - 14, cy - 8), (cx - 6, cy - 12), (cx + 6, cy - 12), (cx + 14, cy - 4),
                        (cx + 15, cy + 6), (cx + 13, cy + 13), (cx + 10, cy + 9), (cx + 6, cy + 12),
                        (cx - 2, cy + 12), (cx - 8, cy + 15), (cx - 11, cy + 11), (cx - 16, cy + 14),
                        (cx - 16.5, cy + 6), (cx - 19, cy + 9), (cx - 17, cy - 2)]), 'H')
        e.fill(lambda x, y: e.g[y][x] == 'H' and x < cx - 13 and y > cy + 4, 'h')
        for (dx, dy, rx, ry) in ears_by_pose[ears]:
            ox, oy = cx + dx, cy + dy
            e.fill(ellipse(ox, oy, rx, ry), 'F')
            e.fill(minus(ellipse(ox, oy, rx, ry), ellipse(ox + 1, oy - 1, rx, ry)), 'f')
            e.fill(ellipse(ox + 0.8, oy + 0.6, rx * 0.5, ry * 0.5), 'P')
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 14.5, 12.5)
        h.fill(skull, 'F')
        h.fill(polygon([(cx + 11, cy + 6), (cx + 15.5, cy + 8), (cx + 11, cy + 10)]), 'F')   # cheek tuft
        h.fill(minus(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 2.5, cy - 2.5, 15.5, 13)), 'f')
        h.fill(ellipse(cx + 4.5, cy + 7.5, 6.5, 3.8), 'W')                                     # muzzle
        # Messy fringe swept toward the front, with a long lock over the back temple and one at the front cheek.
        hair = polygon([(cx - 16, cy - 20), (cx + 17, cy - 20), (cx + 16, cy - 4), (cx + 13, cy + 1),
                        (cx + 11, cy - 6), (cx + 8, cy - 8), (cx + 5, cy - 2), (cx + 2, cy - 8),
                        (cx - 1, cy - 7), (cx - 3, cy - 2), (cx - 6, cy - 8), (cx - 10, cy - 5),
                        (cx - 13, cy + 6), (cx - 16, cy + 3)])
        h.fill(both(hair, ellipse(cx + 0.3, cy - 0.6, 15.4, 13.4)), 'H')
        h.fill(polygon([(cx + 13, cy - 3), (cx + 16, cy - 2), (cx + 15.5, cy + 7), (cx + 13.5, cy + 4)]), 'H')
        h.fill(polygon([(cx - 4, cy - 11), (cx - 3, cy - 18), (cx + 1, cy - 13), (cx + 4, cy - 17),
                        (cx + 5, cy - 11)]), 'H')                                              # tuft on the crown
        h.fill(minus(ellipse(cx + 2, cy - 8, 9, 3.5), ellipse(cx + 2, cy - 7, 9, 3.5)), 'h', only='H')
        h.outline()

        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        bx, fx = cx - 7 + look, cx + 5 + look
        h.stamp(sq, bx, cy - 1).stamp(front, fx, cy - 1)
        if eye in ('open', 'droopy'):                     # lashes flicking out at the outer corners
            h.stamp(["K"], bx - 1, cy - 1 + (2 if eye == 'droopy' else 0))
            h.stamp(["K"], fx + 5, cy - 1 + (2 if eye == 'droopy' else 0))
        h.stamp(base.NOSE, cx + 4 + look, cy + 5)
        m = base.MOUTHS[mouth_kind]
        h.stamp(m, cx + 5 + look - len(m[0]) // 2, cy + 7)
        if blush:
            h.stamp(["BBB"], cx - 8, cy + 5).stamp(["BBB"], cx + 9, cy + 4)
        return [e, collar(cx, cy), h]

    def collar(cx, cy):
        """The jacket's fur collar: a fluffy ruff round the neck, over the shoulders and arms, under the chin."""
        c = Layer()
        y0 = cy + 11
        # Spiky fur: deep tufts over the shoulders, shallow in front where the jacket opens under the chin.
        lower = []
        for i in range(12):
            x = 12 - i * 2.4
            depth = 1.2 + min(2.6, abs(x - 2.5) * 0.26)
            lower += [(x, depth + (1.4 if i % 2 == 0 else 0))]
        pts = ([(-13, -2.5), (13, -2.5), (16.5, 0), (14, 1), (16, 2.8), (12.5, 2.8)] + lower +
               [(-14, 3.6), (-17, 2.8), (-14.5, 1.2), (-16.5, 0)])
        c.fill(polygon([(cx + x, y0 + y) for (x, y) in pts]), 'I')
        c.fill(lambda x, y: c.g[y][x] == 'I' and (x <= cx - 9 or y >= y0 + 3), 'i')
        c.outline()
        return c

    def jacket(ux, uy):
        """Cropped jacket with broad shoulders, open over a black crop top, and a big fur collar."""
        j = Layer()
        o = lambda pts: [(x + ux, y + uy) for (x, y) in pts]
        body = polygon(o([(24, 38), (40, 38), (44.5, 41), (45, 44.5), (42.5, 49.5), (22, 49.5), (19, 44.5),
                          (19.5, 41)]))
        j.fill(body, 'J')
        j.fill(lambda x, y: body(x, y) and x <= 21.5 + ux, 'j')
        j.fill(lambda x, y: body(x, y) and x >= 42 + ux and y <= 46 + uy, 'k')
        j.fill(polygon(o([(29, 40), (38, 40), (37, 47.5), (30, 47.5)])), 'n')        # crop top
        j.fill(polygon(o([(29, 47.5), (37.2, 47.5), (37, 49.5), (29.5, 49.5)])), 'F')   # midriff
        j.fill(polygon(o([(28.5, 41), (30.5, 41), (31, 46), (29.5, 47)])), 'k')       # lapels
        j.fill(polygon(o([(36.5, 41), (38.5, 41), (37.5, 47), (36.5, 46)])), 'j')
        j.outline()
        return j

    CELL = base.CELL

    def pants(ux, uy, sit=False):
        s = Layer()
        if sit:
            pts = [(20, 55), (43, 55), (47, 58), (48, 62.5), (20, 62.5)]
        else:
            pts = [(21.5, 48.5), (42.5, 48.5), (44.5, 54.5), (33.5, 54.5), (32, 52.5), (30.5, 54.5), (19.5, 54.5)]
        top = 55 if sit else 48.5
        shape = polygon([(x + ux, y + uy) for (x, y) in pts])
        s.fill(shape, 's')
        s.fill(lambda x, y: shape(x, y) and x <= 22.5 + ux, 'g')
        s.fill(lambda x, y: shape(x, y) and y <= top + 1.5 + uy, 'n')                 # belt
        s.stamp(["Y.Y", "YYY", ".Y."], 34 + ux, int(top + uy))                         # heart buckle
        s.outline()
        return s

    def leg(path, fur='F', radii=None):
        """A wide-leg pant leg (flaring at the bottom) over a platform shoe."""
        cloth = 'g' if fur == 'f' else 's'
        lg = Layer()
        (hx, hy), (kx, ky), (ax, ay), (bx, by), (tx, ty) = path
        shoe = stroke([(ax + 0.5, ay + 0.8), (bx, by), (tx, ty)], [2.0, 2.2, 1.9])
        lg.fill(shoe, 'n')
        lg.outline()
        p = Layer()
        p.fill(stroke([(hx, hy), (kx, ky), (ax, ay)], [3.8, 3.3, 3.6]), cloth)
        if cloth == 's':
            p.stamp(["F", "F"], int(kx), int(ky) - 1)                                   # rip at the knee
        p.outline()
        for y in range(CELL):
            for x in range(CELL):
                if p.g[y][x] is not None and not (p.g[y][x] == 'K' and lg.g[y][x] not in (None, 'K')):
                    lg.g[y][x] = p.g[y][x]
        return lg

    def tail(ux, uy):
        """A long tail curving down from the back hip, out and up, ending in a dark tuft."""
        t = Layer()
        path = [(21, 50), (16, 54), (11, 54.5), (7.5, 51.5), (6.5, 46.5), (8, 42.5)]
        t.fill(stroke([(x + ux, y + uy) for (x, y) in path], [1.8, 1.6, 1.5, 1.4, 1.3, 1.3]), 'F')
        t.fill(ellipse(8.6 + ux, 40.5 + uy, 2.5, 3.0), 'H')
        t.fill(polygon([(7 + ux, 39 + uy), (8.5 + ux, 35.5 + uy), (10.5 + ux, 39.5 + uy)]), 'H')
        t.outline()
        return t

    def arm(path, paw=True):
        """A jacket sleeve with a darker cuff; Kyra's fists always show."""
        a = Layer()
        a.fill(stroke(path, [3.0] * (len(path) - 1) + [3.2]), 'J')
        (x0, y0), (x1, y1) = path[-2], path[-1]
        d = math.hypot(x1 - x0, y1 - y0) or 1
        dx, dy = (x1 - x0) / d, (y1 - y0) / d
        a.fill(stroke([(x1 - 1.2 * dx, y1 - 1.2 * dy), (x1 + 0.6 * dx, y1 + 0.6 * dy)], [3.3, 3.3]), 'j')
        a.fill(ellipse(x1 + 2.8 * dx, y1 + 2.8 * dy, 2.5, 2.5), 'F')
        a.outline()
        return a

    base.head = head
    base.hoodie = jacket
    base.shorts = pants
    base.leg = leg
    base.tail = tail
    base.arm = arm
