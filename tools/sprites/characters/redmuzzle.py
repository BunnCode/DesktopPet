"""Redmuzzle: a stocky dark bear with a big red muzzle (the user's reference art).

Defining features: charcoal-brown fur, a huge round head that merges into a low, heavy body; small round ears set
wide; small dark slit-like eyes set wide on either side of a big red-orange oval muzzle with a dark nose and a frowning
mouth line (a darker lip below it); a tuft of pale spiky fur at the back of the neck; thick short legs with red claws;
a lighter brown sheen on the shoulder and the crown.
A quadruped: it stands on all fours, so this file draws its own rig and frames() instead of the rabbit's upright one.
It rears up on its hind legs for the poses that need paws (hang, shimmy, climb, cling, push, held). Sitting is a bear
sit (rump down, front legs straight).
Won't fit at 64 px: the white rim on the eye slits, the separate toes, the fur texture of the tuft.

    python tools/sprites/make_character.py redmuzzle
"""


def apply(base):
    Layer, Frame, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.F, base.ellipse, base.polygon,
                                                                 base.stroke, base.both, base.minus, base.math)
    G = base.grid
    base.PAL.update({
        'F': (0x66, 0x5F, 0x53),   # head fur: charcoal brown
        'f': (0x4C, 0x46, 0x3E),   # head shade
        'W': (0x86, 0x7D, 0x6C),   # sheen on the crown
        'J': (0x55, 0x4F, 0x46),   # body and leg fur (a touch darker than the head)
        'j': (0x3E, 0x39, 0x33),   # body shade, far legs
        'k': (0x80, 0x77, 0x66),   # shoulder sheen
        'P': (0xF0, 0x5C, 0x4A),   # muzzle and claws: red-orange
        'p': (0xC4, 0x3E, 0x38),   # muzzle shade (lower lip), far claws
        'N': (0x2E, 0x29, 0x2A),   # nose
        'n': (0x5A, 0x52, 0x54),   # nose shine
        'H': (0xC8, 0xC2, 0xA8),   # pale neck tuft
        'h': (0x9C, 0x96, 0x80),   # tuft shade
        'M': (0x6A, 0x1E, 0x24),   # open mouth
        'T': (0xF2, 0x9A, 0x8C),   # tongue
    })

    # Small dark slit-like eyes (3x6 inside the 5x7 stamp) with a tall shine; the other expressions keep the rabbit's
    # set, narrowed to match.
    base.EYES.update({
        'open': G([".EEE.", ".EwE.", ".EwE.", ".EEE.", ".EEE.", ".EEE.", "....."]),
        'blink': G([".....", ".....", ".....", "K...K", ".KKK.", ".....", "....."]),
        'happy': G([".....", ".KKK.", "K...K", ".....", ".....", ".....", "....."]),
        'wide': G([".KKK.", "KwwwK", "KwEwK", "KwEwK", "KwwwK", ".KKK.", "....."]),
        'droopy': G([".....", ".....", "KKKK.", ".EEE.", ".EwE.", ".EEE.", "....."]),
        'angry': G(["K....", ".KK..", ".EEKK", ".EwE.", ".EEE.", ".EEE.", "....."]),
    })
    # Mouths sit on the muzzle, below the nose. The resting face frowns, as in the reference.
    base.MOUTHS.update({
        'none': G([".KKK.", "K...K"]),
        'frown': G([".KKK.", "K...K", "K...K"]),
        'smile': G(["K...K", ".KKK."]),
        'pout': G([".KKK."]),
        'open': G([".KKK.", "KMMMK", "KMTMK", ".KKK."]),
        'shout': G([".KKKK.", "KMMMMK", "KMMMMK", "KMTTMK", ".KKKK."]),
        'munch': G([".KKK.", ".KMK.", "..K.."]),
    })
    NOSE = G([".NNN.", "NnNNN", "NNNNN", ".NNN."])

    # Round ears: (dx, dy of the centre from the head centre, rx, ry).
    EARS = {
        'up': [(-11, -9.5, 4.0, 4.0), (9, -11, 3.8, 3.8)],
        'tall': [(-10.5, -10.5, 3.8, 4.4), (8.5, -12, 3.6, 4.2)],
        'down': [(-13, -7, 4.2, 3.0), (5, -12, 4.0, 2.8)],   # flattened back
    }

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        """[ears and neck tuft (behind), head] for a head centred at (cx, cy)."""
        e = Layer()
        # Pale spiky tuft at the back of the neck, poking out behind the head over the shoulder.
        e.fill(polygon([(cx - 11, cy - 1), (cx - 18, cy - 3), (cx - 14.5, cy + 0.5), (cx - 20.5, cy + 2),
                        (cx - 15, cy + 4), (cx - 20, cy + 7.5), (cx - 14, cy + 7.5), (cx - 17, cy + 11.5),
                        (cx - 10, cy + 9)]), 'H')
        e.fill(lambda x, y: e.g[y][x] == 'H' and y >= cy + 5, 'h')
        for (dx, dy, rx, ry) in EARS[ears]:
            ox, oy = cx + dx, cy + dy
            e.fill(ellipse(ox, oy, rx, ry), 'F')
            e.fill(minus(ellipse(ox, oy, rx, ry), ellipse(ox + 1, oy - 1, rx, ry)), 'f')
            e.fill(ellipse(ox + 0.6, oy + 0.6, rx * 0.5, ry * 0.5), 'f')
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 15, 12.5)
        h.fill(skull, 'F')
        h.fill(minus(skull, ellipse(cx + 2.5, cy - 2.5, 16, 13)), 'f')                  # shade, lower back
        h.fill(minus(ellipse(cx + 1, cy - 6, 10, 4), ellipse(cx + 1, cy - 4.8, 10, 4)), 'W', only='F')   # sheen
        h.outline()
        # The big red-orange muzzle, outlined, with a darker lower lip.
        mx, my = cx + 2.5 + look, cy + 3.5
        h.fill(ellipse(mx, my, 6.0, 7.6), 'K')
        h.fill(ellipse(mx, my, 5.0, 6.6), 'P')
        h.fill(both(ellipse(mx, my, 5.0, 6.6), lambda x, y: y >= my + 4), 'p')
        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        h.stamp(sq, cx - 9 + look, cy - 2).stamp(front, cx + 8 + look, cy - 2)
        h.stamp(NOSE, round(mx) - 2, cy - 2)
        m = base.MOUTHS[mouth_kind]
        h.stamp(m, round(mx) - len(m[0]) // 2, cy + 4)
        if blush:
            h.stamp(["BB"], cx - 9, cy + 5).stamp(["BB"], cx + 11, cy + 5)
        return [e, h]

    def body(cx, cy, rx, ry, seat=None):
        """The body ellipse; with seat, cut flat so its bottom outline lands on that row."""
        b = Layer()
        shape = ellipse(cx, cy, rx, ry)
        if seat is not None:
            shape = both(shape, lambda x, y: y < seat)
        b.fill(shape, 'J')
        b.fill(minus(shape, ellipse(cx + 2, cy - 2, rx, ry)), 'j')                       # shade, lower back
        b.fill(both(shape, ellipse(cx + rx * 0.25, cy - ry * 0.6, rx * 0.45, ry * 0.28)), 'k')   # sheen
        b.outline()
        return b

    def tail(x, y):
        t = Layer()
        t.fill(ellipse(x, y, 2.6, 2.4), 'J')
        t.fill(both(ellipse(x, y, 2.6, 2.4), lambda px, py: py >= y + 1), 'j')
        return t.outline()

    def leg(hip, paw, flat=None, far=False, sheen=False):
        """A thick stubby leg from the hip (or shoulder) to a paw with two red claws. A paw on the ground is a flat
        oval with the claws to the front; a raised paw is round, with the claws pointing along the leg."""
        fur = 'j' if far else 'J'
        L = Layer()
        (hx, hy), (px, py) = hip, paw
        ground = py >= 59 if flat is None else flat
        if ground:
            L.fill(stroke([hip, (px, py - 1.2)], [4.2, 3.5]), fur)
            L.fill(ellipse(px + 0.6, py, 3.7, 2.0), fur)
            d = (1.0, 0.0)
        else:
            L.fill(stroke([hip, paw], [4.0, 3.2]), fur)
            L.fill(ellipse(px, py, 2.9, 2.9), fur)
            ll = math.hypot(px - hx, py - hy) or 1
            d = ((px - hx) / ll, (py - hy) / ll)
        if sheen and not far:
            L.fill(both(stroke([hip, paw], [3.8, 3.0]), ellipse(hx + 1.2, hy - 0.5, 2.0, 2.4)), 'k')
        L.outline()
        claw = 'p' if far else 'P'
        nx, ny = -d[1], d[0]
        c = Layer()
        if ground:
            for (ox, oy) in ((2.8, -0.2), (1.8, 1.2)):
                bx, by = px + ox, py + oy
                c.fill(polygon([(bx, by - 0.9), (bx + 2.6, by + 0.9), (bx, by + 0.9)]), claw)
        else:
            for s in (-1, 1):
                bx, by = px + d[0] * 2.2 + nx * s * 1.3, py + d[1] * 2.2 + ny * s * 1.3
                c.fill(polygon([(bx - nx * 0.9, by - ny * 0.9), (bx + d[0] * 2.2, by + d[1] * 2.2),
                                (bx + nx * 0.9, by + ny * 0.9)]), claw)
        c.outline()
        for y in range(base.CELL):
            for x in range(base.CELL):
                v = c.g[y][x]
                if v is not None and (v != 'K' or L.g[y][x] is None):
                    L.g[y][x] = v
        return L

    def bear(b, hc, legs, tl=None, ears='up', eye='open', mouth_kind='none', look=0, blush=False,
             props_mid=(), props_front=(), front_last=()):
        """A whole bear. b: body ellipse (cx, cy, rx, ry); hc: head centre; legs: (far hind, far front, near hind,
        near front) as (hip, paw) or None. Far legs go behind the body, near legs over it, all under the head.
        front_last: indexes of legs drawn over props_front (paws holding something in front)."""
        f = Frame()
        layers = {i: (leg(*lg, far=i < 2, sheen=i == 3) if lg else None) for i, lg in enumerate(legs)}
        for i in (0, 1):
            if layers[i] and i not in front_last:
                f.add(layers[i])
        f.add(tail(*(tl or (b[0] - b[2] + 1, b[1] - 2))))
        f.add(body(*b[:4], seat=b[4] if len(b) > 4 else None))
        for i in (2, 3):
            if layers[i] and i not in front_last:
                f.add(layers[i])
        for p in props_mid:
            f.add(p)
        for layer in head(hc[0], hc[1], ears, eye, mouth_kind, look, blush):
            f.add(layer)
        for p in props_front:
            f.add(p)
        for i in front_last:
            f.add(layers[i])
        return f.grid()

    FOOT, FAR_FOOT = 60.5, 59.6
    BODY = (25, 47, 12.5, 10)
    HEAD = (39, 32)

    def stand(dy=0, paws=(0, 0, 0, 0), lift=(0, 0, 0, 0)):
        """Standing legs; paws shifts each paw forward, lift raises it (far hind, far front, near hind, near front)."""
        hips = [(20, 51), (35, 51), (16, 53), (30, 53)]
        feet = [(21, FAR_FOOT), (37, FAR_FOOT), (15, FOOT), (31, FOOT)]
        return [((hx, hy + dy), (fx + paws[i], fy - lift[i] - (0.5 if lift[i] else 0)))
                for i, ((hx, hy), (fx, fy)) in enumerate(zip(hips, feet))]

    def standing(uy=0, hdx=0, hdy=0, legs=None, **kw):
        b = (BODY[0], BODY[1] + uy, BODY[2], BODY[3])
        return bear(b, (HEAD[0] + hdx, HEAD[1] + uy + hdy), legs or stand(uy), **kw)

    SIT_BODY = (25, 51.5, 11, 11)

    def sitting(hdx=0, hdy=0, front=None, **kw):
        legs = [None, ((38, 44), (40, FAR_FOOT)), ((20, 56), (25, FOOT)), ((34, 45), (35, FOOT))]
        if front:
            legs[1], legs[3] = front
        return bear(SIT_BODY, (37 + hdx, 28 + hdy), legs, tl=(15, 59), **kw)

    def frames():
        a = {}
        a['idle'] = [standing(hdy=1 if (i // 4) % 2 else 0, eye='blink' if i == 13 else 'open') for i in range(16)]

        # Diagonal gait: contact, the trailing pair swings forward (lifted), contact with the other pair ahead, ...
        A = (3, -3, -3, 3)       # far hind and near front forward
        B = (-3, 3, 3, -3)       # far front and near hind forward
        a['walk'] = [standing(legs=stand(0, A), look=1),
                     standing(hdy=-1, legs=stand(0, (0, 0, 0, 0), (0, 2.5, 2.5, 0)), look=1),
                     standing(legs=stand(0, B), look=1),
                     standing(hdy=-1, legs=stand(0, (0, 0, 0, 0), (2.5, 0, 0, 2.5)), look=1)]

        a['sit'] = [sitting(hdy=dy, eye='happy', mouth_kind='smile') for dy in (-1, 0)]

        def fall(i):
            u = 0 if i == 0 else -3
            legs = [((20, 47), (13, 55 + u)), ((35, 47), (45, 53 + u)),
                    ((16, 48), (8, 52 + u)), ((31, 49), (40, 58 + u))]
            return bear((25, 42, 13, 8.5), (39, 27), legs, ears='tall', eye='wide', mouth_kind='open')

        a['fall'] = [fall(0), fall(1)]

        def crouch(d):
            return stand(d, (-1, 1, -1, 1)) if d else stand()

        a['land'] = [standing(uy=dy, hdy=hdy, ears=e, eye=ey, legs=crouch(dy))
                     for (dy, hdy, e, ey) in ((3, 2, 'down', 'squeeze'), (2, 1, 'down', 'squeeze'),
                                              (0, 1, 'up', 'open'), (0, 0, 'up', 'open'))]

        # Upright poses: the body stands on end under the head, front legs used as arms.
        def held(i):
            arms = (((37, 39), (47, 31)), ((26, 40), (16, 46))) if i == 0 else (((37, 40), (48, 46)), ((26, 39), (15, 31)))
            hind = (((33, 53), (36, 61)), ((27, 54), (24, 60))) if i == 0 else (((33, 53), (35, 60)), ((27, 54), (27, 61)))
            return bear((30, 45, 9.5, 10.5), (32, 24), [hind[0], arms[0], hind[1], arms[1]], tl=(23, 54),
                        ears='tall', eye='squeeze', mouth_kind='shout')

        a['held'] = [held(0), held(1)]

        def happy(i):
            if i % 2:
                legs = [((19, 48), (17, 55)), ((35, 48), (39, 55)), ((14, 50), (11, 57)), ((30, 50), (34, 57))]
                return standing(uy=-3, hdy=1, legs=legs, eye='happy', mouth_kind='open', blush=True)
            return standing(hdy=1, eye='happy', mouth_kind='smile', blush=True)

        a['happy'] = [happy(i) for i in range(4)]

        def hang(i, shimmy=False):
            back, front = (15, 2.4), (49, 2.4)
            if shimmy:
                back, front = ((13, 3.2), (51, 2.4)) if i == 0 else ((17, 2.4), (47, 3.2))
            hind = (((34, 46), (36, 54)), ((28, 47), (27, 55))) if i == 0 else (((34, 46), (34, 55)), ((28, 47), (30, 54)))
            return bear((31, 38, 9.5, 9.5), (32, 17), [hind[0], ((37, 30), front), hind[1], ((26, 30), back)],
                        tl=(24, 47), ears='down', look=1 if shimmy else 0)

        a['hang'] = [hang(0), hang(1)]
        a['shimmy'] = [hang(0, True), hang(1, True)]

        a['idle_sad'] = [standing(hdy=2 + i, ears='down', eye='droopy', mouth_kind='frown') for i in range(2)]

        def sleep(i):
            legs = [None, None, ((15, 58), (21, FOOT)), ((34, 57), (48, FOOT))]
            return bear((24, 55.5, 15, 7), (39, 47 + i), legs, tl=(10, 54), ears='down', eye='blink',
                        mouth_kind='none', props_front=[base.zee(50, 18, False) if i == 0 else base.zee(53, 9, True)])

        a['sleep'] = [sleep(0), sleep(1)]

        def eat(i):
            return sitting(hdy=1, eye='happy', mouth_kind='munch' if i == 0 else 'smile', blush=True,
                           front=(((38, 44), (49, 45)), ((34, 45), (42, 46))),
                           props_front=[base.apple(46, 42, bitten=i == 1)], front_last=(1, 3))

        a['eat'] = [eat(0), eat(1)]

        # Climbing a wall on the right: reared up against it, paws (and the raised hind paw) at column 63.
        def climb(i, blink=False):
            up, low = ((51, 37), (58, 29)), ((51, 43), (58, 45))
            hup, hlow = ((48, 52), (58, 54)), ((48, 53), (58, 59.5))
            far_arm, near_arm = (up, low) if i == 0 else (low, up)
            far_leg, near_leg = (hlow, hup) if i == 0 else (hup, hlow)
            near_leg = ((near_leg[0][0] - 3, near_leg[0][1] + 1), (near_leg[1][0] - 2, near_leg[1][1]))
            return bear((46, 45, 9, 10), (39, 28), [far_leg, far_arm, near_leg, near_arm], tl=(37, 52),
                        eye='blink' if blink else 'open', look=1)

        a['climb'] = [climb(0), climb(1)]
        a['cling'] = [climb(0), climb(0, blink=True)]

        def push(i):
            s = i % 2
            hind = (((47, 52), (46 if s else 42, FAR_FOOT)), ((42, 53), (38 if s else 42, FOOT)))
            arms = (((49, 43), (58, 42 + s)), ((48, 47), (58, 47 + s)))
            return bear((44, 45, 9, 9.5), (38, 29 + s), [hind[0], arms[0], hind[1], arms[1]], tl=(35, 50),
                        ears='down', eye='squeeze', mouth_kind='pout')

        a['push'] = [push(i) for i in range(4)]

        # Sitting on a title bar: the rump's bottom outline (the seat) on row 46, hind legs hanging over the edge.
        def ledge(i):
            sw = (0, 2)[i]
            legs = [((35, 42), (38 + sw, 53)), ((37, 38), (40, 43.2), True), ((30, 43), (32 - sw, 55)),
                    ((33, 38), (36, 43.4), True)]
            return bear((28, 38.5, 10.5, 9.5, 46), (36, 20), legs, tl=(18, 41), ears='down', eye='happy', mouth_kind='smile')

        a['sit_ledge'] = [ledge(0), ledge(1)]

        def kick(paw, **kw):
            legs = stand()
            legs[3] = ((30, 52), paw)
            return standing(legs=legs, look=1, **kw)

        a['kick'] = [standing(legs=stand(0, (0, 0, 0, 0), (0, 0, 0, 2)), look=1),
                     kick((42, 55), eye='squeeze', mouth_kind='open'),
                     kick((40, 46), eye='happy', mouth_kind='smile'),
                     standing(look=1)]

        def game(i):
            return sitting(hdy=i, look=1, mouth_kind='pout' if i == 0 else 'smile',
                           front=(((38, 45), (48, 53)), ((34, 46), (40, 53))),
                           props_front=[base.pad(45, 57 + i, i == 1)], front_last=(1, 3))

        a['game'] = [game(0), game(1)]

        air = [((19, 44), (13, 51)), ((35, 44), (42, 50)), ((14, 46), (7, 52)), ((30, 46), (37, 53))]
        a['bounce'] = [standing(uy=3, hdy=1, legs=crouch(3), eye='happy', mouth_kind='smile', blush=True),
                       standing(uy=-4, hdy=1, legs=air, ears='tall', eye='happy', mouth_kind='open', blush=True)]

        a['love'] = [standing(hdy=i, eye='happy', mouth_kind='smile', blush=True, look=1,
                              props_front=[base.heart(56, 14 - 4 * i, 2.6), base.heart(60, 5 + 8 * i, 1.6)])
                     for i in range(2)]
        return a

    base.head = head
    base.tail = tail
    base.leg = leg
    base.frames = frames
