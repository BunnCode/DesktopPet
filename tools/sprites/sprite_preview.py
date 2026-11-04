"""Previews and checks for a generated sprite sheet (64x64 cells, one row per animation in make_rabbit.ORDER).

    python tools/sprites/sprite_preview.py overview SHEET OUT.png          one frame of each pose, 4x
    python tools/sprites/sprite_preview.py zoom SHEET OUT.png ROW,COL ...   chosen cells side by side, 8x
    python tools/sprites/sprite_preview.py check SHEET                      bounds and the pose conventions

ROW and COL count from 0; the row is the animation's index in ORDER (idle 0, walk 1, sit 2, fall 3, land 4,
held 5, happy 6, hang 7, shimmy 8, idle_sad 9, sleep 10, eat 11, climb 12, cling 13, push 14, sit_ledge 15,
kick 16, game 17, bounce 18, love 19).
"""
import os
import sys

from PIL import Image

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import make_rabbit as base  # noqa: E402

C = base.CELL
OVERVIEW = [(0, 0), (1, 1), (2, 0), (3, 0), (4, 0), (5, 0), (6, 1), (7, 0), (9, 0),
            (10, 0), (11, 0), (12, 0), (14, 0), (15, 0), (16, 1), (17, 0), (18, 1), (19, 0)]


def board(sheet, cells, per_row):
    gap = C + 2
    rows = (len(cells) + per_row - 1) // per_row
    out = Image.new('RGBA', (gap * min(per_row, len(cells)), gap * rows))
    for i, (r, c) in enumerate(cells):
        out.paste(sheet.crop((c * C, r * C, c * C + C, r * C + C)), ((i % per_row) * gap, (i // per_row) * gap))
    return out


def opaque(sheet, r, c):
    return [(x, y) for y in range(C) for x in range(C) if sheet.getpixel((c * C + x, r * C + y))[3]]


def check(sheet):
    """Prints each animation's bounds and whether the conventions the app relies on hold. Returns the failures."""
    from_core = {'idle': 16, 'walk': 4, 'sit': 2, 'fall': 2, 'land': 4, 'held': 2, 'happy': 4, 'hang': 2,
                 'shimmy': 2, 'idle_sad': 2, 'sleep': 2, 'eat': 2, 'climb': 2, 'cling': 2, 'push': 4,
                 'sit_ledge': 2, 'kick': 4, 'game': 2, 'bounce': 2, 'love': 2}
    grounded = {'idle', 'walk', 'sit', 'land', 'idle_sad', 'sleep', 'eat', 'push', 'kick', 'game', 'love'}
    fails = []
    for r, name in enumerate(base.ORDER):
        for c in range(from_core[name]):
            px = opaque(sheet, r, c)
            if not px:
                fails.append(f'{name} {c}: empty')
                continue
            top, bottom = min(y for _, y in px), max(y for _, y in px)
            right = max(x for x, _ in px)
            print(f'{name:10} {c:2}  top {top:2}  bottom {bottom:2}  right {right:2}')
            if name in grounded and bottom != C - 1:
                fails.append(f'{name} {c}: feet should reach row {C - 1} (bottom is {bottom})')
            if name in ('climb', 'cling', 'push') and right != C - 1:
                fails.append(f'{name} {c}: hands should reach column {C - 1} (right is {right})')
            if name in ('hang', 'shimmy') and top != 0:
                fails.append(f'{name} {c}: hands should reach row 0 (top is {top})')
            if name not in ('hang', 'shimmy') and any(y == 0 and sheet.getpixel((c * C + x, r * C))[:3] !=
                                                      base.PAL['K'] for (x, y) in px):
                fails.append(f'{name} {c}: something other than an outline touches row 0 (clipped ear tip?)')
    print('ok' if not fails else '\n'.join(['PROBLEMS:'] + fails))
    print('sit_ledge: check by eye that the seat (the bottom outline of the shorts) is on row 46.')
    return fails


def main():
    cmd, sheet_path = sys.argv[1], sys.argv[2]
    sheet = Image.open(sheet_path).convert('RGBA')
    if cmd == 'overview':
        base.preview(board(sheet, OVERVIEW, 9), sys.argv[3], scale=4)
    elif cmd == 'zoom':
        cells = [tuple(int(v) for v in a.split(',')) for a in sys.argv[4:]]
        base.preview(board(sheet, cells, len(cells)), sys.argv[3], scale=8)
    elif cmd == 'check':
        return 1 if check(sheet) else 0
    else:
        print(__doc__)
        return 2
    return 0


if __name__ == '__main__':
    sys.exit(main())
