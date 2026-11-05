"""Pastel pixel-art forest backdrop for the demo screenshots, in the pets' style.

Follows the sprite generator's rules (tools/sprites/make_rabbit.py): flat colours with no anti-aliasing, light from the
upper left (a lit upper-left edge and a shaded lower-right edge), and a 1-pixel outline in the sprites' outline colour
K on the nearest things. Distant layers get softer outlines (a darker shade of their own colour) so the pets stand out.
Drawn at half size and upscaled 2x with nearest neighbour, so its pixels are a little chunkier than the pets'.

    python tools/demo/forest.py out.png [width height]   (physical pixels; default 2560x1380, the stage's work area)
"""
import sys

import numpy as np
from PIL import Image

SCALE = 2
K = (0x26, 0x22, 0x2B)  # the sprites' outline colour (PAL['K'] in make_rabbit.py)
OUT = sys.argv[1] if len(sys.argv) > 1 else "forest.png"
PW, PH = (int(sys.argv[2]), int(sys.argv[3])) if len(sys.argv) > 3 else (2560, 1380)
W, H = (PW + SCALE - 1) // SCALE, (PH + SCALE - 1) // SCALE
rng = np.random.default_rng(7)
YY, XX = np.mgrid[0:H, 0:W]
img = np.zeros((H, W, 3), np.uint8)
BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0


# ---------------------------------------------------------------- shapes (boolean masks over the canvas)
def ellipse(cx, cy, rx, ry):
    return ((XX - cx) / rx) ** 2 + ((YY - cy) / ry) ** 2 <= 1.0


def shift(m, dx, dy):
    out = np.zeros_like(m)
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    out[yd, xd] = m[ys, xs]
    return out


def lit(m, d=2):
    """The strip along the upper-left edge, d pixels deep (as in the generator)."""
    return m & ~shift(m, d, d)


def shaded(m, d=2):
    """The strip along the lower-right edge, d pixels deep."""
    return m & ~shift(m, -d, -d)


def ring(m):
    """The 1-pixel outline just outside a shape (4-neighbour, as the generator's Layer.outline)."""
    grown = m | shift(m, 1, 0) | shift(m, -1, 0) | shift(m, 0, 1) | shift(m, 0, -1)
    return grown & ~m


def solid(m, base, light=None, dark=None, line=K, ld=2, sd=2):
    """Paint an outlined part: a flat colour, a light upper-left edge and a dark lower-right edge."""
    img[m] = base
    if light:
        img[lit(m, ld)] = light
    if dark:
        img[shaded(m, sd)] = dark
    if line:
        img[ring(m)] = line


def mix(a, b, t):
    return tuple(round(x + (y - x) * t) for x, y in zip(a, b))


# ---------------------------------------------------------------- sky: posterized bands, ordered dither between them
SKY = [(176, 212, 238), (190, 220, 240), (204, 228, 240), (220, 234, 238), (238, 234, 230), (248, 226, 218),
       (250, 218, 210)]
horizon = H * 0.62
t = np.clip(YY / horizon, 0, 1) * (len(SKY) - 1)
band = np.floor(t).astype(int)
band = np.where(t - band > BAYER[YY % 4, XX % 4], band + 1, band).clip(0, len(SKY) - 1)
img[:] = np.array(SKY, np.uint8)[band]

# The sun, upper left (the light comes from there, like on the sprites), in flat rings.
sx, sy = W * 0.16, H * 0.17
d = np.hypot(XX - sx, YY - sy)
for r, col in ((90, (244, 236, 228)), (66, (250, 240, 226)), (46, (254, 246, 230)), (30, (255, 250, 236))):
    img[d <= r] = mix(img[int(sy), int(sx)], col, 1.0) if r == 30 else col
img[(d <= 30) & lit(d <= 30, 3)] = (255, 254, 246)


def cloud(cx, cy, s):
    puffs = [(0, 0, 34, 20), (-30, 8, 24, 15), (32, 6, 28, 16), (-8, -14, 22, 18), (16, -10, 18, 14), (52, 12, 18, 11),
             (-52, 14, 16, 10)]
    m = np.zeros((H, W), bool)
    for dx, dy, rx, ry in puffs:
        m |= ellipse(cx + dx * s, cy + dy * s, rx * s, ry * s)
    m &= YY <= cy + 16 * s  # flat bottoms
    solid(m, (252, 250, 250), light=(255, 255, 255), dark=(226, 222, 240), line=(196, 192, 222), sd=4)


