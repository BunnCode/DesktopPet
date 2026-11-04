# Custom sprites

DesktopPet draws every animation with its built-in chibi rabbit unless you give it your own. Custom art lives in
`%LOCALAPPDATA%\DesktopPet\sprites\` (menu → Customize → Open data folder) and **reloads as soon as you save**.

## Quick start
1. Menu → Customize → **Export sprite template**. This writes `template.png` (the built-in rabbit art, one row per
   animation, 32×32 cells) and a matching `atlas.json` to `sprite-template\`.
2. Copy both files into `sprites\`.
3. Paint over the frames and save. The pet updates within a second.

**One pet only:** put the files in that pet's own folder instead, `pets\<n>\sprites\` (menu → About *name* → Open its
folder). Per animation, the pet's own sprites win over the shared `sprites\`, which win over the built-in rabbit. Each
atlas keeps its own `facesLeft` and `smooth`, so the layers can mix different art.

Any animation you remove from `atlas.json` goes back to the built-in art, so you can replace one animation at a time.
Problems (a missing image, a frame outside the image, an unknown animation name) show up as a notification. The full
list goes to `problems.txt` in the data folder, and the affected animations use the built-in art.

## atlas.json
```jsonc
{
  "frameWidth": 32,         // cell size in pixels (default 32×32); can be overridden per animation
  "frameHeight": 32,
  "image": "sheet.png",     // default image, relative to the sprites folder
  "facesLeft": false,       // true if your art is drawn facing left
  "smooth": false,          // true for painted art (smooth scaling); false keeps pixel art crisp
  "animations": {
    "idle": { "row": 0 },                                   // frames along row 0 from column 0
    "walk": { "row": 1, "column": 2, "frames": 4 },         // start at column 2
    "fall": { "image": "fall.png", "frameWidth": 48 }       // its own image and cell width
  }
}
```
- Frames run left to right along `row`, starting at `column`. Both count from 0.
- `frames` defaults to what the game plays. With fewer frames the strip cycles; extra frames are ignored.
- Comments and trailing commas are allowed. Animation names are case-insensitive.
- Frames are scaled uniformly into the pet's square box (`SizeDip` in settings), aligned to the bottom centre.

## Animations

| Name | Frames | What it is |
|---|---|---|
| `idle` | 16 | Standing, breathing; a blink near the end |
| `idle_sad` | 2 | Standing while unhappy |
| `walk` | 4 | Walking |
| `sit` | 2 | Sitting on the floor |
| `sit_ledge` | 2 | Sitting on a window's title bar, legs hanging over |
| `sleep` | 2 | Asleep |
| `eat` | 2 | Eating |
| `happy` | 4 | After being clicked; also used while playing |
| `fall` | 2 | Falling or flying |
| `land` | 4 | Landing squash (plays once) |
| `held` | 2 | Dangling from the cursor |
| `climb` | 2 | Climbing a wall |
| `cling` | 2 | Holding still on a wall |
| `push` | 4 | Pushing a window |
| `hang` | 2 | Hanging from a title bar (off by default) |
| `shimmy` | 2 | Moving along a title bar (off by default) |
| `kick` | 4 | Kicking a ball (plays once per kick) |
| `game` | 2 | Sitting with a controller, playing games at the TV |
| `bounce` | 2 | Bouncing on the trampoline or the couch |
| `love` | 2 | Courting another pet: gazing at it (it's on the side the art faces), hearts floating up |

## Conventions
- **Facing:** draw facing right, or set `"facesLeft": true`. The game mirrors the art when the pet turns.
- **Feet** at the bottom centre of the cell: the bottom edge of the cell is the ground.
- **climb, cling, push:** the hands (and feet, for climbing) touch the side of the cell the art faces. That's where the
  wall is.
- **hang, shimmy:** the hands grip the top edge of the cell.
- **sit_ledge:** the seat is 9/32 of the cell height above the bottom (row 23 of 32). The legs hang into the bottom
  part.
- **Transparency:** pixels that are fully transparent are click-through; anything else catches the mouse. Keep the
  background fully transparent, not "almost" transparent.
