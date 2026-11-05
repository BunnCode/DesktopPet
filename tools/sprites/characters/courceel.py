"""Courceel: a tan foal that rears up on its hind legs (the user's reference art).

Defining features: a peach-tan coat; a pale cream blaze over the front of the face and the whole muzzle; big blue
eyes with a white shine; a small dark nostril and a gentle smile; upright tan ears with darker insides; a dark grey
forelock swept back over the crown (and a short mane at the back of the head); a darker tan patch on the shoulder;
chunky dark grey hooves with thick "boot" cuffs on all four legs; a dark grey flowing tail.
Courceel is a quadruped, so it has its own rig (frames() below): a chibi foal standing on all four short legs, that
rears up on its hind legs (as in the reference) to climb, hang, push, flail when held and hop when happy, and sits
on its haunches to eat and play games.
Won't fit at 64 px: the eye's separate pupil ring, the soft shading on the muzzle, the folds in the boot cuffs.

    python tools/sprites/make_character.py courceel
"""


def apply(base):
    Layer, F, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.F, base.ellipse, base.polygon,
                                                             base.stroke, base.both, base.minus, base.math)
    CELL = base.CELL
    base.PAL.update({
        'F': (0xF0, 0xB6, 0x84),   # coat: peach tan
        'f': (0xD2, 0x92, 0x64),   # coat shade (and the far legs)
        't': (0xFA, 0xCE, 0xA2),   # coat light
        'p': (0xB8, 0x78, 0x50),   # darker tan: shoulder patch, inner ears
        'W': (0xFB, 0xE8, 0xCE),   # cream blaze and muzzle
        'H': (0x5E, 0x58, 0x5E),   # mane and tail: dark grey
        'h': (0x82, 0x7C, 0x82),   # mane shine
        '0': (0x58, 0x58, 0x60),   # hoof boots
        '9': (0x42, 0x42, 0x4A),   # hoof shade
        '8': (0x78, 0x78, 0x82),   # cuff light
        'y': (0x4E, 0xA6, 0xE0),   # eye: blue
        'z': (0xA6, 0xDA, 0xF6),   # eye: light blue
        'N': (0x3A, 0x2A, 0x2E),   # nostril
    })

    G = base.grid
    base.EYES.update({
        'open': G([".EEE.", "EwwzE", "EwwyE", "EzyEE", "EyyEE", "EzyyE", ".EEE."]),
        'wide': G([".KKK.", "KwwwK", "KwywK", "KwywK", "KwwwK", ".KKK.", "....."]),
        'droopy': G([".....", ".....", "KKKKK", "EwwyE", "EzyEE", "EzyyE", ".EEE."]),
        'angry': G(["K....", ".KK..", "EEEKK", "EwwyE", "EzyEE", "EzyyE", ".EEE."]),
    })
    base.MOUTHS.update({
        'none': G(["K..K", ".KK."]),           # a gentle smile
        'smile': G(["K...K", ".KKK."]),
    })

    # ------------------------------------------------------------ helpers
    def rell(cx, cy, rx, ry, ang):
        """An ellipse whose rx axis points at ang degrees (0 = right, 90 = up)."""
        a = math.radians(ang)
        ca, sa = math.cos(a), math.sin(a)

        def inside(x, y):
            dx, dy = x - cx, y - cy
            u, v = dx * ca - dy * sa, dx * sa + dy * ca
            return (u / rx) ** 2 + (v / ry) ** 2 <= 1.0
        return inside

    def merge(dst, src):
        for y in range(CELL):
            for x in range(CELL):
                if src.g[y][x] is not None:
                    dst.g[y][x] = src.g[y][x]
        return dst

    # ------------------------------------------------------------ head
    EARS = {
        'up': [(-8, -7, -24, 10.5, 3.5), (3, -10, 8, 10.5, 3.7)],
        'tall': [(-7, -8, -14, 11, 3.4), (3, -10, 3, 11, 3.6)],
        'down': [(-6, -8, -100, 10, 3.5), (0, -10, -80, 10, 3.6)],   # pinned back
    }
    base.EAR_SETS = EARS

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        # A short mane at the back of the head, in chunky locks.
        e.fill(polygon([(cx - 6, cy - 12), (cx - 12, cy - 10), (cx - 16, cy - 6), (cx - 14, cy - 3.5),
                        (cx - 16.5, cy + 1), (cx - 10, cy + 3)]), 'H')
        e.fill(lambda x, y: e.g[y][x] == 'H' and x >= cx - 13 and y <= cy - 6, 'h')
        for (bx, by, ang, ln, hw) in EARS[ears]:
            outer, inner, _ = base.ear(cx + bx, cy + by, ang, ln, hw)
            e.fill(outer, 'F')
            e.fill(minus(outer, lambda x, y, _o=outer: _o(x + 1, y - 1)), 'f')
            e.fill(inner, 'p')
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 12.5, 11.5)
        snout = ellipse(cx + 9.5, cy + 5, 8, 5.5)
        h.fill(skull, 'F').fill(snout, 'F')
        h.fill(minus(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 3, cy - 2.5, 14, 12.5)), 'f')   # shade, lower back
        h.fill(minus(lambda x, y: h.g[y][x] == 'f', ellipse(cx + 9.5, cy + 5, 8, 5.5)), 'f')
        # The cream blaze: down the front of the face and over the whole muzzle.
        blaze = lambda x, y: (ellipse(cx + 10.5, cy + 4, 9, 7.5)(x, y) or
                              polygon([(cx + 1, cy - 11), (cx + 7, cy - 11), (cx + 11, cy - 1), (cx + 5, cy)])(x, y))
        h.fill(both(blaze, lambda x, y: h.g[y][x] in ('F', 'f')), 'W')
        # The forelock: swept back over the crown in two locks, the big one curling up at the back.
        lock = polygon([(cx + 7, cy - 8), (cx + 5, cy - 13), (cx, cy - 16), (cx - 5, cy - 18.5),
                        (cx - 10, cy - 20.5), (cx - 8.5, cy - 16), (cx - 12, cy - 13.5), (cx - 9, cy - 10.5),
                        (cx - 3, cy - 10), (cx + 2, cy - 7)])
        h.fill(lock, 'H')
        h.fill(both(lock, lambda x, y: y <= cy - 13 and x >= cx - 7 and x <= cx + 2), 'h')
        h.outline()

        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        h.stamp(sq, cx - 7 + look, cy - 3).stamp(front, cx + 2 + look, cy - 3)
        h.stamp(["N", "N"], cx + 14 + look, cy + 3)                                      # nostril
        m = base.MOUTHS[mouth_kind]
        h.stamp(m, cx + 11 + look - len(m[0]) // 2, cy + 8)
        if blush:
            h.stamp(["BB"], cx - 9, cy + 4).stamp(["BB"], cx + 7, cy + 5)
        return [e, h]

    # ------------------------------------------------------------ body parts
    def off(path, dx, dy):
        return [(x + dx, y + dy) for (x, y) in path]

    def body(bx, by, ang, rx=12.0, ry=7.5, neck=None):
        b = Layer()
        shape = rell(bx, by, rx, ry, ang)
        if neck:
            shape = (lambda s, n: lambda x, y: s(x, y) or n(x, y))(shape, stroke(neck, [4.6, 4.2]))
        a = math.radians(ang)
        ca, sa = math.cos(a), math.sin(a)
        b.fill(shape, 'F')
        b.fill(minus(shape, lambda x, y: rell(bx + 1.2, by - 1.6, rx, ry, ang)(x, y) or
                     (neck is not None and stroke(off(neck, 1.2, -1), [4.6, 4.2])(x, y))), 'f')               # shade, lower back
        fx, fy = bx + ca * rx * 0.45, by - sa * rx * 0.45                               # towards the chest
        b.fill(both(shape, rell(fx + sa * 1.2 + 0.5, fy - ry * 0.35, 3.6, 2.6, ang)), 'p')   # shoulder patch
        b.fill(both(shape, rell(bx - ca * rx * 0.3 + sa * 0.5, by + sa * rx * 0.3 - ry * 0.55, 4, 1.1, ang)),
               't', only='F')                                                           # light along the back
        b.outline()
        return b

    def leg(path, far=False):
        """A short chunky leg ending in a grey hoof boot: a thick rolled cuff over a flared hoof."""
        tan, hoof, shade, cuff_light = ('f', '9', '9', '0') if far else ('F', '0', '9', '8')
        (x0, y0), (x1, y1) = path[-2], path[-1]
        d = math.hypot(x1 - x0, y1 - y0) or 1
        dx, dy = (x1 - x0) / d, (y1 - y0) / d
        px, py = -dy, dx                                                                # across (to the left/back)
        lg = Layer()
        lg.fill(stroke(path, [2.7] + [2.5] * (len(path) - 1)), tan)
        pt = lambda u, v: (x1 + dx * u + px * v, y1 + dy * u + py * v)
        hshape = polygon([pt(1.0, -2.2), pt(1.0, 2.2), pt(6.9, 2.8), pt(6.9, -2.8)])
        lg.fill(hshape, hoof)
        lg.fill(both(hshape, lambda x, y: (x - x1) * px + (y - y1) * py > 1.2), shade)
        lg.outline()
        c = Layer()
        ang = math.degrees(math.atan2(-dy, dx))
        cuff = rell(x1, y1, 2.0, 3.4, ang)
        c.fill(cuff, hoof)
        c.fill(both(cuff, lambda x, y: (x - x1) * dx + (y - y1) * dy < -0.7), cuff_light)
        c.outline()
        return merge(lg, c)

    def tail(x, y, ang=0.0, flip=1.0):
        """A dark grey tail flowing back from (x, y); ang rotates it (positive swings the tip down)."""
        a = math.radians(ang)
        ca, sa = math.cos(a), math.sin(a)
        rot = lambda px, py: (x + px * ca - py * sa, y + px * sa + py * ca * flip)
        t = Layer()
        main = [(0, 0), (-4, -1.8), (-8, -1.2), (-10.5, 2), (-11.5, 6), (-10.5, 10)]
        t.fill(stroke([rot(*p) for p in main], [1.6, 2.6, 3.0, 2.9, 2.3, 1.2]), 'H')
        t.fill(stroke([rot(-10.5, 6), rot(-13, 10), rot(-13.5, 12)], [1.6, 1.0, 0.7]), 'H')   # wispy end
        t.fill(stroke([rot(-3.5, -2.2), rot(-7.5, -1.8), rot(-9.5, 0.8)], [0.6, 0.7, 0.5]), 'h')  # shine
        t.outline()
        return t

    # ------------------------------------------------------------ assembling a foal
    def foal(bod, hd, far=(), near=(), over=(), tl=None, props_mid=(), props_front=(), neck=None):
        """bod: (x, y, angle[, rx, ry]); hd: (cx, cy, ears, eye, mouth, look, blush); far legs behind the body,
        near legs over it (under the head), over legs over the head and props_front (holding things up)."""
        f = F()
        for p in far:
            f.add(leg(p, far=True))
        if tl:
            f.add(tail(*tl))
        f.add(body(*bod, neck=neck))
        for p in near:
            f.add(leg(p))
        for layer in props_mid:
            f.add(layer)
        for layer in head(*hd):
            f.add(layer)
        for layer in props_front:
            f.add(layer)
        for p in over:
            f.add(leg(p))
        return f.grid()

    # Standing on all fours: body x 16-40, y 39-53; head centre (40, 26); hooves on row 63 (ankles at y 56).
    ANK = 56.0
    STAND = {'nh': [(20, 48), (20, ANK)], 'fh': [(24, 47), (24.5, ANK)],
             'nf': [(34, 48), (34, ANK)], 'ff': [(38, 47), (38.5, ANK)]}

    def lifted(top, fwd=1.5):
        return [top, (top[0] + 2 + fwd, top[1] + 3.5), (top[0] + fwd, top[1] + 5)]

    def stand(dx=0, dy=0, hdx=0, hdy=0, ears='up', eye='open', mouth='none', look=0, blush=False, legs=None,
              props_front=(), tail_ang=0.0):
        L = dict(STAND)
        if legs:
            L.update(legs)
        bod = (28 + dx, 45 + dy, 0, 12, 7)
        hd = (40 + dx + hdx, 28 + dy + hdy, ears, eye, mouth, look, blush)
        return foal(bod, hd, far=[L['fh'], L['ff']], near=[L['nh'], L['nf']],
                    tl=(17 + dx, 42 + dy, tail_ang), props_front=props_front,
                    neck=[(35 + dx, 43 + dy), (38 + dx + hdx, 34 + dy + hdy)])

    # Rearing on the hind legs (as in the reference): body upright, forelegs tucked in front of the chest.
    def rear(dx=0, dy=0, hdx=0, hdy=0, ears='up', eye='open', mouth='none', look=0, blush=False,
             fore=None, hind=None, ang=65, props_front=(), over=None, far_fore_behind=True):
        nf = [(35, 41), (39.5, 42.5), (40, 46.5)]
        ff = [(38, 37), (43.5, 37), (46, 40)]
        if fore:
            nf, ff = fore
        nh, fh = hind or ([(27, 50), (26, ANK)], [(31, 49), (31.5, ANK)])
        bod = (31 + dx, 44 + dy, ang, 11.5, 7.5)
        hd = (36 + dx + hdx, 23 + dy + hdy, ears, eye, mouth, look, blush)
        nfp, ffp = off(nf, dx, dy), off(ff, dx, dy)
        return foal(bod, hd, far=[off(fh, dx, 0) if dy < 0 else fh, ffp] if far_fore_behind else [fh],
                    near=[nh if dy >= 0 else off(nh, dx, 0)] + ([] if over else [nfp]),
                    over=([nfp] if over else []) + ([] if far_fore_behind else [ffp]),
                    tl=(22 + dx, 51 + dy, 50), props_front=props_front)

    # Sitting on the haunches: rump on the ground, hind legs folded forward, forelegs straight down.
    def sit(hdy=0, ears='up', eye='open', mouth='none', look=0, blush=False, fore=None, props_front=(),
            props_mid=()):
        nf, ff = fore or ([(36, 49), (36, ANK)], [(39.5, 48), (40, ANK)])
        f = F()
        f.add(leg(ff, far=True))
        f.add(tail(19, 54, 25))
        f.add(body(28, 51, 35, 11.5, 7.5, neck=[(34, 46), (36, 36 + hdy)]))
        thigh = Layer()
        thigh.fill(ellipse(25, 55, 5.5, 5.2), 'F')
        thigh.fill(minus(ellipse(25, 55, 5.5, 5.2), ellipse(26, 54, 5.5, 5.2)), 'f')
        thigh.outline()
        f.add(leg([(26, 57.5), (30, 59.5)]))
        f.add(thigh)
        for layer in props_mid:
            f.add(layer)
        if not fore:
            f.add(leg(nf))
        for layer in head(38, 27 + hdy, ears, eye, mouth, look, blush):
            f.add(layer)
        for layer in props_front:
            f.add(layer)
        if fore:
            f.add(leg(nf))
        return f.grid()

    def frames():
        a = {}
        a['idle'] = [stand(hdy=1 if (i // 4) % 2 else 0, eye='blink' if i == 13 else 'open') for i in range(16)]

        # A trot: diagonal pairs. Contact, the trailing pair swings forward lifted, contact the other way, and so on.
        def walk(i):
            back = {'nh': [(20, 48), (17.5, ANK)], 'ff': [(38, 47), (36, ANK)],
                    'nf': [(34, 48), (36.5, ANK)], 'fh': [(24, 47), (26.5, ANK)]}
            fwd = {'nh': [(20, 48), (22.5, ANK)], 'ff': [(38, 47), (40.5, ANK)],
                   'nf': [(34, 48), (31.5, ANK)], 'fh': [(24, 47), (21.5, ANK)]}
            if i == 0:
                return stand(legs=back, look=1)
            if i == 2:
                return stand(legs=fwd, look=1)
            if i == 1:
                return stand(hdy=-1, look=1, legs={'nh': lifted((20, 48), 0.5), 'ff': lifted((38, 47), 0.5),
                                                   'nf': [(34, 48), (34.5, ANK)], 'fh': [(24, 47), (24.5, ANK)]})
            return stand(hdy=-1, look=1, legs={'nf': lifted((34, 48), 0.5), 'fh': lifted((24, 47), 0.5),
                                               'nh': [(20, 48), (20.5, ANK)], 'ff': [(38, 47), (38.5, ANK)]})

        a['walk'] = [walk(i) for i in range(4)]

        a['sit'] = [sit(hdy=dy, eye='happy', mouth='smile') for dy in (-1, 0)]

        def fall(i):
            k = 0 if i == 0 else 2
            legs = {'nh': [(20, 48), (15, 54 - k)], 'fh': [(24, 47), (21, 54 + k)],
                    'nf': [(34, 48), (40, 53 - k)], 'ff': [(38, 47), (44, 52 + k)]}
            return stand(dy=-4, ears='tall', eye='wide', mouth='open', legs={k: off(v, 0, -4) for k, v in legs.items()},
                         tail_ang=-25)

        a['fall'] = [fall(0), fall(1)]

        def land(i):
            dy, hdy, e, ey = ((3, 1, 'down', 'squeeze'), (2, 1, 'down', 'squeeze'), (0, 1, 'up', 'open'),
                              (0, 0, 'up', 'open'))[i]
            legs = {k: [(p[0][0], p[0][1] + dy), p[1]] for k, p in STAND.items()}
            return stand(dy=dy, hdy=hdy, ears=e, eye=ey, legs=legs)

        a['land'] = [land(i) for i in range(4)]

        def held(i):
            s = 1 if i == 0 else -1
            fore = ([(36, 42), (42, 42 - 3 * s), (46.5, 41 - 3 * s)], [(39, 39), (45, 40 + 3 * s), (49.5, 39 + 3 * s)])
            hind = ([(27, 50), (24 - s, 54), (23 - 2 * s, 57)], [(31, 49), (33 + s, 53), (34 + 2 * s, 56)])
            return rear(dy=-2, ears='tall', eye='squeeze', mouth='shout', fore=fore, hind=hind, ang=75)

        a['held'] = [held(0), held(1)]

        def happy(i):
            if i % 2 == 1:     # rearing up, as in the reference
                return rear(dy=-1, hdy=0, eye='happy', mouth='open', blush=True)
            return stand(eye='happy', mouth='smile', blush=True, hdy=1)

        a['happy'] = [happy(i) for i in range(4)]

        def hang(i, shimmy=False):
            bk, fr = (16, 6.5), (48, 6.5)
            if shimmy:
                bk, fr = ((14, 7.5), (51, 6.5)) if i == 0 else ((18, 6.5), (46, 7.5))
            sw = 0 if i == 0 else 2
            f = F()
            f.add(leg([(37, 32), (44, 20), fr], far=True))
            f.add(tail(28, 49, 70))
            f.add(leg([(33, 46), (34 + sw, 51), (35 + sw, 54)], far=True))
            f.add(body(32, 40, 85, 11, 7.5))
            f.add(leg([(29, 47), (28 - sw, 52), (27 - sw, 55)]))
            f.add(leg([(27, 32), (20, 20), bk]))
            for layer in head(33 + (1 if shimmy else 0), 22, 'down', 'open', 'none', 1 if shimmy else 0, False):
                f.add(layer)
            return f.grid()

        a['hang'] = [hang(0), hang(1)]
        a['shimmy'] = [hang(0, True), hang(1, True)]

        a['idle_sad'] = [stand(ears='down', eye='droopy', mouth='frown', hdy=2 + i, hdx=-1, tail_ang=30)
                         for i in range(2)]

        def sleep(i):
            f = F()
            f.add(tail(18, 55, 15))
            f.add(body(29, 55, 0, 13, 7))
            f.add(leg([(26, 59), (31, 60.5)], far=True))
            f.add(leg([(36, 58), (40, 59.5)]))
            for layer in head(41, 41 + i, 'down', 'blink', 'none', 0, False):
                f.add(layer)
            f.add(base.zee(52, 14, False) if i == 0 else base.zee(54, 6, True))
            return f.grid()

        a['sleep'] = [sleep(0), sleep(1)]

        def eat(i):
            fore = ([(35, 45), (40, 46), (43.5, 42)], [(38, 44), (44, 45), (48.5, 42)])
            return sit(eye='happy', mouth='munch' if i == 0 else 'smile', blush=True, fore=fore,
                       props_front=[base.apple(48, 37, bitten=i == 1)], props_mid=[leg(fore[1], far=True)])

        a['eat'] = [eat(0), eat(1)]

        # Climbing: reared up against the wall on the right, all four hooves on it (hoof soles at column 63).
        def climb(i, blink=False):
            up_f, low_f = [(49, 36), (52, 34), (56, 34)], [(50, 40), (53, 42), (56, 42)]
            up_h, low_h = [(48, 49), (52, 50), (56, 50)], [(47, 53), (52, 56), (56, 56)]
            nf, ff = (up_f, low_f) if i == 0 else (low_f, up_f)
            nh, fh = (low_h, up_h) if i == 0 else (up_h, low_h)
            f = F()
            f.add(leg(off(ff, 0, 0), far=True)).add(leg(fh, far=True))
            f.add(tail(40, 50, 60))
            f.add(body(46, 43, 80, 11, 7.5))
            f.add(leg(nh))
            for layer in head(39, 21, 'up', 'blink' if blink else 'open', 'none', 1, False):
                f.add(layer)
            f.add(leg(nf))
            return f.grid()

        a['climb'] = [climb(0), climb(1)]
        a['cling'] = [climb(0), climb(0, blink=True)]

        def push(i):
            s = i % 2
            fore = ([(44, 41), (50, 42 + s), (56, 42 + s)], [(45, 37), (50, 37 - s), (56, 37 - s)])
            hind = ([(33, 51), (29 + 2 * s, ANK)], [(37, 50), (35 - 2 * s, ANK)])
            return rear(dx=6, dy=1, hdx=-2, hdy=1 + s, ears='down', eye='squeeze', mouth='pout', fore=fore,
                        hind=([(p[0] - 6, p[1]) for p in hind[0]], [(p[0] - 6, p[1]) for p in hind[1]]), ang=55)

        a['push'] = [push(i) for i in range(4)]

        # On a ledge: sitting on the rump with the seat (the body's bottom outline) on row 46, legs hanging over.
        def ledge(i):
            sw = (0, 2)[i]
            f = F()
            f.add(leg([(36, 40), (38 + sw, 46), (38 + sw, 50)], far=True))
            f.add(tail(24, 40, 70))
            f.add(leg([(33, 43), (32 + sw, 48), (31 + sw, 52)], far=True))
            f.add(body(30, 36, 55, 11, 7.5))
            f.add(leg([(30, 43), (28 - sw, 48), (28 - sw, 52)]))
            f.add(leg([(35, 41), (36 - sw, 46), (35 - sw, 50)]))
            for layer in head(36, 22, 'down', 'happy', 'smile', 0, False):
                f.add(layer)
            return f.grid()

        a['sit_ledge'] = [ledge(0), ledge(1)]

        a['kick'] = [stand(look=1, legs={'nf': lifted((34, 48), 1.5)}),
                     stand(dx=-3, eye='squeeze', mouth='open', look=1, legs={'nf': [(34, 48), (39, 53), (43, 54)]}),
                     stand(dx=-3, eye='happy', mouth='smile', look=1, legs={'nf': [(34, 48), (39, 49), (42, 46)]}),
                     stand(look=1)]

        def game(i):
            fore = ([(35, 47), (40, 51), (44, 50)], [(38, 46), (44, 50), (48, 49)])
            return sit(eye='open', mouth='pout' if i == 0 else 'smile', look=1, hdy=i, fore=fore,
                       props_front=[base.pad(47, 53 + i, i == 1)], props_mid=[leg(fore[1], far=True)])

        a['game'] = [game(0), game(1)]

        crouch = {k: [(p[0][0], p[0][1] + 3), p[1]] for k, p in STAND.items()}
        leap = {'nh': [(20, 48), (16, 53)], 'fh': [(24, 47), (20, 52)],
                'nf': [(34, 48), (39, 52)], 'ff': [(38, 47), (43, 51)]}
        a['bounce'] = [stand(dy=3, legs=crouch, eye='happy', mouth='smile', blush=True, hdy=-1),
                       stand(dy=-5, legs={k: off(p, 0, -5) for k, p in leap.items()}, ears='tall', eye='happy',
                             mouth='open', blush=True, tail_ang=-20)]

        a['love'] = [stand(eye='happy', mouth='smile', blush=True, look=1, hdy=i,
                           props_front=[base.heart(57, 13 - 4 * i, 2.6), base.heart(60, 4 + 8 * i, 1.6)])
                     for i in range(2)]
        return a

    base.head = head
    base.tail = lambda ux, uy: tail(17 + ux, 43 + uy)
    base.frames = frames