for cx, cy, s in ((W * 0.42, H * 0.12, 1.0), (W * 0.74, H * 0.2, 1.25), (W * 0.93, H * 0.07, 0.75),
                  (W * 0.28, H * 0.3, 0.6), (W * 0.58, H * 0.33, 0.55)):
    cloud(cx, cy, s)


# ---------------------------------------------------------------- distant hills
def hills(base_y, amp, seed, col, light, line):
    r = np.random.default_rng(seed)
    x = np.arange(W)
    y = base_y + sum(a * np.sin(x / p + r.uniform(0, 6.28)) for a, p in ((amp, 140), (amp * 0.5, 61), (amp * 0.2, 23)))
    m = YY >= np.round(y)[None, :]
    solid(m, col, light=light, line=line, ld=3)


hills(H * 0.50, 26, 1, (206, 198, 228), (220, 214, 238), (188, 180, 214))
hills(H * 0.56, 20, 2, (190, 204, 226), (204, 216, 234), (172, 186, 212))


# ---------------------------------------------------------------- trees
def pine(cx, base, h, w):
    """A pine: stacked tiers, each a triangle with a slightly drooping hem."""
    m = np.zeros((H, W), bool)
    tiers = 4
    for i in range(tiers):
        top = base - h + i * h * 0.2
        bot = top + h * 0.38
        half = w * (0.42 + 0.58 * (i + 1) / tiers) / 2
        frac = np.clip((YY - top) / (bot - top), 0, 1)
        m |= (YY >= top) & (YY <= bot + 2 * np.cos((XX - cx) / max(half, 1) * 1.57)) & (np.abs(XX - cx) <= half * frac)
    return m


def round_tree(cx, base, h, w, seed):
    """A round tree: a short trunk and a canopy of overlapping blobs."""
    r = np.random.default_rng(seed)
    m = np.zeros((H, W), bool)
    cy = base - h * 0.62
    for _ in range(7):
        m |= ellipse(cx + r.uniform(-0.3, 0.3) * w, cy + r.uniform(-0.22, 0.18) * h, w * r.uniform(0.25, 0.36),
                     h * r.uniform(0.22, 0.3))
    return m


def trunk(cx, base, h, w):
    return (np.abs(XX - cx) <= w / 2) & (YY >= base - h) & (YY <= base)


def floor(base, seed, col, line):
    """The forest floor in front of the rows behind: a gently wavy band from base down."""
    r = np.random.default_rng(seed)
    x = np.arange(W)
    y = base - 8 + 5 * np.sin(x / 53 + r.uniform(0, 6.28)) + 3 * np.sin(x / 19 + r.uniform(0, 6.28))
    solid(YY >= np.round(y)[None, :], col, line=line)


def forest_row(base, n, hrange, seed, col, light, dark, line, trunk_cols=None, rounds=0.35):
    r = np.random.default_rng(seed)
    xs = np.sort(r.uniform(-40, W + 40, n))
    for cx in xs:
        h = r.uniform(*hrange)
        b = base + r.uniform(-4, 6)
        is_round = r.random() < rounds
        if trunk_cols:
            th = h * (0.6 if is_round else 0.3)
            solid(trunk(cx, b, th, max(4, h * 0.07)), trunk_cols[0], dark=trunk_cols[1], line=line, sd=2)
        if is_round:
            m = round_tree(cx, b - h * 0.12, h, h * 0.7, int(r.integers(1 << 30)))
        else:
            m = pine(cx, b - h * 0.08, h, h * 0.55)
        solid(m, col, light=light, dark=dark, line=line, ld=3, sd=3)


# Far to near: each row a little greener and more contrasty; only the nearest gets the sprites' black outline.
floor(H * 0.68, 31, (196, 218, 214), None)
forest_row(H * 0.70, 46, (70, 110), 11, (176, 208, 212), (192, 220, 222), None, (160, 192, 200))
floor(H * 0.775, 32, (184, 216, 196), (168, 200, 186))
forest_row(H * 0.79, 34, (100, 150), 12, (160, 204, 186), (182, 222, 200), (140, 186, 172), (118, 162, 154),
           trunk_cols=((196, 170, 170), (172, 146, 150)))

