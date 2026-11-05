"""Carycajolt: an electric caracal-like cat (the user's reference art, by zebraknight1).

Defining features: dark navy fur with bright yellow patches (a face mask, the insides of the ears, the chest, the
lower legs with dark stripes, the paws); long pointed caracal ears with dark tufts on the tips; orange spiky fur
tufts sticking out along the back, the back of the head, the elbows and hocks; a long navy tail with yellow bands
and a sharp yellow tip; pale icy eyes with slit pupils and a small orange diamond on the forehead.
It walks on all fours, so this file has its own quadruped rig (a chibi cat: big head, a short bean body, short chunky
legs) instead of the rabbit's upright one. It rears up on its hind legs to climb, hang, push, eat and play.
Won't fit at 64 px: the thin spindly legs and the many long spikes of the reference (kept as a few chunky tufts),
the cross inside the forehead diamond, the claws, the shadow-like after-image.

    python tools/sprites/make_character.py carycajolt
"""


def apply(base):
    Layer, F, ellipse, polygon, stroke, both, minus, math = (base.Layer, base.F, base.ellipse, base.polygon,
                                                             base.stroke, base.both, base.minus, base.math)
    base.PAL.update({
        'F': (0x3A, 0x40, 0x6E),   # fur: dark navy
        'f': (0x2C, 0x30, 0x54),   # navy shade (and the far legs)
        'W': (0x56, 0x5E, 0x96),   # navy light
        'Y': (0xFA, 0xD0, 0x3A),   # yellow patches
        'y': (0xD8, 0x9A, 0x22),   # yellow shade
        't': (0xFF, 0xEC, 0x94),   # yellow light
        'z': (0xF2, 0x80, 0x2C),   # orange tufts, forehead diamond
        'p': (0xC0, 0x56, 0x1E),   # orange shade
        '1': (0xD6, 0xF4, 0xF6),   # pale icy eyes
        'E': (0x18, 0x16, 0x26),   # eye rim and pupil
        'N': (0x24, 0x20, 0x34),   # nose
    })
    G = base.grid
    base.EYES.update({
        # Dark eyes with a big shine, the pale icy iris glowing in the lower half.
        'open': G([".EEE.", "EwwEE", "EwwEE", "EEEEE", "E1E1E", "E111E", ".EEE."]),
        'wide': G([".EEE.", "E1w1E", "E111E", "E1E1E", "E111E", ".EEE.", "....."]),
        'droopy': G([".....", ".....", "KKKKK", "EwwEE", "E1E1E", "E111E", ".EEE."]),
        'angry': G(["K....", ".KK..", "EEEKK", "EwwEE", "E1E1E", "E111E", ".EEE."]),
    })
    base.MOUTHS.update({
        'none': G(["K.K.K", ".KwK."]),        # a cat "w" with a little fang
        'smile': G(["K...K", ".KwK."]),
    })
    FOOT = 60.6

    # ------------------------------------------------------------------ head
    # Ears: (base dx, dy from the head centre, angle from up, length, half width). The tufts add 5 px.
    EARS = {
        'up': [(-7, -8, -16, 12.5, 5.4), (5, -9, 14, 12.5, 5.4)],
        'tall': [(-6, -8, -6, 11.5, 5.0), (5, -8, 6, 11.5, 5.0)],
        'down': [(-9, -5, -78, 13, 5.0), (8, -8, 64, 12, 5.0)],
    }

    def ear(L, bx, by, ang, ln, hw):
        a = math.radians(ang)
        dx, dy = math.sin(a), -math.cos(a)
        nx, ny = math.cos(a), math.sin(a)
        P = lambda u, v: (bx + dx * u + nx * v, by + dy * u + ny * v)
        outer = polygon([P(-2, -hw), P(ln * 0.45, -hw * 0.85), P(ln, 0), P(ln * 0.45, hw * 0.85), P(-2, hw)])
        tuft = polygon([P(ln - 2, -1.1), P(ln + 5, -0.3), P(ln - 2, 1.1)])
        inner = polygon([P(0, -hw * 0.5), P(ln * 0.75, 0), P(0, hw * 0.5)])
        L.fill(tuft, 'f')
        L.fill(outer, 'F')
        L.fill(minus(outer, lambda x, y: outer(x + nx * 1.2, y + ny * 1.2)), 'f')     # shade on the back edge
        L.fill(inner, 'Y')
        L.fill(both(inner, lambda x, y: (x - bx) * nx + (y - by) * ny > hw * 0.15), 't')

    def head(cx, cy, ears='up', eye='open', mouth_kind='none', look=0, blush=False):
        e = Layer()
        # Orange spikes at the back of the head and neck.
        for (x0, y0, x1, y1, x2, y2) in ((-11, -2, -18, -3, -12, 3), (-12, 3, -19, 6, -11, 8)):
            e.fill(polygon([(cx + x0, cy + y0), (cx + x1, cy + y1), (cx + x2, cy + y2)]), 'z')
        for spec in EARS[ears]:
            bx, by, ang, ln, hw = spec
            ear(e, cx + bx, cy + by, ang, ln, hw)
        e.outline()

        h = Layer()
        skull = ellipse(cx, cy, 14, 12.2)
        h.fill(skull, 'F')
        # Yellow cheek ruffs: spiky at the back of the jaw, a tuft at the front.
        h.fill(polygon([(cx - 11, cy + 1), (cx - 17, cy + 3), (cx - 13, cy + 5.5), (cx - 16.5, cy + 8.5),
                        (cx - 11, cy + 8), (cx - 9, cy + 10)]), 'Y')
        h.fill(polygon([(cx + 11, cy + 5), (cx + 16.5, cy + 7), (cx + 12, cy + 8.5), (cx + 15, cy + 10.5),
                        (cx + 9, cy + 10)]), 'Y')
        # The yellow face mask: everything below the brows, with the navy coming down to a point between the eyes.
        mask = polygon([(cx - 16, cy - 1), (cx - 12, cy - 4.5), (cx - 7, cy - 5.5), (cx - 2.5, cy - 4),
                        (cx + 0.5, cy + 1.5), (cx + 3.5, cy - 4), (cx + 8, cy - 5.5), (cx + 13, cy - 4),
                        (cx + 17, cy), (cx + 17, cy + 14), (cx - 16, cy + 14)])
        h.fill(both(skull, mask), 'Y')
        h.fill(minus(lambda x, y: h.g[y][x] == 'Y', ellipse(cx + 2.5, cy - 2.5, 15, 13.5)), 'y')   # shade
        h.fill(minus(lambda x, y: h.g[y][x] == 'F', ellipse(cx + 2, cy - 2, 14.5, 12.5)), 'f')
        h.fill(both(skull, minus(ellipse(cx + 3, cy - 8, 8, 3.2), ellipse(cx + 3, cy - 7, 8, 3.2))), 'W')
        h.fill(ellipse(cx + 4.5, cy + 7, 5, 3), 't')                                          # muzzle
        h.outline()
        h.stamp([".z.", "zzz", ".z."], cx - 1 + look, cy - 6)                                 # forehead diamond
        sq = base.EYES[eye]
        front = [r[::-1] for r in sq] if eye == 'squeeze' else sq
        h.stamp(sq, cx - 7 + look, cy - 1).stamp(front, cx + 5 + look, cy - 1)
        h.stamp(["NNN", ".N."], cx + 4 + look, cy + 5)
        m = base.MOUTHS[mouth_kind]
        h.stamp(m, cx + 5 + look - len(m[0]) // 2, cy + 7)
        if blush:
            h.stamp(["BBB"], cx - 8, cy + 6).stamp(["BBB"], cx + 9, cy + 6)
        return [e, h]

    # ------------------------------------------------------------------ body, limbs, tail
    def body(R, C, rr, rc, seat=None):
        """A bean-shaped body from the rump R to the chest C, orange spikes along the back, a yellow chest."""
        L = Layer()
        shape = stroke([R, C], [rr, rc])
        if seat is not None:                                # flat bottom (sitting on a ledge)
            shape = both(shape, lambda x, y, _s=seat: y <= _s)
        ln = math.hypot(C[0] - R[0], C[1] - R[1]) or 1
        dx, dy = (C[0] - R[0]) / ln, (C[1] - R[1]) / ln
        nx, ny = dy, -dx                                    # the back (top) side
        for t in (0.1, 0.45, 0.8):
            px, py = R[0] + (C[0] - R[0]) * t, R[1] + (C[1] - R[1]) * t
            r = rr + (rc - rr) * t
            L.fill(polygon([(px + nx * (r - 2) - dx * 2.2, py + ny * (r - 2) - dy * 2.2),
                            (px + nx * (r + 4.5) - dx * 3.5, py + ny * (r + 4.5) - dy * 3.5),
                            (px + nx * (r - 2) + dx * 1.8, py + ny * (r - 2) + dy * 1.8)]), 'z')
        L.fill(shape, 'F')
        L.fill(minus(shape, lambda x, y: shape(x + nx * 2, y + ny * 2)), 'f')                 # belly side in shade
        L.fill(minus(shape, lambda x, y: shape(x - nx * 1.2, y - ny * 1.2)), 'W')             # light along the back
        cx_, cy_ = C[0] + dx * rc * 0.35 - nx * rc * 0.3, C[1] + dy * rc * 0.35 - ny * rc * 0.3
        chest = both(shape, ellipse(cx_, cy_, rc * 0.7, rc * 0.85))
        L.fill(chest, 'Y')
        L.fill(both(chest, ellipse(cx_ + 1, cy_ - 1.5, rc * 0.35, rc * 0.4)), 't')
        L.outline()
        return L

    def limb(path, far=False, hind=False, socks=True):
        """A short chunky leg: navy upper part, yellow lower part with a dark stripe, a yellow paw (flat on the
        ground when the last point is at foot level) and an orange tuft at the elbow or hock. socks=False keeps
        the lower part navy with a yellow stripe (raised legs, so they don't read as ears)."""
        L = Layer()
        navy, yel, sh, org = ('f', 'y', 'f', 'p') if far else ('F', 'Y', 'y', 'z')
        (rx, ry), (jx, jy), (ex, ey) = path
        flat = ey >= FOOT - 0.01
        radii = (4.4, 2.9, 2.2) if hind else (3.2, 2.8, 2.2)
        # Tuft at the joint, pointing backwards.
        L.fill(polygon([(jx + 0.5, jy - 2.2), (jx - (4.5 if hind else 3.6), jy - 0.5), (jx + 0.5, jy + 1.6)]), org)
        L.fill(stroke(path, list(radii)), navy)
        lower = stroke([(jx, jy), (ex, ey)], [radii[1], radii[2]])
        L.fill(lower, yel if socks else navy)
        sx, sy = jx + (ex - jx) * 0.5, jy + (ey - jy) * 0.5
        ln = math.hypot(ex - jx, ey - jy) or 1
        ux, uy = (ex - jx) / ln, (ey - jy) / ln
        L.fill(both(lower, lambda x, y: abs((x - sx) * ux + (y - sy) * uy) <= 0.6), navy if socks else yel)
        if flat:
            paw = ellipse(ex + 0.8, FOOT, 3.2, 2.0)
        else:
            paw = ellipse(ex, ey, 2.5, 2.5)
        L.fill(paw, yel)
        L.fill(minus(paw, lambda x, y: paw(x + 1, y - 1)), sh if not far else 'f')
        L.outline()
        return L

    def tail(path):
        """A long navy tail with yellow bands, ending in a sharp yellow spike."""
        L = Layer()
        n = len(path)
        radii = [2.5 - 1.2 * i / (n - 1) for i in range(n)]
        shape = stroke(path, radii)
        (ax, ay), (bx, by) = path[-2], path[-1]
        ln = math.hypot(bx - ax, by - ay) or 1
        dx, dy = (bx - ax) / ln, (by - ay) / ln
        nx, ny = -dy, dx
        spike = polygon([(bx + nx * 1.9 - dx, by + ny * 1.9 - dy), (bx + dx * 6.5, by + dy * 6.5),
                         (bx - nx * 1.9 - dx, by - ny * 1.9 - dy)])
        L.fill(shape, 'F')
        L.fill(spike, 'Y')
        L.fill(both(spike, lambda x, y: (x - bx) * nx + (y - by) * ny < 0), 'y')
        for i in range(1, n - 1, 1):                         # yellow bands at the inner joints
            px, py = path[i]
            qx, qy = path[i + 1]
            l2 = math.hypot(qx - px, qy - py) or 1
            vx, vy = (qx - px) / l2, (qy - py) / l2
            L.fill(both(shape, lambda x, y, px=px, py=py, vx=vx, vy=vy: 0.2 <= (x - px) * vx + (y - py) * vy <= 1.6),
                   'Y')
        L.outline()
        return L

    def mv(path, dx, dy):
        return [(x + dx, y + dy) for (x, y) in path]

    # ------------------------------------------------------------------ the rig
    HX, HY = 40, 29
    # Standing: (far back, far front, near back, near front) leg paths (root, joint, paw); paws on FOOT are flat.
    STAND = ([(22, 49), (21, 55.5), (22.5, FOOT)], [(37, 49), (37, 55.5), (37.5, FOOT)],
             [(17, 50), (15.5, 56), (17, FOOT)], [(32, 50), (32, 56), (32.5, FOOT)])
    TAIL_UP = [(11, 45), (6, 42), (4, 36), (5, 30), (8, 25)]
    TAIL_DOWN = [(11, 46), (6, 48), (4, 53), (6, 57), (10, 59)]

    def leg_at(path, dx=0, lift=0, dy=0):
        """A standing leg path with its paw moved dx forward and lifted, and its root moved dy (crouching)."""
        (rx, ry), (jx, jy), (ex, ey) = path
        return [(rx, ry + dy), (jx + dx * 0.5 + (dy * 0.4), jy + dy * 0.6 - lift * 0.5), (ex + dx, ey - lift)]

    def cat(ux=0, uy=0, hdx=0, hdy=0, ears='up', eye='open', mouth_kind='none', look=0, blush=False,
            legs=None, tail_path=None, R=(17, 47), C=(31, 46), rr=8, rc=7.5, props_mid=(), props_front=(),
            front_last=False, head_xy=None):
        """A whole cat. legs = (far back, far front, near back, near front) paths in cell coordinates; the upper
        body (rump R, chest C, head) moves by ux, uy; the head by hdx, hdy on top of that."""
        fb, ff, nb, nf = legs or STAND
        f = F()
        f.add(limb(ff, far=True)).add(limb(fb, far=True, hind=True))
        f.add(tail(mv(tail_path or TAIL_UP, ux, uy)))
        f.add(body((R[0] + ux, R[1] + uy), (C[0] + ux, C[1] + uy), rr, rc))
        f.add(limb(nb, hind=True))
        for layer in props_mid:
            f.add(layer)
        if not front_last:
            f.add(limb(nf))
        hx, hy = head_xy or (HX + ux + hdx, HY + uy + hdy)
        for layer in head(hx, hy, ears, eye, mouth_kind, look, blush):
            f.add(layer)
        for layer in props_front:
            f.add(layer)
        if front_last:
            f.add(limb(nf))
        return f

    def stand_legs(moves=((0, 0), (0, 0), (0, 0), (0, 0)), dy=0):
        return tuple(leg_at(p, dx, lift, dy) for p, (dx, lift) in zip(STAND, moves))

    def frames():
        a = {}
        a['idle'] = [cat(hdy=1 if (i // 4) % 2 else 0, eye='blink' if i == 13 else 'open').grid() for i in range(16)]

        # Diagonal pairs: contact, the trailing pair swings forward (lifted), contact the other way, and so on.
        # Order of moves: far back, far front, near back, near front.
        walk = [((2, 0), (-2, 0), (-2, 0), (2, 0)), ((0, 0), (0, 2), (0, 2), (0, 0)),
                ((-2, 0), (2, 0), (2, 0), (-2, 0)), ((0, 2), (0, 0), (0, 0), (0, 2))]
        a['walk'] = [cat(legs=stand_legs(m), hdy=-1 if i % 2 else 0, look=1).grid() for i, m in enumerate(walk)]

        # Sitting: rump on the ground, front legs straight, tail curled round on the floor.
        SIT = ([(25, 53), (28, 57), (31, FOOT)], [(37, 47), (37, 54), (37.5, FOOT)],
               [(20, 54), (23, 58), (27, FOOT)], [(33, 47), (33, 54), (33.5, FOOT)])
        SIT_TAIL = [(15, 58), (10, 59), (7, 56), (6, 51), (8, 46)]
        sitk = dict(R=(21, 53), C=(30, 43), rr=8.5, rc=6.5, legs=SIT, tail_path=SIT_TAIL)
        a['sit'] = [cat(eye='happy', mouth_kind='smile', hdy=1 + dy, **sitk).grid() for dy in (-1, 0)]

        def fall(i):
            l = 0 if i == 0 else 3
            legs = ([(22, 49), (18, 54 - l), (15, 57 - l)], [(37, 49), (43, 52 - l), (47, 54 - l)],
                    [(17, 50), (12, 54 + l), (8, 56 + l)], [(32, 50), (37, 55 + l), (41, 58 + l)])
            return cat(ears='tall', eye='wide', mouth_kind='open', legs=legs)

        a['fall'] = [fall(0).grid(), fall(1).grid()]

        a['land'] = [cat(uy=dy, legs=stand_legs(dy=dy), hdy=hdy, ears=e, eye=ey).grid()
                     for (dy, hdy, e, ey) in ((3, 2, 'down', 'squeeze'), (2, 1, 'down', 'squeeze'),
                                              (0, 1, 'up', 'open'), (0, 0, 'up', 'open'))]

        def held(i):
            s = 1 if i == 0 else -1
            legs = ([(22, 49), (19, 54 + 2 * s), (16, 57 + 2 * s)], [(37, 49), (43, 52 - 2 * s), (47, 53 - 3 * s)],
                    [(17, 50), (13, 54 - 2 * s), (10, 56 - 3 * s)], [(32, 50), (37, 55 + 2 * s), (40, 59 + s)])
            return cat(ears='tall', eye='squeeze', mouth_kind='shout', legs=legs, tail_path=TAIL_DOWN)

        a['held'] = [held(0).grid(), held(1).grid()]

        def happy(i):
            if i % 2 == 1:
                u = -3
                legs = ([(22, 46), (19, 51), (17, 55)], [(37, 46), (40, 51), (42, 55)],
                        [(17, 47), (14, 52), (12, 56)], [(32, 47), (35, 52), (37, 56)])
                return cat(uy=u, hdy=1, eye='happy', mouth_kind='open', blush=True, legs=legs)
            return cat(eye='happy', mouth_kind='smile', blush=True, hdy=1)

        a['happy'] = [happy(i).grid() for i in range(4)]

        # Hanging from the title bar by the front paws (reared up, front legs behind the head), hind legs dangling.
        def hang(i, shimmy=False):
            back, front = (14, 2.5), (50, 2.5)
            if shimmy:
                back, front = ((12, 2.5), (52, 2.5)) if i == 0 else ((16, 2.5), (48, 2.5))
            sw = 2 if i == 1 else 0
            f = F()
            f.add(limb([(37, 33), (47, 20), front], far=True, socks=False))
            f.add(limb([(36, 50), (38 - sw, 55), (38 - sw, 59)], far=True, hind=True))
            f.add(tail([(29, 54), (24, 57), (19, 56), (16, 52), (15, 47)]))
            f.add(body((32, 49), (32, 36), 7.5, 7))
            f.add(limb([(29, 50), (28 + sw, 55), (29 + sw, 59.5)], hind=True))
            f.add(limb([(27, 34), (17, 20), back], socks=False))
            for layer in head(32 + (1 if shimmy else 0), 22, 'down', 'open', 'none', 1 if shimmy else 0, False):
                f.add(layer)
            return f.grid()

        a['hang'] = [hang(0), hang(1)]
        a['shimmy'] = [hang(0, True), hang(1, True)]

        a['idle_sad'] = [cat(ears='down', eye='droopy', mouth_kind='frown', hdy=2 + i, tail_path=TAIL_DOWN).grid()
                         for i in range(2)]

        # Asleep: lying as a loaf, the head resting on the front paws, tail wrapped round the front.
        def sleep(i):
            f = F()
            f.add(body((20, 54.5), (34, 54.5), 7.5, 7.5))
            f.add(limb([(20, 56), (23, 58.5), (26, FOOT)], hind=True))
            f.add(tail([(13, 55), (10, 58), (14, 59.5), (21, 60), (27, 59.5)]))
            f.add(limb([(34, 57), (40, 59), (44, FOOT)]))
            for layer in head(42, 43 + i, 'down', 'blink', 'none', 0, False):
                f.add(layer)
            f.add(base.zee(51, 16, False) if i == 0 else base.zee(53, 8, True))
            return f.grid()

        a['sleep'] = [sleep(0), sleep(1)]

        # Eating: sitting up with the apple held at the mouth in both front paws.
        def eat(i):
            legs = (SIT[0], [(37, 46), (45, 47), (52, 44)], SIT[2], [(33, 46), (41, 49), (48, 46)])
            return cat(eye='happy', mouth_kind='munch' if i == 0 else 'smile', blush=True, hdy=1,
                       **dict(sitk, legs=legs), props_front=[base.apple(52, 40, bitten=i == 1)], front_last=True)

        a['eat'] = [eat(0).grid(), eat(1).grid()]

        # Climbing a wall on the right: reared up against it, paws (and the raised hind paw) at column 63.
        def climb(i, blink=False):
            up_f = [(51, 40), (55, 34), (60, 29)]
            low_f = [(53, 42), (57, 45), (60, 46)]
            up_h = [(51, 50), (56, 50), (60, 49)]
            low_h = [(47, 53), (53, 57), (60, 58)]
            near_f, far_f = (up_f, low_f) if i == 0 else (low_f, up_f)
            near_h, far_h = (low_h, up_h) if i == 0 else (up_h, low_h)
            f = F()
            f.add(limb(mv(far_f, 0, 1), far=True)).add(limb(far_h, far=True, hind=True))
            f.add(tail([(44, 54), (39, 57), (35, 56), (33, 52), (33, 47)]))
            f.add(body((48, 52), (51, 39), 7.5, 7))
            f.add(limb(near_h, hind=True))
            for layer in head(40, 28, 'up', 'blink' if blink else 'open', 'none', 1, False):
                f.add(layer)
            f.add(limb(near_f))
            return f.grid()

        a['climb'] = [climb(0), climb(1)]
        a['cling'] = [climb(0), climb(0, blink=True)]

        # Pushing: leaning into the wall on the right, both front paws pressed on it at column 63, hind legs striding.
        def push(i):
            s = i % 2
            legs = ([(33, 50), (32 - 2 * s, 56), (34 - 3 * s, FOOT)], [(46, 46), (53, 45 + s), (60, 44 + s)],
                    [(28, 51), (25 + 2 * s, 56), (24 + 3 * s, FOOT)], [(44, 49), (52, 51), (60, 51 - s)])
            return cat(ux=12, uy=0, R=(17, 47), C=(31, 44), hdx=-8, hdy=2 + s, ears='down', eye='squeeze',
                       mouth_kind='pout', legs=legs, tail_path=mv(TAIL_UP, 2, 0))

        a['push'] = [push(i).grid() for i in range(4)]

        # Sitting on a title bar: the rump's bottom outline (the seat) on row 46, legs hanging over the edge.
        def ledge(i):
            sw = (0, 2)[i]
            f = F()
            f.add(limb([(35, 36), (37, 45), (38 + sw, 53)], far=True))
            f.add(limb([(29, 41), (33, 46), (34 - sw, 54)], far=True, hind=True))
            f.add(tail([(19, 42), (15, 46), (14, 52), (16, 56)]))
            f.add(body((24, 38), (31, 28), 8.5, 6, seat=45))
            f.add(limb([(29, 41), (33, 44.5), (34 - sw, 54)], hind=True))
            f.add(limb([(32, 35), (33, 45), (34 + sw, 53)]))
            for layer in head(39, 19, 'down', 'happy', 'smile', 0, False):
                f.add(layer)
            return f.grid()

        a['sit_ledge'] = [ledge(0), ledge(1)]

        # Kicking a ball: batting it with the near front paw.
        nf = STAND[3]
        a['kick'] = [cat(legs=stand_legs(((0, 0), (0, 0), (0, 0), (0, 2))), look=1).grid(),
                     cat(legs=STAND[:3] + ([(32, 50), (39, 53), (46, 53)],), eye='squeeze', mouth_kind='open',
                         look=1).grid(),
                     cat(legs=STAND[:3] + ([(32, 49), (38, 46), (43, 41)],), eye='happy', mouth_kind='smile',
                         look=1).grid(),
                     cat(look=1).grid()]
        del nf

        def game(i):
            legs = (SIT[0], [(37, 46), (44, 51), (49, 53 + i)], SIT[2], [(33, 46), (39, 52), (43, 54 + i)])
            return cat(eye='open', mouth_kind='pout' if i == 0 else 'smile', look=1, hdy=1 + i,
                       **dict(sitk, legs=legs), props_front=[base.pad(46, 55 + i, i == 1)], front_last=True).grid()

        a['game'] = [game(0), game(1)]

        legs_up = ([(22, 46), (17, 49), (13, 51)], [(37, 46), (43, 48), (48, 49)],
                   [(17, 47), (12, 51), (8, 53)], [(32, 47), (37, 51), (41, 54)])
        a['bounce'] = [cat(uy=3, legs=stand_legs(dy=3), eye='happy', mouth_kind='smile', blush=True, hdy=1).grid(),
                       cat(uy=-3, legs=legs_up, ears='tall', eye='happy', mouth_kind='open', blush=True,
                           hdy=1).grid()]

        a['love'] = [cat(eye='happy', mouth_kind='smile', blush=True, look=1, hdy=i,
                         props_front=[base.heart(58, 13 - 4 * i, 2.3), base.heart(60, 4 + 7 * i, 1.4)]).grid()
                     for i in range(2)]
        return a

    base.head = head
    base.frames = frames
