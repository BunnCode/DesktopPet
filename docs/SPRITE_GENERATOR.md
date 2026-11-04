# Sprite generator (`tools/sprites/make_rabbit.py`)

Builds the built-in chibi rabbit sheet, `src/DesktopPet.App/SpriteSheet/rabbit.png`, which is embedded in the exe
and read by `DefaultArt` (see `docs/CODEMAP.md`), and the default furniture art, `src/DesktopPet.App/Furniture/Art/<kind>.png`
(embedded, and copied into the user's `furniture` folder on first run). The PNG is checked in, so the generator only runs when the art
changes. Never hand-edit the PNG: the next run overwrites it.

Other characters in the same style reuse this generator's kit and rig: see `docs/CHARACTER_SPRITES.md`.

The character is based on the user's reference sketches of their own rabbit OC (the latest is a turnaround sheet):
grey fur, both ears up (the back one leaning back a little), a dark fringe swept forward from the crown with a tuft
on top and fur showing on the back of the head, big dark eyes, a light muzzle, cheek fluff, an oversized hoodie
(hood bunched behind the neck, drawstrings, front pocket, ribbed hem) over shorts, digitigrade legs with ankle tufts,
and a fluffy white tail; the sleeves hide the hands when the arms hang down. The sketches are greyscale, so colours
other than the pink inner ears and blush are guesses.

**Style:** chunky chibi sprite style (the user pointed at a classic monster-collecting dungeon game's sheets as the
reference): a big round head about as tall as the body and legs together, a small pear-shaped body, stubby
limbs, bold dark outlines, flat colours with one shade and one light tone, tall oval eyes with a big shine, 3/4 view.

## Running it

```
python tools/sprites/make_rabbit.py                     # writes src/DesktopPet.App/SpriteSheet/rabbit.png
python tools/sprites/make_rabbit.py --out x.png --preview zoom.png   # elsewhere, plus an 8x preview
python tools/sprites/make_rabbit.py --furniture-preview f.png        # plus an 8x preview of all furniture
```
- Needs Python 3 and Pillow. On this machine: `/f/Users/mrsma/anaconda3/python` (Bash) has Pillow; the `py`
  launcher may not. Pass it Windows-style paths (`C:/...`), not `/c/...`. A run takes about 7 seconds.
- `--preview` writes the sheet at 8× on a white/blue checkerboard (4-pixel squares). At 64 px cells the full preview
  is huge: crop a few cells out of the sheet first and preview those (4× is enough for an overview, 8× for faces).
- After changing the art: run it, preview, rebuild the app (the PNG is an EmbeddedResource), and ideally check it
  live. Running it twice with no changes produces a byte-identical PNG (handy for checking that a refactor
  didn't change the output: `cmp`).

## Output contract (what the app relies on)

- 64×64 cells (`CELL`, and `DefaultArt.FrameSize` in the App must match). 16 columns (the most frames any animation
  has: `idle`) × 20 rows, one row per animation in **`ORDER`, which must match `Animations.All` in Core**
  (`DefaultArt` finds the row by the animation's index in `Animations.All`). Frame *n* is column *n*. Each animation
  needs exactly its `AnimationDef.FrameCount` frames; unused cells stay transparent.
- The pet's default size is 64 DIP (`PetConfig.SizeDip`), so one art pixel is one DIP. Furniture is drawn the same
  way, at one art pixel = 1 DIP.
- Fully transparent background (alpha 0) or fully opaque pixels only. Transparent pixels are click-through.
- Art faces right; the feet sit on the bottom row (row 63, the foot outline) of the cell.
- Pose conventions from `docs/SPRITES.md`: `climb`/`cling`/`push` hands (and climbing feet) reach column 63;
  `hang`/`shimmy` hands grip row 0; `sit_ledge` has its seat at row 46 (18/64 = 9/32 of the cell above the bottom,
  `Animations.LedgeSinkFraction`) with the legs below.
- **Hoodie colours `J`, `j`, `k` must not appear anywhere else**: `DefaultArt` recolours them per pet by exact
  ARGB match against `JacketColors.Variants[0]` in Core. If you change them in `PAL`, change that entry too.

## How it's built

The rabbit is drawn from **shapes**, not typed pixel by pixel: each part is a `Layer` (a 64×64 grid of palette keys)
filled from shape tests, auto-outlined in `K`, and painted onto the frame back to front. Because every part has its
own outline, overlapping parts (an arm across the body, the front leg over the back one) stay readable. Small
details (eyes, mouths, nose) are ASCII stamps. Furniture is built the same way, from `Part`s (see below).

### Palette (`PAL`)
| Key | Use | Key | Use |
|---|---|---|---|
| `K` | outline | `E` / `w` / `e` | eye / eye shine / iris glint |
| `F` / `f` / `W` | fur / fur shade (and the far leg) / muzzle, tail | `B` | blush |
| `H` / `h` | hair / hair shine | `N` | nose |
| `P` | inner ear (pink) | `M` / `T` | open mouth / tongue |
| `J` / `j` / `k` | hoodie / shade, cuffs, hem / light (recoloured per pet) | `S` | drawstrings, bed sheet |
| `s` / `g` | shorts / shade | `Z` | snooze "z" |
| `R` / `r` / `L` | apple / apple shine / leaf | `X` | anger mark (unused) |
| `G` / `V` | grey plastic / dark screen, mat, d-pad | `O` / `o` | wood / wood shade (also the apple stem) |
| `C` / `c` | couch fabric / shade | `U` / `u` | blanket / shade |
| `Y` | yellow (game pixels) | `Q` / `q` | heart / heart shine |
| `A` / `a` | egg shell / speckles and shade | `D` / `d` | gravestone / shade |
| `x` | apple and ball shade | `I` / `i` | couch light / wood light |
| `n` | blanket light | `b` / `v` | grey plastic light / shade |
| `l` / `m` | leaf light / shade (grass, greens) | | |

### Drawing helpers
- Shapes are predicates `(x, y) -> bool` on pixel centres: `ellipse(cx, cy, rx, ry)`, `polygon(points)`,
  `stroke(path, radii)` (a polyline with a radius per point, tapering along each segment), and the combinators
  `both(a, b)` and `minus(a, b)`.
- `Layer.fill(shape, key, only=None)` paints a key where the shape holds (with `only`, just over those keys, e.g. a
  shine on the hair). `Layer.stamp(rows, x, y)` paints an ASCII part. `Layer.outline()` adds `K` around everything.
- `F().add(layer)...grid()` composes a frame; later layers are in front.

### Head (`head(cx, cy, ears, eye, mouth_kind, look, blush)`)
Returns `[ears layer, head layer]` for a head centred at `(cx, cy)`; standing, that's `(HX, HY) = (32, 30)`.
Standing layout: ears y 2–21, head y 18–42, hoodie y 38–51, shorts 48–54, hips at y 53, feet on row 63.
- Skull: a 29×25 ellipse, cheek fluff at the back and front of the jaw, a shade crescent at the lower back (`f`),
  the muzzle (`W` ellipse below the eyes, right of centre).
- Hair: a polygon whose lower edge is three chunky locks swept forward (above the back eye, a big one between the
  eyes, one at the front temple), clipped to a slightly larger skull, plus a cowlick on the crown and a shine arc.
  Move the polygon's points to restyle it.
- `ears` (`EAR_SETS`: base offset from the head centre, angle from vertical, length, half width): `'up'` (default,
  the back ear leaning back), `'tall'` (straight up: falling, held, bouncing), `'down'` (flopped back: sad, sleep,
  landing, pushing, hanging, the ledge). `ear()` builds a leaf shape with a pink inner stripe; ears are drawn
  behind the head, with a shaded back edge and a pale tuft at the base. Lengths keep the tips inside row 0.
- `eye` (`EYES`, one 5×7 stamp used for both eyes; `squeeze` is mirrored for the front eye): `open`, `blink`,
  `happy` (^ ^), `wide` (white eyes, small pupils), `squeeze` (> <), `droopy`, `angry` (unused). `look=1` shifts
  the eyes, nose and mouth a pixel forward.
- `mouth_kind` (`MOUTHS`): `none` (a small rabbit "w"), `smile`, `open`, `shout`, `frown`, `pout`, `munch`.
- `blush=True` adds a pink dash under each eye.

### Body
- `hoodie(ux, uy)`: pear-shaped body polygon, shaded back, lit front, ribbed hem, front pocket, the hood rolled
  round the neck with its dark inside showing under the chin, drawstrings with tips. `shorts(ux, uy, sit)`:
  standing (legs split at the bottom) or seated (thighs forward). `tail(ux, uy)`: a puff of circles at the back hip.
- `arm(path, paw)`: a chubby sleeve (radius 3) along the path with a wide cuff; `paw=True` adds a paw past the cuff.
  Hanging arms (the default, from `SHOULDER_BACK`/`SHOULDER_FRONT`) have no paw: the hands are in the sleeves, as
  in the sketch. Arms given as paths show paws.
- `leg(path, fur)`: a stubby digitigrade leg along five joints, **hip, knee (forward), hock (back, off the ground),
  ball, toe**, tapering from the thigh to the foot, with spiky fur at the back of the hock. The far leg uses
  `fur='f'`.
  `LEGS` holds the (back, front) pairs in standing coordinates: `stand`, `stride`/`stride2` (walk contact poses),
  `pass`/`pass2` (one foot lifted), `dangle`, `kick` (fluttering), `crouch` (landing, bounce squash),
  `kick_out`/`kick_up`. Feet rest with the ball and toe at `FOOT` (y 60.6), so the outline lands on row 63. `SIT_LEGS` is the
  seated pair (knees up, feet flat in front).
- `rabbit(...)` assembles a whole pet: `ux, uy` move the upper body (tail, shorts, hoodie, default arms, head),
  `hdx, hdy` the head on top of that, `lx, ly` the legs; `legs` picks a `LEGS` pair; `arms` gives (back, front)
  paths in cell coordinates instead of hanging arms; `sit=True` uses the seated shorts and legs; `props_mid` are
  drawn over the hoodie, `props_front` over everything (hearts, z's, a held apple or controller);
  `front_arm_last=True` draws the right arm over `props_front` (holding something in front of the face).
  Order: far (right) arm, tail, legs, shorts, hoodie, `props_mid`, near (left) arm, ears, head, `props_front`. In
  the 3/4 view the pet faces right and toward the viewer, so the image's left side is nearer: the left arm goes over
  the body and the right one behind it (the other way round looks like the arms are on backwards). Both go under
  the head because the shoulders sit below the chin.

### Props
`apple(x, y, bitten)`, `heart(x, y, r)`, `pad(x, y, pressed)` (game controller, buttons swap colour), `zee(x, y, big)`.

### Frames (`frames()`)
Returns `{animation id: [grid, ...]}`, one short block per animation:

| Animation | Frames | How it's drawn |
|---|---|---|
| `idle` | 16 | standing; head down 1 px on frames 4–7 and 12–15 (breathing); blink on 13 |
| `walk` | 4 | stride, pass2 (the trailing back foot swings forward), stride2, pass (the trailing front foot swings forward); head bobs on the passing frames. Lifting the foot that's already ahead reads as walking backwards |
| `sit` | 2 | seated (upper body down 8), happy eyes, head sways 1 px |
| `fall` | 2 | tall ears, wide eyes, open mouth, arms flung out, legs dangling / fluttering |
| `land` | 4 | crouched with the head low and ears down, squeezed eyes, then back up |
| `held` | 2 | tall ears, > < eyes, shouting, arms flailing, legs kicking |
| `happy` | 4 | alternates a smiling stand with a hop (body up 2 px, arms up, open mouth); blush |
| `hang` / `shimmy` | 2 / 2 | arms behind the head up to paws at row 0, ears down, legs dangling; shimmy moves the hands |
| `idle_sad` | 2 | ears down, droopy eyes, frown, head lowered 2–3 px |
| `sleep` | 2 | seated, ears down, eyes closed, a "z" growing and rising |
| `eat` | 2 | apple held at the mouth in both paws (front arm over the apple), munch / smile with a bitten apple |
| `climb` / `cling` | 2 / 2 | built by hand: body pressed against the wall (shifted 15 px right) so limbs of their normal length reach it, head turned back 5 px so the raised arm misses the face; diagonal pairs alternate (back hand + front foot up, then front hand + back foot up), paws and the raised foot at column 63; cling holds frame 0 and blinks. Don't stretch limb paths to reach the wall: long limbs look spidery |
| `push` | 4 | ears down, straining face, both arms straight out to column 63, legs striding |
| `sit_ledge` | 2 | built by hand: upper body raised so the shorts' bottom (the seat) is row 46, legs hanging and swinging, ears down |
| `kick` | 4 | front foot lifted, leg kicked out (> < eyes), leg up in follow-through, standing |
| `game` | 2 | seated, paws on a controller in front, head bobbing, buttons swapping colour |
| `bounce` | 2 | crouched with happy eyes, then stretched up with arms flung wide and tall ears |
| `love` | 2 | standing, happy eyes and blush looking ahead, head bobbing; two hearts float in front of the face |

### Furniture (`FURNITURE`)
Each kind is a function returning a list of frames (character grids), saved as a horizontal strip by `render_strip`.
1 art pixel = 1 DIP, so the sizes below match `DefaultFurniture` / `Eggs.Spec` / `Graves.Spec` in Core exactly, and so
do the seat heights (`SeatDip`: the couch seat, bed mattress and trampoline mat tops are 18 px up, drawn 16 DIP up so the
pet sinks in a little). Pieces are built like the rabbit: each part is a `Part` (a grid of palette keys) filled from
shapes, and `solid(w, h, shape, base, light, dark)` makes an outlined part with a light tone on the upper-left edge
(`lit`) and a shade on the lower-right edge (`shaded`); `compose` paints parts back to front. Shape helpers beyond the
rabbit's: `rect`, `rrect` (rounded rectangle), `union`, `shifted`.

| Kind | Art px | Frames |
|---|---|---|
| `ball` | 16×16 | 4 (a white band turning 45° a frame while the light stays put; plays while moving) |
| `tv` | 48×40 | 3 (off, then a two-frame game with a tiny pet hopping; plays while in use) |
| `couch` | 80×36 | 1 (seat top row 18) |
| `trampoline` | 64×20 | 1 (mat top row 2) |
| `bed` | 80×32 | 1 (mattress top row 14) |
| `playpen` | 112×40 | 1 (posts, rails and a hung sign reading "GAY BABY JAIL" in a 3×5 font; the inside is transparent) |
| `trough` | 48×20 | 1 |
| `pill` | 20×12 | 1 |
| `egg` | 24×26 | 4 (still, still, top leaning left, leaning right; built in, not written to the furniture folder; `Eggs.Spec` in Core) |
| `grave` | 64×60 | 1 (headstone, plinth, grass, two flowers; built in, `Graves.Spec` in Core). The face (x 14–49, y 10–43) stays plain: the app's `GraveArt` writes the name and dates there (its `FaceLeft/Top/Width/Height`), so keep the two in step |

## Common edits

- **Tweak a pose:** edit its block in `frames()` (offsets, arm paths, which `LEGS` pair) or add a `LEGS` pair.
- **Change the look everywhere:** edit `head()` (skull, hair polygon, muzzle), `EAR_SETS`, `EYES`, `hoodie()`,
  `shorts()`, `leg()` or `PAL`; every frame picks it up.
- **New expression:** add an entry to `EYES` (front and back stamp, 8 rows) or `MOUTHS`.
- **New animation:** add it to Core's `Animations` first (see `docs/CODEMAP.md` recipes), then append its id to
  `ORDER` in the same position as in `Animations.All` and add a block in `frames()` producing exactly
  `FrameCount` grids. If it has more than 16 frames, raise `cols` in `render()`; `DefaultArt` accepts any sheet
  at least 16 rows × the largest frame count wide.
- **Checking bounds:** after a pose change, check that standing feet reach row 63, ear tips don't cross row 0, and
  the climb/push/hang/ledge conventions above still hold (a few lines of Pillow over the sheet will do).

## Known weak spots

- Seated poses hide most of the hoodie behind the big head.
- The hood only shows as a small bump at the back of the neck.