# Light shafts from the upper left, over the far layers: lighten in two flat steps.
shaft = ((XX * 0.55 + YY) % 260 < 46) & (YY < H * 0.86)
img[shaft] = np.minimum(255, img[shaft].astype(int) + 10).astype(np.uint8)

floor(H * 0.885, 33, (176, 214, 170), (150, 192, 154))
forest_row(H * 0.90, 22, (150, 210), 13, (146, 200, 156), (178, 224, 172), (114, 168, 132), (78, 112, 98),
           trunk_cols=((204, 160, 136), (170, 128, 112)), rounds=0.5)


# ---------------------------------------------------------------- big framing trees at the edges (nearest, outlined in K)
def big_tree(cx, seed):
    r = np.random.default_rng(seed)
    base = H
    solid(trunk(cx, base, H * 0.85, 46), (196, 150, 128), light=(220, 180, 156), dark=(160, 116, 100), ld=5, sd=6)
    for i in range(5):  # bark lines
        y0 = H * (0.25 + 0.13 * i)
        img[(np.abs(XX - cx - r.uniform(-12, 12)) <= 1) & (YY >= y0) & (YY <= y0 + 22)] = (160, 116, 100)
    m = np.zeros((H, W), bool)
    for _ in range(11):
        m |= ellipse(cx + r.uniform(-130, 130), r.uniform(-30, H * 0.2), r.uniform(60, 100), r.uniform(44, 70))
    solid(m, (150, 204, 152), light=(186, 228, 176), dark=(116, 172, 128), ld=6, sd=7)
    leaves = m & ~shaded(m, 7) & (BAYER[YY % 4, XX % 4] > 0.9) & (((XX // 9) + (YY // 7)) % 5 == 0)
    img[leaves] = (186, 228, 176)  # a few leaf highlights inside the canopy


big_tree(W * 0.035, 21)
big_tree(W * 0.975, 22)


# ---------------------------------------------------------------- meadow along the bottom (where the pets stand)
ground_top = H - 34
x = np.arange(W)
edge = ground_top + np.round(3 * np.sin(x / 37) + 2 * np.sin(x / 13)).astype(int)
blades = rng.random(W) < 0.22
edge = edge - np.where(blades, rng.integers(2, 6, W), 0)
m = YY >= edge[None, :]
solid(m, (166, 216, 156), light=(196, 234, 180), dark=None, ld=3)
img[m & (YY >= H - 14) & (BAYER[YY % 4, XX % 4] < 0.5)] = (148, 200, 142)  # dithered darker bottom
img[m & (YY >= H - 7)] = (148, 200, 142)


def stamp(px, py, rows, cols):
    for j, row in enumerate(rows):
        for i, ch in enumerate(row):
            if ch != "." and 0 <= px + i < W and 0 <= py + j < H:
                img[py + j, px + i] = cols[ch]


FLOWER = [".K.", "KcK", ".K.", ".g."]
MUSHROOM = ["..KKKK..", ".KrrwrK.", "KrwrrrrK", "KKKKKKKK", "..KwwK..", "..KwwK..", "..KKKK.."]
TUFT = ["g...g", "g.g.g", ".ggg."]
for fx in rng.uniform(4, W - 8, 70).astype(int):
    fy = int(edge[fx]) + int(rng.integers(5, 22))
    c = [(244, 172, 196), (252, 228, 140), (255, 255, 255), (196, 180, 240)][int(rng.integers(4))]
    stamp(fx, fy, FLOWER, {"K": mix(c, K, 0.45), "c": c, "g": (110, 168, 112)})
for fx in rng.uniform(4, W - 8, 90).astype(int):
    stamp(fx, int(edge[fx]) + int(rng.integers(6, 26)), TUFT, {"g": (128, 186, 126)})
for fx in (W * 0.09, W * 0.47, W * 0.885):
    fx = int(fx)
    stamp(fx, H - 16, MUSHROOM, {"K": K, "r": (238, 140, 150), "w": (252, 244, 236)})

# Fireflies / sparkles in the air.
SPARK = [".y.", "yYy", ".y."]
for _ in range(26):
    px, py = int(rng.uniform(40, W - 40)), int(rng.uniform(H * 0.35, H * 0.85))
    stamp(px, py, SPARK, {"y": (250, 236, 170), "Y": (255, 252, 226)})

big = Image.fromarray(img, "RGB").resize((W * SCALE, H * SCALE), Image.NEAREST).crop((0, 0, PW, PH))
big.save(OUT)
print("wrote", OUT, big.size)
