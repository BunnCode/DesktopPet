"""Generates a sprite sheet for another character in the built-in rabbit's style, ready to drop into a pet's folder.

A character is a module in tools/sprites/characters/ with an `apply(base)` function. `base` is make_rabbit (the
built-in rabbit generator); apply() changes only what differs from the rabbit (palette entries, head(), EAR_SETS,
EYES, hoodie(), shorts(), tail(), leg(), ...) by assigning to base's attributes. Everything else (poses, limbs,
props, the frame list, the sheet layout) is reused, so the new character animates exactly like the rabbit.
See docs/CHARACTER_SPRITES.md.

    python tools/sprites/make_character.py <name> [--out folder]

Writes <out>/sheet.png (64x64 cells, one row per animation, same layout as rabbit.png) and <out>/atlas.json.
Copy both into the data folder's pets\\<n>\\sprites\\ (one pet) or sprites\\ (all pets).
"""
import argparse
import importlib
import json
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import make_rabbit as base  # noqa: E402


def atlas(image):
    """atlas.json for a sheet in the generator's layout: one row per animation in ORDER."""
    return {
        'frameWidth': base.CELL,
        'frameHeight': base.CELL,
        'image': image,
        'facesLeft': False,
        'smooth': False,
        'animations': {name: {'row': row} for row, name in enumerate(base.ORDER)},
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('name', help='module name in tools/sprites/characters/')
    ap.add_argument('--out', help='output folder (default: tools/sprites/out/<name>)')
    args = ap.parse_args()

    character = importlib.import_module('characters.' + args.name)
    character.apply(base)

    out = args.out or os.path.join(HERE, 'out', args.name)
    os.makedirs(out, exist_ok=True)
    sheet = base.render(base.frames())
    sheet.save(os.path.join(out, 'sheet.png'))
    with open(os.path.join(out, 'atlas.json'), 'w', encoding='utf-8', newline='\n') as f:
        json.dump(atlas('sheet.png'), f, indent=2)
        f.write('\n')
    print('wrote', os.path.normpath(os.path.join(out, 'sheet.png')), sheet.size, 'and atlas.json')


if __name__ == '__main__':
    sys.exit(main())
