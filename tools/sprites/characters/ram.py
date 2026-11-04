"""Ram: a muscular, shirtless ram in a black baseball cap (the user's OC, art by Enro).

Defining features: charcoal-black face and muzzle with gold fang caps and a few gold piercings; long pointed peach
ears sticking out sideways; grey hair tufts under a black baseball cap (peak to the front); a broad black-furred torso
with a cream chest tuft and a silver chain necklace; cream-furred arms ending in black gloves; black joggers with a
drawstring; cream legs with dark hooves; a short cream tail. Broad shoulders over a narrower waist.
Won't fit at 64 px: the abs, nipple studs, skull ring, the lettering on the teeth, ram horns (hidden by the cap in
the reference, so left out).

    python tools/sprites/make_character.py ram
"""


def apply(base):
    Layer, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.ellipse, base.polygon, base.stroke,
                                                          base.both, base.minus, base.math)
    base.PAL.update({
        'F': (0x52, 0x50, 0x62),   # face fur: charcoal
        'f': (0x3E, 0x3C, 0x4C),   # face shade
        'W': (0x70, 0x6E, 0x82),   # muzzle: lighter charcoal
        'N': (0x9A, 0x92, 0xA8),   # nose
        'H': (0xC2, 0xBE, 0xCA),   # hair: pale grey
        'h': (0xE6, 0xE4, 0xEC),
        'P': (0xF0, 0xC4, 0xA2),   # ears: peach
        'p': (0xCC, 0x9C, 0x82),   # ear shade
        'J': (0x44, 0x42, 0x52),   # torso fur: black
        'j': (0x32, 0x30, 0x3E),
        'k': (0x5C, 0x5A, 0x6E),
        'I': (0xF4, 0xE8, 0xDA),   # cream fur (arms, legs, chest tuft, tail)
        'i': (0xD2, 0xBE, 0xAC),
        'n': (0x34, 0x32, 0x40),   # gloves, hooves
        'y': (0xF0, 0xB8, 0x30),   # gold (fangs, piercings)
        'x': (0xCC, 0xD2, 0xE0),   # silver chain
        'v': (0x34, 0x36, 0x48),   # cap
        'm': (0x52, 0x56, 0x6C),   # cap highlight
        's': (0x40, 0x40, 0x50),   # joggers
        'g': (0x2E, 0x2E, 0x3A),
        'S': (0xA8, 0xA8, 0xB8),   # drawstring
    })
    base.SHOULDER_BACK, base.SHOULDER_FRONT = (20, 41), (44, 41)

    base.MOUTHS.update({
        'none': base.grid(["KKKKK", "yK.Ky", "y...y"]),        # closed, gold fangs hanging over the lip
        'smile': base.grid(["K...K", "yKKKy", "y...y"]),
        'open': base.grid(["KKKKK", "yMMMy", "KMTMK", ".KKK."]),
        'shout': base.grid([".KKKK.", "KyMMyK", "KMMMMK", "KMTTMK", ".KKKK."]),
    })

    # Pointed ears sticking out sideways: (base dx, base dy from the head centre, angle from up, length, half width).
    base.EAR_SETS = {
        'up': [(-11, -4, -78, 14, 4.2), (11, -1, 80, 12, 4.4)],
        'tall': [(-8, -9, -38, 13, 4.2), (8, -9, 34, 12, 4.4)],
        'down': [(-12, -3, -128, 12, 4.2), (12, -3, 122, 11, 4.4)],
    }

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        for (bx, by, ang, ln, hw) in base.EAR_SETS[ears]:
            outer, inner, _ = base.ear(cx + bx, cy + by, ang, ln, hw)
            e.fill(outer, 'P')
            e.fill(minus(outer, lambda x, y, _o=outer: _o(x + 1, y - 1)), 'p')
        # Grey hair tufts poking out at the back, under the cap.
        e.fill(polygon([(cx - 13, cy - 3), (cx - 17, cy), (cx - 14.5, cy + 2), (cx - 17.5, cy + 6),
                        (cx - 12, cy + 7), (cx - 9, cy + 4)]), 'H')
        e.fill(lambda x, y: e.g[y][x] == 'H' and y > cy + 3, 'h')
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 14.5, 12.5)
        h.fill(skull, 'F')
        h.fill(polygon([(cx + 11, cy + 6), (cx + 15.5, cy + 8), (cx + 11, cy + 10)]), 'F')   # cheek tuft
        h.fill(minus(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 2.5, cy - 2.5, 15.5, 13)), 'f')
        h.fill(ellipse(cx + 4.5, cy + 7.5, 6.5, 3.8), 'W')                                     # muzzle
        h.outline()

        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        h.stamp(sq, cx - 7 + look, cy - 1).stamp(front, cx + 5 + look, cy - 1)
        h.stamp(base.NOSE, cx + 4 + look, cy + 5)
        m = base.MOUTHS[mouth_kind]
        h.stamp(m, cx + 5 + look - len(m[0]) // 2, cy + 7)
        h.stamp(["y"], cx + 11, cy + 2).stamp(["y"], cx + 10, cy + 10)                         # piercings
        if blush:
            h.stamp(["BBB"], cx - 8, cy + 5).stamp(["BBB"], cx + 9, cy + 4)

        # The baseball cap: a dome over the top of the head and a peak out to the front.
        c = Layer()
        dome = both(ellipse(cx - 0.5, cy - 3, 16.2, 11), lambda x, y: y <= cy - 4)
        c.fill(dome, 'v')
        c.fill(both(dome, ellipse(cx + 6, cy - 10, 6, 3.5)), 'm')                              # shine
        c.fill(lambda x, y: dome(x, y) and x <= cx - 11, 'v')
        c.fill(ellipse(cx - 0.5, cy - 13, 1.2, 0.8), 'm')                                       # button
        c.fill(lambda x, y: dome(x, y) and y >= cy - 6, 'v')
        c.outline()
        c.fill(stroke([(cx - 14, cy - 5.2), (cx + 13, cy - 5.2)], [0.5, 0.5]), 'm', only='v')    # band
        pk = Layer()
        pk.fill(polygon([(cx + 6, cy - 6.5), (cx + 16, cy - 5.5), (cx + 22, cy - 3.5), (cx + 21.5, cy - 1),
                         (cx + 6, cy - 3)]), 'm')
        pk.fill(polygon([(cx + 6, cy - 3.8), (cx + 21.5, cy - 1.8), (cx + 21.5, cy - 1), (cx + 6, cy - 3)]), 'v')
        pk.outline()
        return [e, h, c, pk]

    def hoodie(ux, uy):
        """The bare torso: broad shoulders in cream fur, a charcoal chest (the face's colour) with pecs and abs, a
        cream chest tuft running down to the belly, and a silver chain."""
        t = Layer()
        o = lambda pts: [(x + ux, y + uy) for (x, y) in pts]
        body = polygon(o([(24, 38), (40, 38), (46, 41), (46.5, 45), (43, 49.5), (22, 49.5), (17.5, 45),
                          (18, 41)]))
        # Cream fur all over (like the arms), shaded at the back.
        t.fill(body, 'I')
        t.fill(lambda x, y: body(x, y) and x <= 22 + ux, 'i')
        # The black fur: a shaggy patch over the chest and belly, a cream tuft notched into it under the chin.
        patch = polygon(o([(23.5, 40), (29.5, 40), (31, 43), (32.5, 45.5), (34, 43), (35.5, 40), (41, 40),
                           (42.5, 41.5), (41, 42.5), (42, 44), (40.5, 45), (41, 47), (39, 47.5), (39.5, 50),
                           (25.5, 50), (26, 47.5), (24, 47), (24.5, 45), (23, 44), (24, 42.5), (22.5, 41.5)]))
        t.fill(both(body, patch), 'F')
        # Pecs lit on top, a shadow under them and down the middle, two small lit ab blocks.
        for px in (28, 37):
            t.fill(both(both(body, patch), ellipse(px + ux, 43.2 + uy, 3.2, 1.2)), 'W')
        t.fill(both(patch, stroke(o([(25, 45.6), (40.5, 45.6)]), [0.5, 0.5])), 'f')
        for ax in (30.5, 34.5):
            t.fill(both(patch, ellipse(ax + ux, 47.6 + uy, 1.3, 0.8)), 'W')
        t.outline()
        t.fill(stroke(o([(24.5, 41), (27, 45.2), (32.5, 47.5), (38, 45.2), (40.5, 41)]), [0.6] * 5), 'x')
        t.fill(ellipse(32.5 + ux, 47.8 + uy, 1.1, 1.1), 'x')
        return t

    def shorts(ux, uy, sit=False):
        s = Layer()
        if sit:
            pts = [(20, 55), (43, 55), (47, 58), (48, 62.5), (20, 62.5)]
            top = 55
        else:
            pts = [(19, 48.5), (45, 48.5), (46.5, 55), (34, 55), (32, 52.5), (30, 55), (18, 55)]
            top = 48.5
        shape = polygon([(x + ux, y + uy) for (x, y) in pts])
        s.fill(shape, 's')
        s.fill(lambda x, y: shape(x, y) and x <= 22 + ux, 'g')
        s.fill(lambda x, y: shape(x, y) and y <= top + 1.2 + uy, 'g')                        # waistband
        s.fill(stroke([(31 + ux, top + 1 + uy), (30 + ux, top + 5 + uy)], [0.5, 0.5]), 'S')    # drawstrings
        s.fill(stroke([(34 + ux, top + 1 + uy), (35 + ux, top + 4 + uy)], [0.5, 0.5]), 'S')
        s.outline()
        return s

    def leg(path, fur='F', radii=(4.2, 3.4, 2.6, 2.6, 2.3)):
        """A stout cream leg with a fluffy hock and a dark hoof."""
        lg = Layer()
        cream = 'i' if fur == 'f' else 'I'
        lg.fill(stroke(path, list(radii[:len(path)])), cream)
        hx, hy = path[2]
        lg.fill(polygon([(hx - 0.5, hy - 3), (hx - 4.5, hy - 1.5), (hx - 2, hy - 0.5), (hx - 4, hy + 1),
                         (hx, hy + 1)]), cream)
        lg.fill(stroke(path[-2:], [2.4, 2.3]), 'n')                                            # hoof
        lg.outline()
        return lg

    def tail(ux, uy):
        t = Layer()
        for (dx, dy, r) in ((0, 0, 2.6), (-1.8, -1.2, 1.8), (-1.2, 1.6, 1.8)):
            t.fill(ellipse(18 + dx + ux, 50 + dy + uy, r, r), 'I')
        t.outline()
        return t

    def arm(path, paw=True):
        """A bulging cream-furred arm ending in a black glove."""
        a = Layer()
        n = len(path) - 1
        a.fill(stroke(path, [3.9] + [3.4] * (n - 1) + [3.2]), 'I')
        a.fill(minus(stroke(path, [3.9] + [3.4] * (n - 1) + [3.2]),
                     stroke([(x - 1, y + 1) for (x, y) in path], [3.9] + [3.4] * (n - 1) + [3.2])), 'i')
        (x0, y0), (x1, y1) = path[-2], path[-1]
        d = math.hypot(x1 - x0, y1 - y0) or 1
        dx, dy = (x1 - x0) / d, (y1 - y0) / d
        a.fill(ellipse(x1 + 2.2 * dx, y1 + 2.2 * dy, 2.9, 2.9), 'n')
        a.outline()
        return a

    base.head = head
    base.hoodie = hoodie
    base.shorts = shorts
    base.leg = leg
    base.tail = tail
    base.arm = arm
