"""Ringbot: a mechanical creature with a floating ring (the user's reference art).

Defining features: a cream dome head with a peach face plate that has pointed flaps at the sides, red-ringed cyan
eyes, a small cream nose dot and a toothy mouth; a plum horn-fin at the back of the head and a zigzag antenna with a
red bead; a big orange ring floating behind, trailing cream ribbon streamers with zigzag ends; a round plum body with
cream bands; red-ringed joint discs at the shoulders and hips; cream arms ending in big plum mitts with cream claws;
banded coral/cream/pink bell-bottom legs with claw spikes.
Won't fit at 64 px: the panel lines, most of the floating bubbles, more than one short streamer, fine leg banding.

    python tools/sprites/make_character.py ringbot
"""


def apply(base):
    Layer, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.ellipse, base.polygon, base.stroke,
                                                          base.both, base.minus, base.math)
    base.PAL.update({
        'F': (0xF6, 0xDE, 0xA8),   # cream shell
        'f': (0xE4, 0xB0, 0x8C),   # cream shade
        'W': (0xFF, 0xF2, 0xD2),   # cream light
        'P': (0xF2, 0xAC, 0x84),   # face plate: peach
        'p': (0xD8, 0x86, 0x6E),   # face plate shade
        'J': (0x6E, 0x3C, 0x5E),   # plum
        'j': (0x52, 0x2A, 0x48),
        'k': (0x92, 0x5C, 0x7E),
        'H': (0xEC, 0x9A, 0x6E),   # the ring: orange
        'h': (0xF8, 0xC4, 0x96),   # ring light
        'X': (0xD2, 0x42, 0x5E),   # crimson (joint rings, eye rings, bead)
        't': (0xA8, 0x2E, 0x4A),   # crimson shade
        'E': (0xD2, 0x42, 0x5E),   # eye ring (crimson)
        'e': (0x4C, 0xD2, 0xEA),   # eye: cyan
        'y': (0x1E, 0x8C, 0xAC),   # eye: deep teal
        's': (0xE2, 0x84, 0x6C),   # coral (hips, legs)
        'g': (0xC2, 0x62, 0x58),   # coral shade
        'z': (0xE6, 0xA2, 0xA2),   # dusty pink band
        'N': (0xFF, 0xF2, 0xD2),   # nose dot
        'M': (0x5A, 0x22, 0x3A),   # open mouth
    })
    base.SHOULDER_BACK, base.SHOULDER_FRONT = (24, 41), (40, 41)

    G = base.grid
    base.EYES.update({
        'open': G([".EEE.", "EwweE", "EweeE", "EeeeE", "EeeyE", "EeyyE", ".EEE."]),
        'wide': G([".EEE.", "EwwwE", "EwewE", "EwewE", "EwwwE", ".EEE.", "....."]),
        'droopy': G([".....", ".....", "KKKKK", "EeeeE", "EewyE", "EeyyE", ".EEE."]),
        'angry': G(["K....", ".KK..", "EEEKK", "EweeE", "EeeyE", "EeyyE", ".EEE."]),
    })
    base.MOUTHS.update({
        'none': G(["KKKKK", ".FKF."]),           # a straight mouth with two little teeth
        'smile': G(["K...K", ".KKK.", ".F.F."]),
        'frown': G([".KKK.", "K...K"]),
    })

    # The horn-fin at the back of the head: (base dx, dy from the head centre, angle from up, length, half width).
    HORN = {
        'up': (-6, -10, -24, 11, 3.2),
        'tall': (-5, -10, -10, 11, 3.0),
        'down': (-8, -8, -70, 11, 3.2),
    }
    # The antenna at the front: base, then a zigzag to the bead.
    ANTENNA = {
        'up': [(7, -10), (9, -15), (12, -15), (13, -19)],
        'tall': [(6, -10), (7, -15), (10, -15), (10, -20)],
        'down': [(8, -10), (12, -13), (14, -11), (17, -13)],
    }

    def disc(L, x, y, r, inner='J', dot='F'):
        """A red-ringed joint disc: crimson rim, plum (or other) centre, a cream dot."""
        L.fill(ellipse(x, y, r, r), 'X')
        L.fill(minus(ellipse(x, y, r, r), ellipse(x + 0.6, y - 0.6, r, r)), 't')
        L.fill(ellipse(x + 0.3, y - 0.2, r * 0.55, r * 0.55), inner)
        if dot:
            L.fill(ellipse(x + 0.4, y - 0.3, r * 0.22, r * 0.22), dot)

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        bx, by, ang, ln, hw = HORN[ears]
        a = math.radians(ang)
        dx, dy = math.sin(a), -math.cos(a)
        nx, ny = math.cos(a), math.sin(a)
        x0, y0 = cx + bx, cy + by
        tip = (x0 + dx * ln, y0 + dy * ln)
        fin = polygon([(x0 - nx * hw, y0 - ny * hw), tip, (x0 + nx * hw * 0.4 + dx * ln * 0.45,
                                                          y0 + ny * hw * 0.4 + dy * ln * 0.45),
                       (x0 + nx * hw, y0 + ny * hw)])
        e.fill(fin, 'J')
        e.fill(both(fin, lambda x, y: (x - x0) * nx + (y - y0) * ny > hw * 0.15), 'F')   # cream front edge
        e.fill(stroke([(cx + px, cy + py) for (px, py) in ANTENNA[ears]], [0.6] * 4), 'F')
        e.outline()
        ex, ey = ANTENNA[ears][-1]
        bead = Layer()
        bead.fill(ellipse(cx + ex, cy + ey, 1.6, 1.6), 'X').outline()
        side = Layer()
        disc(side, cx - 13, cy + 3, 3.3)                                       # joint disc at the back of the head
        side.outline()

        h = Layer()
        skull = ellipse(cx, cy, 14.5, 12.5)
        h.fill(skull, 'F')
        h.fill(minus(skull, ellipse(cx + 2.5, cy - 2.5, 15.5, 13)), 'f')     # shade, lower back
        h.fill(both(skull, ellipse(cx + 3, cy - 8, 6, 2.6)), 'W')             # shine on the dome
        h.outline()

        pl = Layer()
        plate = polygon([(cx - 9, cy - 3.5), (cx - 13.5, cy - 7), (cx - 11, cy + 1), (cx - 7, cy + 7),
                         (cx - 1, cy + 11), (cx + 7, cy + 11), (cx + 13, cy + 7), (cx + 15.5, cy + 1),
                         (cx + 19, cy - 7), (cx + 13.5, cy - 3.5), (cx + 4, cy - 2.5)])
        pl.fill(plate, 'P')
        pl.fill(minus(plate, ellipse(cx + 3, cy - 1, 15, 11.5)), 'p')
        pl.outline()
        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        pl.stamp(sq, cx - 7 + look, cy - 1).stamp(front, cx + 6 + look, cy - 1)
        pl.stamp(["N"], cx + 2 + look, cy + 3).stamp(["N"], cx + 2 + look, cy + 4)
        m = base.MOUTHS[mouth_kind]
        pl.stamp(m, cx + 3 + look - len(m[0]) // 2, cy + 7)
        if blush:
            pl.stamp(["BB"], cx - 9, cy + 6).stamp(["BB"], cx + 11, cy + 6)
        return [e, bead, side, h, pl]

    def ring_layer(ux, uy):
        """The floating ring behind the whole body, with a cream streamer trailing from its back."""
        r = Layer()
        cx, cy = base.HX - 3 + ux, base.HY - 3 + uy
        # Streamer: a ribbon from the back of the ring, waving down and ending in a zigzag.
        r.fill(stroke([(cx - 15, cy + 2), (cx - 19, cy + 4), (cx - 21, cy + 9), (cx - 24, cy + 12),
                       (cx - 22, cy + 14), (cx - 26, cy + 16)], [1.2, 1.2, 1.1, 1.0, 0.9, 0.8]), 'F')
        r.outline()
        ring = minus(ellipse(cx, cy, 17, 17), ellipse(cx, cy, 14.4, 14.4))
        r.fill(ring, 'H')
        r.fill(both(ring, lambda x, y: (x - cx) + (y - cy) < -10), 'h')        # light on the upper back
        for ang in (35, 160, 250):                                             # cream bands
            a = math.radians(ang)
            bx, by = cx + math.cos(a) * 15.7, cy + math.sin(a) * 15.7
            r.fill(both(ring, ellipse(bx, by, 1.3, 1.3)), 'F')
        r.outline()
        return r

    def hoodie(ux, uy):
        """The round plum body with a cream band and a cream chest panel."""
        t = Layer()
        body = ellipse(32 + ux, 45 + uy, 11.5, 7.2)
        t.fill(body, 'J')
        t.fill(minus(body, ellipse(33.5 + ux, 44 + uy, 11.5, 7.2)), 'j')
        t.fill(both(body, ellipse(38 + ux, 42 + uy, 4, 2.2)), 'k')
        t.fill(both(body, lambda x, y: 46.5 + uy <= y - 0.08 * (x - 32 - ux) <= 48 + uy), 'F')
        t.outline()
        return t

    def shorts(ux, uy, sit=False):
        """The banded waist that joins the legs."""
        s = Layer()
        if sit:
            pts = [(21, 55), (43, 55), (46, 58), (47, 62.5), (21, 62.5)]
            top = 55
        else:
            pts = [(24, 49), (40, 49), (40.5, 54.5), (23.5, 54.5)]
            top = 49
        shape = polygon([(x + ux, y + uy) for (x, y) in pts])
        s.fill(shape, 's')
        s.fill(lambda x, y: shape(x, y) and top + 3 + uy <= y <= top + 4 + uy, 'F')
        s.fill(lambda x, y: shape(x, y) and x <= 24 + ux, 'g')
        s.outline()
        return s

    def tail(ux, uy):
        return ring_layer(ux, uy)

    def leg(path, fur='F', radii=None):
        """A banded bell-bottom leg from the hip to a flat foot, with claw spikes at the base."""
        lg = Layer()
        far = fur == 'f'
        hip = path[0]
        (bx, by), (tx, ty) = path[-2], path[-1]
        foot = ((bx + tx) / 2 - 1, (by + ty) / 2)
        if foot[1] >= 59.5:
            d = (0.0, 1.0)                                                      # on the ground: flat bottom
        else:
            ll = math.hypot(foot[0] - hip[0], foot[1] - hip[1]) or 1
            d = ((foot[0] - hip[0]) / ll, (foot[1] - hip[1]) / ll)
        along = lambda x, y: (x - foot[0]) * d[0] + (y - foot[1]) * d[1]
        cut = 1.4
        bell = both(stroke([hip, foot], [2.2, 3.9]), lambda x, y: along(x, y) <= cut)
        main, band, shade = ('g', 'f', 'J') if far else ('s', 'F', 'g')
        # Claw spikes, poking out at the front and back of the base.
        px, py = -d[1], d[0]                                                    # across the leg (to the front)
        bxm, bym = foot[0] + d[0] * cut, foot[1] + d[1] * cut
        for side in (1, -1):
            sx, sy = bxm + px * side * 3.5, bym + py * side * 3.5
            lg.fill(polygon([(sx - d[0] * 2.2, sy - d[1] * 2.2), (sx + px * side * 2.6, sy + py * side * 2.6),
                             (sx - px * side * 0.8, sy - py * side * 0.8)]), 'f' if far else 'W')
        lg.fill(bell, main)
        lg.fill(both(bell, lambda x, y: -1.6 <= along(x, y)), band)            # cream cuff at the base
        lg.fill(both(bell, lambda x, y: -4.6 <= along(x, y) <= -3.4), 'z' if not far else 'g')
        lg.fill(both(bell, lambda x, y: (x - foot[0]) * px + (y - foot[1]) * py < -3.0), shade)
        lg.outline()
        if not far:
            disc(lg, hip[0], hip[1] - 0.5, 2.4, dot=None)
        return lg

    def arm(path, paw=True):
        """A cream arm with a joint disc at the shoulder and a big plum mitt with cream claws."""
        a = Layer()
        n = len(path)
        a.fill(stroke(path, [2.4] * n), 'F')
        a.fill(minus(stroke(path, [2.4] * n), stroke([(x + 0.8, y - 0.8) for (x, y) in path], [2.4] * n)), 'f')
        (x0, y0), (x1, y1) = path[-2], path[-1]
        d = math.hypot(x1 - x0, y1 - y0) or 1
        dx, dy = (x1 - x0) / d, (y1 - y0) / d
        mx, my = x1 + 2.6 * dx, y1 + 2.6 * dy
        for s in (-1, 1):                                                       # claws
            cx_, cy_ = mx + dx * 2.6 - dy * s * 1.6, my + dy * 2.6 + dx * s * 1.6
            a.fill(polygon([(cx_ - dy * s * 1.1, cy_ + dx * s * 1.1), (cx_ + dy * s * 1.1, cy_ - dx * s * 1.1),
                            (cx_ + dx * 2.4, cy_ + dy * 2.4)]), 'W')
        a.outline()
        m = Layer()
        m.fill(ellipse(mx, my, 3.3, 3.3), 'J')
        m.fill(minus(ellipse(mx, my, 3.3, 3.3), ellipse(mx + 0.8, my - 0.8, 3.3, 3.3)), 'j')
        m.fill(ellipse(mx + 0.8, my - 1.2, 1.0, 0.8), 'k')
        m.outline()
        sh = Layer()
        disc(sh, path[0][0], path[0][1], 2.6)
        sh.outline()
        for y in range(base.CELL):
            for x in range(base.CELL):
                for L in (sh, m):
                    if L.g[y][x] is not None:
                        a.g[y][x] = L.g[y][x]
        return a

    base.head = head
    base.hoodie = hoodie
    base.shorts = shorts
    base.leg = leg
    base.tail = tail
    base.arm = arm
