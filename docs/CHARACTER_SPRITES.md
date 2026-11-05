# Making sprites for a new character

How the built-in rabbit's sprites were made, written down so more characters can be made the same way from new
reference art. The style stays the same across characters; everything about the character (species, head shape,
ears, face, hair, clothing, colours, height, build) can change.

Tools (all in `tools/sprites/`, Python 3 + Pillow; on this machine `/f/Users/mrsma/anaconda3/python`, with
Windows-style `C:/...` paths):
- `make_rabbit.py`: the built-in rabbit, and the drawing kit and pose rig every character reuses
  (`docs/SPRITE_GENERATOR.md` documents its parts).
- `make_character.py <name>`: renders `characters/<name>.py` into `out/<name>/sheet.png` + `atlas.json`.
- `characters/template.py`: the starting point for a new character (lists everything that can be overridden).
- `sprite_preview.py`: `overview`, `zoom` and `check` for any generated sheet.

## The style (keep this for every character)

- **Chunky chibi sprite style**, 3/4 view facing right, 64×64 cells, feet on row 63. The user's reference for the
  look was a classic monster-collecting dungeon game's sprite sheets: copy the *style* only, never a design from it.
- **Proportions:** a big round head, about as tall as the body and legs together; a small, rounded (pear-shaped)
  body; short, chubby limbs. Ears, horns or hair may stick up above the head but must stay inside the cell (ear tips
  no higher than row 1 in the jumping frames).
- **Lines and colour:** every part outlined in the dark `K` (each part outlined separately, so overlaps stay
  readable); flat colours with one shade and one light tone per material; the shade on the lower back side, light
  on the front. Light pure-white only for shines.
- **Face:** tall dark oval eyes (5×7) with a big white shine, set low and wide on the face; a small nose and a tiny
  mouth; blush only for happy and love poses. Expressions are stamps, so every character gets the same set.
- **Limbs:** never longer than the standing limbs. To reach something (a wall, a bar), move the body closer
  instead of stretching the arms or legs; stretched limbs look spidery. Hands at the sides can be hidden in
  sleeves.
- **Depth in the 3/4 view:** the character faces right *and toward the viewer*, so its **near** side is the
  image's **left**. The left arm is the near arm, drawn over the body; the right arm is the far arm, drawn behind
  the body and mostly hidden. Drawing the right-hand arm on top makes the arms look on backwards. Both arms go under
  the head (the shoulders sit below the chin); only an arm holding a prop out in front is drawn over everything.
- **Animation:** the rabbit's poses are the reference. A walk lifts the *trailing* foot (lifting the leading one
  reads as walking backwards).

## The process

1. **Read the reference.** Note what defines the character: silhouette (head shape, ears, tail), face (eye shape,
   muzzle, markings), hair, clothing layers, colours (greyscale sketches: guess, then ask), height and build, and any
   notes on the sheet (the rabbit's said "hand hidden when arm is lowered"). Note what won't fit at 64 px and say so.
2. **Start the character file:** copy `characters/template.py` to `characters/<name>.py` and set the palette.
   Run `python tools/sprites/make_character.py <name>` and look at it (step 5) before going further.
3. **Override the parts, biggest silhouette first:** head shape and ears (`head`, `ear`, `EAR_SETS`), then
   clothing (`hoodie`, `shorts`; rename freely inside your file, but keep the function names the rig calls), then
   tail, legs, face stamps. Write shapes with the kit (`Layer.fill` with `ellipse`, `polygon`, `stroke`, `both`,
   `minus`; `Layer.outline`); small details are ASCII stamps (`Layer.stamp`). Reuse the rabbit's functions as the
   pattern: copy one into your file, change it, assign it to `base`.
4. **Different height or build:** change the layout together: `HX, HY` (head centre), `SHOULDER_BACK/FRONT`, the
   hips (first point of each `LEGS` path), `FOOT`, and the body polygons. Then copy `frames()` into your file,
   retune the arm paths and offsets for the new layout, and assign `base.frames = frames`. Keep the pose meaning
   of every animation (`docs/SPRITES.md` lists them).
   **Four-legged characters** may stand on all fours instead of using the upright rig: write a small rig and
   `frames()` in the character file from the kit's shapes (see `redmuzzle.py`, `carycajolt.py`,
   `emberdonkey.py`, `courceel.py`), and rear up on the hind legs for the poses that need hands (climb, cling,
   push, hang, shimmy, held, eat, game). The frame counts and the `check` conventions stay the same.
5. **Look, fix, repeat.** After every change:
   ```
   python tools/sprites/make_character.py <name> --out <tmp>
   python tools/sprites/sprite_preview.py overview <tmp>/sheet.png <tmp>/overview.png     # every pose, 4x
   python tools/sprites/sprite_preview.py zoom <tmp>/sheet.png <tmp>/zoom.png 0,0 1,1 6,1 12,0   # faces, 8x
   ```
   Judge the face at 8× first (it decides whether it's cute), then the overview for consistency. Common fixes from
   the rabbit: eyes too small; hair reading as a helmet (use fewer, chunkier locks); legs hidden under the clothes
   (shorten the top); an arm drawn over the face; arms that look on backwards (the right-hand arm drawn over the
   body: see "Depth"); stray-looking small parts (make them bigger and rounder).
6. **Check the conventions:** `python tools/sprites/sprite_preview.py check <tmp>/sheet.png` must say `ok`
   (feet on row 63, climb/cling/push hands at column 63, hang/shimmy hands at row 0, nothing clipped at row 0), and
   check by eye that the `sit_ledge` seat is on row 46.
7. **Show the user** the overview and a zoom, and iterate on their notes.
8. **Use it:** copy `out/<name>/sheet.png` and `atlas.json` into `data\pets\<n>\sprites\`
   (that pet only) or `...\sprites\` (every pet). The pet reloads them within a second. Custom sheets aren't
   recoloured per pet (only the built-in rabbit's hoodie is), so the colours in the file are what shows.
   To make a character the new **built-in** art instead, put its changes into `make_rabbit.py` itself and follow
   `docs/SPRITE_GENERATOR.md` (keep the `J`/`j`/`k` outfit colours unique and in step with `JacketColors` in Core).

Commit the character file (`characters/<name>.py`); `out/` is generated and ignored.
