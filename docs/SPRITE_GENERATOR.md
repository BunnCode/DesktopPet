# Sprite generator (`tools/sprites/make_rabbit.py`)

Builds the built-in chibi rabbit sheet, `src/DesktopPet.App/SpriteSheet/rabbit.png`, which is embedded in the exe
and read by `DefaultArt` (see `docs/CODEMAP.md`), and the default furniture art, `src/DesktopPet.App/Furniture/Art/<kind>.png`
(embedded, and copied into the user's `furniture` folder on first run). The PNG is checked in, so the generator only runs when the art
changes. Never hand-edit the PNG: the next run overwrites it.

The character is based on the user's reference sketches of their own rabbit OC: grey fur, dark swept bangs with a
tuft at the back, one ear up and one angled forward, big dark eyes, dark collared jacket over a light shirt. The
sketches are greyscale, so colours other than the pink inner ears and blush are guesses.

## Running it

```
python tools/sprites/make_rabbit.py                     # writes src/DesktopPet.App/SpriteSheet/rabbit.png
python tools/sprites/make_rabbit.py --out x.png --preview zoom.png   # elsewhere, plus an 8x preview
python tools/sprites/make_rabbit.py --furniture-preview f.png        # plus an 8x preview of all furniture
```
- Needs Python 3 and Pillow. On this machine: `/f/Users/mrsma/anaconda3/python` (Bash) has Pillow; the `py`
  launcher may not.
- `--preview` writes the sheet at 8× on a white/blue checkerboard (4-pixel squares), which is the easiest way to
  check the art with the Read tool. Crop it first (for example to the first 4 columns) to keep images small.
- After changing the art: run it, preview, rebuild the app (the PNG is an EmbeddedResource), and ideally check it
  live. Running it twice with no changes produces a byte-identical PNG (handy for checking that a refactor
  didn't change the output: `cmp`).

## Output contract (what the app relies on)

- 32×32 cells. 16 columns (the most frames any animation has: `idle`) × 20 rows, one row per animation in
  **`ORDER`, which must match `Animations.All` in Core** (`DefaultArt` finds the row by the animation's index
  in `Animations.All`). Frame *n* is column *n*. Each animation needs exactly its `AnimationDef.FrameCount` frames;
  unused cells stay transparent.
- Fully transparent background (alpha 0) or fully opaque pixels only. Transparent pixels are click-through.
- Art faces right; the feet sit on the bottom row (row 31) of the cell.
- Pose conventions from `docs/SPRITES.md`: `climb`/`cling`/`push` hands (and climbing feet) touch columns 30–31;
  `hang`/`shimmy` hands grip row 0; `sit_ledge` has its seat at row 23 with the legs below.
- **Jacket colours `J`, `j`, `k` must not appear anywhere else**: `DefaultArt` recolours them per pet by exact
  ARGB match against `JacketColors.Variants[0]` in Core. If you change them in `PAL`, change that entry too.

## How it's built

Everything is ASCII art: each character is a palette key, `.` is transparent. A frame is a 32×32 grid of
characters, painted back to front and converted to RGBA at the end.

### Palette (`PAL`)
| Key | Use | Key | Use |
|---|---|---|---|
| `K` | outline | `E` / `w` | eye / eye shine |
| `F` / `f` / `W` | fur / fur shade / light muzzle | `B` | blush |
| `H` / `h` | hair / hair highlight | `N` | nose |
| `P` | inner ear (pink) | `M` / `T` | open mouth / tongue |
| `J` / `j` / `k` | jacket / shade / highlight (recoloured per pet) | `S` | shirt |
| `R` / `r` / `L` | apple / apple shine / leaf | `Z` | snooze "z" |
| `X` | anger mark (unused so far) | `G` / `V` | grey plastic / dark screen, mat |
| `O` / `o` | wood / wood shade | `C` / `c` | couch fabric / shade |
| `U` / `u` | blanket / shade | `Y` | yellow (game pixels) |
| `Q` / `q` | heart / heart shine | `A` / `a` | egg shell / speckles and shade |
| `D` / `d` | gravestone / shade | | |

### Helpers
- `grid(rows)` pads rows to equal width. `blank()` is an empty 32×32 grid.
- `stamp(canvas, part, x, y)` paints a part with its top-left at (x, y), skipping `.` and clipping to the cell.
- `line`, `limb(path, fill='J', end='F', end_size=2)`: a 2-px-thick limb along a polyline of (x, y) points in
  cell coordinates, with a square paw at the last point, auto-outlined in `K`. Returns a full-cell layer.
  Use `fill='F'` for legs. `stamp_layer` paints such a layer.
- `class F`: a frame being built. `F().part(part, x, y).limb(path).grid()`. Calls paint back to front, so order
  is depth: things drawn later are in front.

### Head (`head(ears, eye, mouth_kind, look, blush)`)
Returns a 22×22 part: 9 ear rows (`EAR_ROWS`) plus the 13-row `HEAD` (hair, bangs, tuft, face fur, muzzle).
- `ears`: `'up'` (`EARS_UP`, default: back ear upright, front ear angled forward), `'tall'` (`EARS_TALL`: both
  straight up, for falling or being held), `'down'` (`EARS_DOWN`: flopped back, tips hanging behind the head;
  used for sad, sleep, landing, pushing, hanging, the ledge). Ear parts are drawn **behind** the head (only into
  transparent pixels), so `EARS_DOWN` may be taller than 9 rows.
- `eye` (`eyes()`): `open`, `blink`, `happy` (^ ^), `wide`, `squeeze` (> <), `droopy`, `angry`. `look=1` shifts
  the pupils one pixel forward (walking, climbing).
- `mouth_kind` (`mouth()`): `none` (a dot), `smile`, `open`, `shout`, `frown`, `pout`, `munch`.
- `blush=True` adds two pink cheek dashes.
- Feature anchors in part coordinates: `EYE_BACK (7, 15)`, `EYE_FRONT (14, 15)`, `NOSE (19, 18)`,
  `MOUTH (17, 19)`. Move these if you reshape `HEAD`.

### Bodies
- `TORSO_ARMS_DOWN` (16×8, arms at the sides) and `TORSO` (no arms; add arms with `limb`).
- `LEGS` (16×3 each, bottom row = ground): `stand`, `wide` (stride), `lift` (front foot raised), `dangle`, `kick`,
  `back` (back leg only; the kicking leg is a limb).
- `SIT` (16×9): seated, legs out to the right.
- Standard placement: body at `x = BX = 8`, bottom-aligned (`y = 32 - height`); head at `(HX, HY) = (5, 0)`, so
  the head bottom (row 21) overlaps the torso's collar. `standing(head_part, legs, torso, head_dy, head_dx)` does
  that. A positive `head_dy` lowers the head (squash, breathing).

### Props
`APPLE`, `APPLE_BITTEN` (eat), `Z_SMALL`, `Z_BIG` (sleep), `PAD`, `PAD_PRESSED` (game controller).

### Frames (`frames()`)
Returns `{animation id: [grid, ...]}`. One block per animation, each a short local function:

| Animation | Frames | How it's drawn |
|---|---|---|
| `idle` | 16 | standing; head down 1 px on frames 4–7 and 12–15 (breathing); blink on 13 |
| `walk` | 4 | legs wide / lift / stand / lift, head bobs up on the lift frames, eyes look ahead |
| `sit` | 2 | `SIT` body, happy eyes, head sways 1 px |
| `fall` | 2 | tall ears, wide eyes, open mouth, arms flung out, feet dangle/kick |
| `land` | 4 | head lowered 5, 3, 1, 0 px (squash), ears down and squeezed eyes on the first two |
| `held` | 2 | tall ears, > < eyes, shouting, arms flailing, feet kicking |
| `happy` | 4 | alternates a low smile pose with a hop (body up 1 px, arms up, open mouth); blush |
| `hang` / `shimmy` | 2 / 2 | arms from shoulders to hands at row 0 (drawn behind the head), ears down, feet swing; shimmy moves the hands |
| `idle_sad` | 2 | ears down, droopy eyes, frown, head lowered 1–2 px |
| `sleep` | 2 | sitting, ears down, eyes closed, a "z" growing and rising |
| `eat` | 2 | apple at the mouth held by both arms, munch / smile with a bitten apple |
| `climb` / `cling` | 2 / 2 | body shifted right, one arm up past the face and one low, feet on the wall, all ending at column 30; cling holds frame 0 and blinks |
| `push` | 4 | ears down, straining face, both arms straight out to column 30, legs shuffling |
| `sit_ledge` | 2 | short torso ending at row 23, legs dangling below and swinging, head raised (ears down so they fit) |
| `kick` | 4 | front foot lifted, leg kicked out (> < eyes, open mouth), leg up in follow-through, standing |
| `game` | 2 | `SIT` body, arms to a controller in front, head bobbing, buttons swapping colour |
| `bounce` | 2 | squashed with happy eyes, then stretched with arms flung wide and tall ears |
| `love` | 2 | standing, happy eyes and blush looking ahead, head bobbing; a `HEART` and `HEART_SMALL` float in front of the face |

### Furniture (`FURNITURE`)
Each kind is a function returning a list of frames (character grids of any size), drawn with `fill` and `border`
then `outlined` (K into transparent neighbours), and saved as a horizontal strip by `render_strip`. 1 art pixel =
2 DIP, so the sizes must match `DefaultFurniture` in Core (DIP = art px × 2), and so must the seat heights (`SeatDip`:
the couch seat, bed blanket and trampoline mat tops are 9 px up, drawn 16 DIP up so the pet sinks in a little).

| Kind | Art px | Frames |
|---|---|---|
| `ball` | 8×8 | 4 (white band rolling round; plays while moving) |
| `tv` | 24×20 | 3 (off, then a two-frame game; plays while in use) |
| `couch` | 40×18 | 1 |
| `trampoline` | 32×10 | 1 |
| `bed` | 40×16 | 1 |
| `playpen` | 56×20 | 1 (posts and rails; the inside is transparent) |
| `trough` | 24×10 | 1 |
| `egg` | 12×13 | 4 (still, still, top half tipped left, tipped right; built in, not written to the furniture folder; `Eggs.Spec` in Core) |
| `grave` | 32×30 | 1 (headstone, plinth, grass, two flowers; built in, `Graves.Spec` in Core). The face (x 7–24, y 5–21) stays plain: the app's `GraveArt` writes the name and dates there, so keep the two in step |

## Common edits

- **Tweak a pose:** edit its block in `frames()` (offsets, limb paths, which head variant).
- **Change the look everywhere:** edit `HEAD`, the ear parts, the torsos or `PAL`; every frame picks it up.
- **New expression:** add a `kind` to `eyes()` or `mouth()` (points are relative to the anchors).
- **New animation:** add it to Core's `Animations` first (see `docs/CODEMAP.md` recipes), then append its id to
  `ORDER` in the same position as in `Animations.All` and add a block in `frames()` producing exactly
  `FrameCount` grids. If it has more than 16 frames, raise `cols` in `render()`; `DefaultArt` accepts any sheet
  at least 16 rows × the largest frame count wide.
- **Bigger cells:** `CELL` is used throughout the script, but `DefaultArt.FrameSize`, the parts and all pixel
  offsets assume 32. Treat it as a redraw, not a setting.

## Known weak spots

- `climb`/`cling` are the roughest frames (32 px leaves little room for reaching limbs).
- Sitting hides most of the jacket behind the big head.
- The `angry` eyes and `X` anger mark exist but aren't used by any animation yet.
