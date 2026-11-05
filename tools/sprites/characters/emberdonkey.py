"""Emberdonkey: a little fire donkey (the user's reference art).

Defining features: long upright dark grey-brown ears with pale yellow-tan insides and dark tips; a mauve/taupe head
and body; a spiky tuft of mane between the ears; a big pale pink-white muzzle with two red nostrils; big red-orange
eyes with dark pupils; sooty black "melted" patches dripping over the lower legs, hindquarters and tail; red hooves;
a thin black tail ending in a red-and-yellow flame.
It stands on all fours (a chibi quadruped: big head, short chunky legs) for the grounded poses, and rears up on its
hind legs (the rabbit's rig, front legs as arms) for climb, cling, hang, shimmy, push, held, eat, game and sit_ledge.
Won't fit at 64 px: the fine fur strokes on the crest, the teeth, the separate small ember above the flame.

    python tools/sprites/make_character.py emberdonkey
"""


def apply(base):
    Layer, F, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.F, base.ellipse, base.polygon,
                                                             base.stroke, base.both, base.minus, base.math)
    base.PAL.update({
        'F': (0xB8, 0x9A, 0x98),   # head fur: mauve-taupe
        'f': (0x94, 0x74, 0x74),   # head shade
        'h': (0xCC, 0xB6, 0xB2),   # fur light (crest, top of the head)
        'W': (0xF6, 0xEC, 0xEC),   # muzzle: pale pink-white
        'S': (0xDC, 0xC6, 0xCA),   # muzzle shade
        'H': (0x60, 0x52, 0x50),   # ears: dark grey-brown
        'p': (0x34, 0x2E, 0x30),   # ear tips
        'P': (0xF2, 0xCE, 0x82),   # inner ears: pale yellow-tan
        'N': (0xD6, 0x3A, 0x3A),   # nostrils
        'y': (0xEC, 0x5A, 0x2C),   # iris: red-orange
        'J': (0xA0, 0x7C, 0x7A),   # body: mauve-brown
        'j': (0x7C, 0x5A, 0x5A),   # body shade
        'k': (0xBE, 0xA4, 0xA0),   # chest light
        't': (0x3C, 0x34, 0x38),   # soot
        '1': (0x54, 0x4A, 0x4E),   # soot light
        '3': (0x2E, 0x28, 0x2C),   # soot on the far legs
        'X': (0xD8, 0x40, 0x34),   # hooves, flame
        'x': (0xA2, 0x2A, 0x24),   # hoof shade
        '2': (0xF2, 0x86, 0x32),   # flame: orange
        'z': (0xFA, 0xD2, 0x58),   # flame: yellow
    })
    G = base.grid
    orig_frames = base.frames
    FOOTY = 60.6

    # ------------------------------------------------------------ face
    base.EYES.update({
        'open': G([".EEE.", "EwwyE", "EwwEE", "EyEEE", "EyEEE", "EyyyE", ".EEE."]),
        'wide': G([".KKK.", "KwwwK", "KwEyK", "KwEyK", "KwwwK", ".KKK.", "....."]),
        'droopy': G([".....", ".....", "KKKKK", "EwwEE", "EyEEE", "EyyyE", ".EEE."]),
        'angry': G(["K....", ".KK..", "EEEKK", "EwwEE", "EyEEE", "EyyyE", ".EEE."]),
    })
    base.MOUTHS.update({
        'none': G(["K...", ".KKK"]),
        'smile': G(["K...K", ".KKK."]),
        'frown': G([".KKK.", "K...K"]),
    })
    base.NOSE = G(["N...N", "N...N"])

    # Long donkey ears: (base dx, base dy from the head centre, angle from up, length, half width).
    base.EAR_SETS = {
        'up': [(-7, -8, -16, 19, 4.8), (5, -9, 12, 19, 5.0)],
        'tall': [(-6, -9, -6, 18, 4.7), (4, -10, 5, 18, 4.9)],
        'down': [(-6, -8, -100, 17, 5.0), (1, -9, -84, 17, 5.2)],
    }

    def ear(bx, by, angle, length, hw):
        """A long wide ear: (outer, inner, tip) shapes."""
        a = math.radians(angle)
        dx, dy = math.sin(a), -math.cos(a)
        nx, ny = math.cos(a), math.sin(a)

        def shape(scale_w, t0, t1, umin=None):
            def inside(x, y):
                rx, ry = x - bx, y - by
                u, v = (rx * dx + ry * dy) / length, rx * nx + ry * ny
                if not (t0 <= u <= t1) or (umin is not None and u < umin):
                    return False
                t = (u - t0) / (t1 - t0) * 2 - 1
                w = hw * scale_w * math.sqrt(max(0.0, 1 - t * t)) * (1 - 0.25 * max(0.0, t))
                return abs(v) <= w
            return inside

        return shape(1.0, -0.2, 1.0), shape(0.55, 0.1, 0.8), shape(1.0, -0.2, 1.0, umin=0.78)

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        for (bx, by, ang, ln, hw) in base.EAR_SETS[ears]:
            outer, inner, tip = ear(cx + bx, cy + by, ang, ln, hw)
            e.fill(outer, 'H')
            e.fill(minus(outer, lambda x, y, _o=outer: _o(x + 1, y - 1)), 'p')
            e.fill(inner, 'P')
            e.fill(tip, 'p')
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 14, 12.5)
        h.fill(skull, 'F')
        h.fill(polygon([(cx - 11, cy + 2), (cx - 16.5, cy + 4), (cx - 13, cy + 6), (cx - 15.5, cy + 9),
                        (cx - 9, cy + 9)]), 'F')                                    # spiky cheek fur at the back
        # The crest: a spiky tuft of mane between the ears.
        h.fill(polygon([(cx - 8, cy - 9), (cx - 7.5, cy - 15.5), (cx - 4, cy - 12.5), (cx - 2, cy - 18.5),
                        (cx + 0.5, cy - 13), (cx + 3.5, cy - 17), (cx + 4, cy - 12), (cx + 7, cy - 13.5),
                        (cx + 6, cy - 9)]), 'F')
        h.fill(minus(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 2.5, cy - 2.5, 15, 13)), 'f')
        h.fill(both(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 1, cy - 11, 7, 3.4)), 'h')
        h.outline()
        h.fill(stroke([(cx - 2, cy - 11), (cx - 1, cy - 15)], [0.4, 0.4]), 'F', only='h')

        mz = Layer()
        muzzle = ellipse(cx + 10, cy + 7, 7.5, 5.2)
        mz.fill(muzzle, 'W')
        mz.fill(minus(muzzle, ellipse(cx + 10.8, cy + 6.2, 7.5, 5.2)), 'S')
        mz.outline()
        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        mz.stamp(sq, cx - 7 + look, cy - 4).stamp(front, cx + 3 + look, cy - 4)
        mz.stamp(base.NOSE, cx + 10 + look, cy + 4)
        m = base.MOUTHS[mouth_kind]
        mz.stamp(m, cx + 12 + look - len(m[0]) // 2, cy + 8)
        if blush:
            mz.stamp(["BBB"], cx - 9, cy + 3).stamp(["BB"], cx + 1, cy + 3)
        return [e, h, mz]

    def flame_tail(path, flame):
        """A thin black tail along path ending in a flame at (x, y) (the flame's base)."""
        t = Layer()
        t.fill(stroke(path, [1.3] + [1.0] * (len(path) - 1)), 't')
        t.outline()
        fx, fy = flame
        fl = Layer()
        outer = polygon([(fx - 3.4, fy), (fx - 3, fy - 3), (fx - 1, fy - 6), (fx, fy - 10), (fx + 1.5, fy - 6),
                         (fx + 3.4, fy - 4), (fx + 3.4, fy), (fx + 1.8, fy + 2.4), (fx - 1.8, fy + 2.4)])
        fl.fill(outer, 'X')
        fl.fill(polygon([(fx - 2, fy), (fx - 1, fy - 4), (fx + 0.2, fy - 7), (fx + 1.6, fy - 3), (fx + 2, fy),
                         (fx, fy + 1.5)]), '2')
        fl.fill(polygon([(fx - 1, fy), (fx + 0.2, fy - 4.5), (fx + 1.2, fy - 1), (fx, fy + 0.8)]), 'z')
        fl.outline()
        for y in range(base.CELL):
            for x in range(base.CELL):
                if fl.g[y][x] is not None:
                    t.g[y][x] = fl.g[y][x]
        return t

    DRIP = {0: 2.5, 1: 3, 4: 1.5, 7: 3.5, 8: 3, 11: 1, 14: 2.5, 15: 3, 18: 1.5, 21: 3}

    def drip(v):
        return DRIP.get(int(v) % 24, 0)

    # ------------------------------------------------------------ rearing parts (the rabbit's rig)
    def hoodie(ux, uy):
        """The upright body: mauve, a light chest, soot dripping up from the belly."""
        b = Layer()
        o = lambda pts: [(x + ux, y + uy) for (x, y) in pts]
        body = polygon(o([(25, 38), (39, 38), (42, 42), (43.5, 47), (44.5, 51.5), (19.5, 51.5), (20.5, 47),
                          (22, 42)]))
        b.fill(body, 'J')
        b.fill(lambda x, y: body(x, y) and x <= 23 + ux, 'j')
        b.fill(both(body, ellipse(36 + ux, 43 + uy, 5, 4)), 'k')
        b.fill(lambda x, y: body(x, y) and y >= 49 + uy - drip(x - ux), 't')
        b.outline()
        return b

    def shorts(ux, uy, sit=False):
        """The sooty hindquarters (the seat)."""
        s = Layer()
        if sit:
            pts = [(20, 55), (43, 55), (47, 58), (48, 62.5), (20, 62.5)]
        else:
            pts = [(21, 48), (43, 48), (44.5, 54.5), (33.5, 54.5), (32, 52.5), (30.5, 54.5), (19.5, 54.5)]
        shape = polygon([(x + ux, y + uy) for (x, y) in pts])
        s.fill(shape, 't')
        s.fill(lambda x, y: shape(x, y) and x >= 40 + ux, '1')
        s.outline()
        return s

    def leg(path, fur='F', radii=(3.8, 3.0, 2.2, 2.2, 1.9)):
        """A sooty hind leg with a red hoof."""
        lg = Layer()
        far = fur == 'f'
        lg.fill(stroke(path, list(radii[:len(path)])), '3' if far else 't')
        lg.fill(stroke(path[-2:], [2.2, 1.9]), 'x' if far else 'X')
        lg.outline()
        return lg

    def arm(path, paw=True):
        """A front leg used as an arm: mauve above, soot dripping down to a red hoof."""
        a = Layer()
        n = len(path)
        shape = stroke(path, [3.0] * (n - 1) + [2.6])
        a.fill(shape, 'J')
        a.fill(minus(shape, stroke([(x + 0.8, y - 0.8) for (x, y) in path], [3.0] * (n - 1) + [2.6])), 'j')
        lens = [math.hypot(b[0] - c[0], b[1] - c[1]) for b, c in zip(path, path[1:])]
        total = sum(lens)

        def along(x, y):
            best, acc, out = 1e9, 0.0, 0.0
            for (p0, p1, ln) in zip(path, path[1:], lens):
                dx, dy = p1[0] - p0[0], p1[1] - p0[1]
                t = 0.0 if ln == 0 else max(0.0, min(1.0, ((x - p0[0]) * dx + (y - p0[1]) * dy) / (ln * ln)))
                d = math.hypot(p0[0] + t * dx - x, p0[1] + t * dy - y)
                if d < best:
                    best, out = d, acc + t * ln
                acc += ln
            return out

        a.fill(lambda x, y: shape(x, y) and along(x, y) >= total * 0.5 - drip(x + y) * 0.6, 't')
        (x0, y0), (x1, y1) = path[-2], path[-1]
        d = math.hypot(x1 - x0, y1 - y0) or 1
        dx, dy = (x1 - x0) / d, (y1 - y0) / d
        hoof = stroke([(x1 + 0.6 * dx, y1 + 0.6 * dy), (x1 + 2.2 * dx, y1 + 2.2 * dy)], [2.6, 2.4])
        a.fill(hoof, 'X')
        a.outline()
        return a

    def tail(ux, uy):
        path = [(20 + ux, 50 + uy), (15 + ux, 52.5 + uy), (11 + ux, 51 + uy), (9.5 + ux, 46.5 + uy),
                (10 + ux, 43 + uy)]
        return flame_tail(path, (10 + ux, 40 + uy))

    base.ear = ear
    base.head = head
    base.hoodie = hoodie
    base.shorts = shorts
    base.leg = leg
    base.arm = arm
    base.tail = tail

    # ------------------------------------------------------------ on all fours
    HEAD = (38, 30)
    BODY = (24, 46)
    HIPS = {'hf': (20.5, 48.5), 'ff': (34.5, 48.5), 'hn': (16, 50), 'fn': (30.5, 50)}

    def qbody(bx, by):
        b = Layer()
        cx, cy = BODY[0] + bx, BODY[1] + by
        body = ellipse(cx, cy, 12.5, 7.6)
        b.fill(body, 'J')
        b.fill(minus(body, ellipse(cx + 1, cy - 1.2, 12.5, 7.6)), 'j')
        b.fill(both(body, ellipse(cx + 9, cy - 1, 4.5, 4.5)), 'k')
        # Soot over the hindquarters and dripping along the belly.
        b.fill(lambda x, y: body(x, y) and (x - bx <= 17 + drip(y - by) * 0.8 or
                                            y - by >= 50.5 - drip(x - bx)), 't')
        b.fill(both(lambda x, y: b.g[y][x] == 't', ellipse(cx - 9, cy - 3, 3, 2)), '1')
        b.outline()
        return b

    def qleg(hip, foot, kind, far):
        """A short chunky leg from hip to foot; the hoof is flat when it stands on the ground."""
        lg = Layer()
        (hx, hy), (fx, fy) = hip, foot
        L = math.hypot(fx - hx, fy - hy) or 1
        dx, dy = (fx - hx) / L, (fy - hy) / L
        shape = stroke([hip, foot], [3.4, 2.6])
        u = lambda x, y: (x - hx) * dx + (y - hy) * dy
        if kind == 'front':
            lg.fill(shape, 'j' if far else 'J')
            lg.fill(lambda x, y: shape(x, y) and u(x, y) >= L * 0.45 - drip(x * 3) * 0.7, '3' if far else 't')
        else:
            lg.fill(shape, '3' if far else 't')
        if not far:                                                       # light front edge on the near legs
            lg.fill(lambda x, y: shape(x, y) and not shape(x + 1, y), '1', only='t')
        hoof_col = 'x' if far else 'X'
        lg.fill(lambda x, y: shape(x, y) and u(x, y) >= L - 1.4, hoof_col)
        if fy >= FOOTY - 0.3:
            lg.fill(stroke([(fx - 0.6, FOOTY), (fx + 1.2, FOOTY)], [2.0, 2.0]), hoof_col)
        lg.outline()
        return lg

    def donkey(bx=0, by=0, hdx=0, hdy=0, ears='up', eye='open', mouth_kind='none', look=0, blush=False,
               legs=None, props_front=()):
        """A whole donkey on all fours. legs: {leg: (foot dx, lift)} or {leg: (x, y) absolute foot} per leg key
        ('hn' near hind, 'hf' far hind, 'fn' near front, 'ff' far front)."""
        legs = legs or {}

        def one(key, far):
            hx, hy = HIPS[key]
            hip = (hx + bx, hy + by)
            spec = legs.get(key, (0, 0))
            if len(spec) == 3:
                foot = spec[1:]
            else:
                foot = (hx + bx + spec[0], FOOTY - spec[1])
            return qleg(hip, foot, 'front' if key[0] == 'f' else 'hind', far)

        f = F()
        f.add(one('hf', True)).add(one('ff', True))
        tx, ty = 12 + bx, 44 + by
        f.add(flame_tail([(tx, ty), (8 + bx, 46 + by), (5 + bx, 43 + by), (4.5 + bx, 39 + by), (5.5 + bx, 36 + by)],
                         (5.5 + bx, 33 + by)))
        f.add(qbody(bx, by))
        f.add(one('hn', False)).add(one('fn', False))
        for layer in head(HEAD[0] + bx + hdx, HEAD[1] + by + hdy, ears, eye, mouth_kind, look, blush):
            f.add(layer)
        for layer in props_front:
            f.add(layer)
        return f

    def lying(hdy=0, ears='up', eye='open', mouth_kind='none', blush=False, props_front=()):
        """Lying down: legs folded under, belly on the ground."""
        f = F()
        f.add(flame_tail([(12, 55), (7, 57), (4, 54), (3.5, 50), (5, 47)], (5, 44)))
        b = Layer()
        body = ellipse(24, 54.4, 13, 7.6)
        b.fill(body, 'J')
        b.fill(minus(body, ellipse(25, 53.2, 13, 7.6)), 'j')
        b.fill(both(body, ellipse(33, 53, 4.5, 4.5)), 'k')
        b.fill(lambda x, y: body(x, y) and (x <= 18 + drip(y) * 0.8 or y >= 59 - drip(x)), 't')
        b.outline()
        f.add(b)
        hl = Layer()                                                      # the folded hind leg
        hl.fill(ellipse(17, 56, 5.5, 4.8), 't')
        hl.fill(stroke([(16, 60.4), (23, 60.4)], [2.0, 2.0]), 't')
        hl.fill(stroke([(22, 60.4), (24, 60.4)], [2.0, 2.0]), 'X')
        hl.outline()
        f.add(hl)
        fl = Layer()                                                      # the folded front leg
        fl.fill(stroke([(31, 57), (39, 60.4)], [2.6, 2.1]), 't')
        fl.fill(stroke([(38.5, 60.4), (41, 60.4)], [2.1, 2.0]), 'X')
        fl.outline()
        f.add(fl)
        for layer in head(HEAD[0], HEAD[1] + 7 + hdy, ears, eye, mouth_kind, 0, blush):
            f.add(layer)
        for layer in props_front:
            f.add(layer)
        return f

    def frames():
        a = orig_frames()                         # rearing poses come from the rabbit's rig
        a['idle'] = [donkey(hdy=1 if (i // 4) % 2 else 0, eye='blink' if i == 13 else 'open').grid()
                     for i in range(16)]

        # Trot: diagonal pairs (near front + far hind, far front + near hind); the trailing pair swings forward.
        def walk(i):
            A = [(3, 0), (0, 0), (-3, 0), (0, 2.5)][i]
            B = [(-3, 0), (0, 2.5), (3, 0), (0, 0)][i]
            return donkey(look=1, hdy=-1 if i % 2 else 0, legs={'fn': A, 'hf': A, 'ff': B, 'hn': B})

        a['walk'] = [walk(i).grid() for i in range(4)]
        a['sit'] = [lying(hdy=dy, eye='happy', mouth_kind='smile').grid() for dy in (-1, 0)]
        splay = {'fn': (4, 3), 'ff': (5, 4), 'hn': (-4, 3), 'hf': (-3, 4)}
        a['fall'] = [donkey(by=-1, ears='tall', eye='wide', mouth_kind='open', legs=splay).grid(),
                     donkey(by=-1, ears='tall', eye='wide', mouth_kind='open',
                            legs={'fn': (6, 5), 'ff': (3, 3), 'hn': (-6, 5), 'hf': (-2, 3)}).grid()]
        crouch = {'fn': (1.5, 0), 'ff': (1.5, 0), 'hn': (-1.5, 0), 'hf': (-1.5, 0)}
        a['land'] = [donkey(by=dy, hdy=hdy, ears=e, eye=ey, legs=crouch if dy else None).grid()
                     for (dy, hdy, e, ey) in ((3, 2, 'down', 'squeeze'), (2, 1, 'down', 'squeeze'),
                                              (0, 1, 'up', 'open'), (0, 0, 'up', 'open'))]

        def happy(i):
            if i % 2 == 1:
                return donkey(by=-3, hdy=2, eye='happy', mouth_kind='open', blush=True,
                              legs={'fn': (2, 5), 'ff': (2, 5), 'hn': (-2, 5), 'hf': (-2, 5)})
            return donkey(eye='happy', mouth_kind='smile', blush=True, hdy=1)

        a['happy'] = [happy(i).grid() for i in range(4)]
        a['idle_sad'] = [donkey(ears='down', eye='droopy', mouth_kind='frown', hdy=2 + i).grid() for i in range(2)]
        a['sleep'] = [lying(hdy=2 + i, ears='down', eye='blink', props_front=[
            base.zee(52, 14, False) if i == 0 else base.zee(54, 6, True)]).grid() for i in range(2)]

        # Kicking a ball in front: the near front leg swings forward and up.
        a['kick'] = [donkey(look=1, legs={'fn': (0, 2.5)}).grid(),
                     donkey(look=1, eye='squeeze', mouth_kind='open', legs={'fn': (0, 41, 52)}).grid(),
                     donkey(look=1, eye='happy', mouth_kind='smile', legs={'fn': (0, 42, 45)}).grid(),
                     donkey(look=1).grid()]
        a['bounce'] = [donkey(by=3, hdy=0, eye='happy', mouth_kind='smile', blush=True, legs=crouch).grid(),
                       donkey(by=-3, hdy=2, ears='tall', eye='happy', mouth_kind='open', blush=True,
                              legs={'fn': (4, 4), 'ff': (5, 5), 'hn': (-4, 4), 'hf': (-3, 5)}).grid()]
        a['love'] = [donkey(eye='happy', mouth_kind='smile', blush=True, look=1, hdy=i,
                            props_front=[base.heart(57, 12 - 4 * i, 2.4), base.heart(60, 4 + 6 * i, 1.4)]).grid()
                     for i in range(2)]
        return a

    base.frames = frames
