---
name: character-sprites
description: Make a pixel-art sprite sheet for a new Deskling character from reference art, in the built-in rabbit's chibi style. Use when the user shares a character reference (sketch, turnaround) and wants sprites, or wants to change an existing character's sprites.
---

# Character sprites

Follow `docs/CHARACTER_SPRITES.md` (the style rules and the step-by-step process). In short:

1. Read the reference and list what defines the character; say what won't fit at 64 px.
2. Copy `tools/sprites/characters/template.py` to `characters/<name>.py`; override palette, head, ears, face,
   clothing, legs, and (for a different height or build) the layout and `frames()`.
3. Loop: `make_character.py <name> --out <scratch>`, then `sprite_preview.py zoom` (faces, 8x) and `overview`
   (all poses), read the PNGs, fix. Keep the style rules: never stretch limbs; the image-left arm is the near one
   (over the body), the right one is behind it; a walk lifts the trailing foot.
4. `sprite_preview.py check` must say `ok`; check the `sit_ledge` seat (row 46) by eye.
5. Show the user previews and iterate. Then tell them where to copy `sheet.png` + `atlas.json`
   (`pets\<n>\sprites\` or the shared `sprites\`).

Use `/f/Users/mrsma/anaconda3/python` (it has Pillow) with Windows-style `C:/...` paths. New art reuses the
generator's kit; don't write separate image code.
