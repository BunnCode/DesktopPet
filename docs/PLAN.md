# DesktopPet: Architecture & Plan

A Shimeji-style desktop pet for Windows, built like a small 2D game: a fixed-timestep loop, a behavior state machine
and sprite animation, in C# / .NET 10 / WPF.

**The one hard rule:** all game logic lives in `DesktopPet.Core`, a plain `net10.0` library with no WPF or Win32
references. The desktop (monitors, work areas, other windows, cursor, lock state) reaches it only as plain data, and it
answers with plain data (what to draw, which windows to move). Every physics, platform and behavior rule is
unit-tested against a fake desktop.

This document describes the design **as built**, the status of each milestone, and what's left.

---

## Status

| # | Milestone | Status | Commit |
|---|---|---|---|
| M0 | Scaffolding: solution, projects, CsWin32, PerMonitorV2 manifest, VS Code tasks | Done | `7aac71a` |
| M1 | Transparent, click-through, animated sprite window; fixed 60 Hz loop; tray icon | Done | `7bac8c4` |
| M2 | Gravity, landing on the taskbar, respawn safety net | Done | `e08d9c1` |
| M3 | Behavior state machine, walking, walls, multi-monitor and mixed DPI | Done | `23de77a` |
| M4 | Drag and throw, wall bounces, skids | Done | `e5c3be3` |
| M5 | Windows as platforms, riding windows, debug overlay | Done | `6291c79` |
| M6 | Hunger/happiness/energy stats, care menu, saving | Done | `6485fc9` |
| M7 | Climbing window sides and screen edges, pushing windows, title-bar perch | Done | `e19ea8d` |
| M8 | Settings file and real sprite sheets (live reload) | Done | |
| M9 | Multiple pets | Done | `634aa82` |
| — | Default art: chibi pixel rabbit replaces the placeholder blob | Done | |
| M10 | Per-pet traits: own folder and sprites, names, neediness, personality | Done | `976c95a` |
| M11 | Furniture, defined in user-editable files | Done | `0bcd1d0` |
| M12 | Speech bubbles and sticky notes (two behaviors) from a shared local LLM (Qwen3.5-2B) | Done | |
| M12b | Reading on-screen text and referring to it when talking or writing notes | Planned | |
| M13 | Breeding: eggs that hatch into mixed children | Planned | |
| M14 | Death and gravestones | Planned | |

424 unit tests, all in `DesktopPet.Core.Tests`.

### Changes from the original plan

- **Order:** drag and throw (M4) came before window platforms (M5), so the pet could be dropped onto windows by hand
  while building M5.
- **Solution file** is `DesktopPet.slnx` (the .NET 10 default), not `.sln`.
- **Input** isn't passed into `Tick`; the host calls `PointerDown/Move/Up/Cancel` between ticks.
- **Stats popup** became live stat bars at the top of the pet's menu.
- **Hanging from title bars** (maximized windows, windows pinned to the screen top) is built and tested but switched
  off by default (`PetConfig.EnableHanging = false`), at the user's request.
- **Climbing** covers the outside faces of window sides and the outer screen edges. Hanging from window *bottoms*
  wasn't built.
- **Pushing windows** is deliberately rare: a window side only counts when it reaches down to the surface the pet is
  walking on (at the user's request, kept as is).
- **Window moves are guarded** by a hard limit (`WindowMoveGuard`) so the pet can never push a window off screen.
  This wasn't in the original plan.
- **The away rule** is stricter than "cap the catch-up": no stat can drop to critical while the user is away (screen
  locked, PC asleep, app closed).
- **M10 traits live in `traits.json`, not in `SaveData`.** The app rewrites `save.json` every minute, so hand edits
  there would race with autosaves. `traits.json` is only written by the app when a pet is created; the save stays
  stats only.
- **M10 name list** was compiled by hand: ssa.gov refuses automated downloads, so the popular half is the top 50 girls'
  and top 50 boys' names of recent years from memory, and the rare half is 100 old-fashioned names standing in for the
  bottom of the list. It's easy to swap (`PetNames`).
- **Removing a pet** moves its folder to the Recycle Bin instead of deleting the save, since the folder may now hold
  the user's art and personality text.
- **M11 rates are "points per hour of use, on top of the activity".** Sleeping on the bed or couch still restores
  energy at the normal sleep rate and adds a small bonus (+12 and +5 an hour). Feed and Play are instant, so they stay
  well ahead of the trough (about +10 a visit) and the ball or TV. Tucking a pet in (Sleep) isn't instant, so the bed is
  slightly faster per hour than tucking in, but only lasts 5 to 15 minutes.
- **M11 getting there also uses drops:** besides climbing window sides, pets walk off the end of a window top (or step
  across a monitor seam) when that's the way down. Getting *up* always needs a climb, so with climbing off, furniture on
  a window is out of reach. Pets don't use the trampoline to plan a route; it just happens to land them high.
- **M11 pens:** walking pets turn at a pen's side, but a pet on its way to other furniture walks past in front of it.
  Picking the pen up, removing it or its window going lets the pets out. Furniture can't be moved while a pet uses it
  except by the user (it just carries on or walks back to its slot).
- **M11 trips give up** after 60 s or when no route exists, and that piece isn't tried again for 30 s.
- **Furniture windows** are topmost like the pets' but don't re-assert it, so the pets always stay in front.
- **M12 speech and sticky notes are two separate behaviors** (clarified by the user 2026-11-03). Each has its own
  switch (menu: "Let them talk", "Let them leave notes"), rates, gaps, prompt and memory of past lines. They only
  share the model and its one-at-a-time queue.
- **M12 chat frequencies live in `traits.json`** (`Chat` multipliers), not in the save: the same reason as M10.
- **M12 runtime:** LLamaSharp 0.30 with its **Vulkan** backend (17 MB, works with any recent GPU's driver) instead of
  CUDA (200+ MB, NVIDIA only), plus the CPU backend as the fallback. The model is `Qwen3.5-2B-Q6_K.gguf` (1.5 GB,
  smaller than the 2–3 GB guessed).
- **M12 prompts:** the pet's mood only goes into lines about its needs (with it everywhere, a 2B model turned every
  joke into a complaint), and each note gets one concrete topic (given "a reminder, a kind word or a silly thought",
  it wrote that phrase back).

---

## Running it

From VS Code (`.vscode/`):

| | |
|---|---|
| F5 | Build and run with the debugger (needs the C# extension) |
| Ctrl+Shift+B | Build |
| Task `test` / `watch tests` | Run the tests once / on every save |
| Task `run` | Run without the debugger |
| Task `publish` | Self-contained single-file `publish/DesktopPet.App.exe` (no .NET install needed) |

Only one pet can run at a time (named mutex). A running pet locks `bin/`, so stop it (Shift+F5 or Exit in the menu)
before rebuilding.

---

## Solution layout

```
DesktopPet.slnx
Directory.Build.props        nullable, latest analyzers, warnings as errors
global.json                  pins the .NET 10 SDK
src/
  DesktopPet.Core/           net10.0, no Windows references: the whole game
    Math/                    Vec2, Vec2I, RectI (physical pixels; RectI is half-open like Win32 RECT)
    World/                   DesktopSnapshot, MonitorInfo, WindowInfo
                             Platform + PlatformBuilder     floors, window tops, (hang bars)
                             Wall + WallBuilder             window sides, screen edges
                             WorldQuery                     MonitorAt, Nearest, IsWalkable, IsLost
                             WindowMoveGuard                keeps moved windows on screen
    Physics/                 PetBody (modes), PhysicsSystem (+ PhysicsWorld)
    Behavior/                PetContext, PetState/TimedState, States.cs, StateMachine, BehaviorPicker
    Animation/               AnimationDef, AnimationPlayer, Animations (the set)
    Input/                   ThrowEstimator
    Stats/                   PetStats, StatRules, StatsSystem (+ PetAction, StatActivity)
    Persistence/             SaveData, ISaveStore, JsonFileSaveStore
    Simulation/              PetSimulation, PetFrame, PetConfig, FixedStepper, WindowCommand
    Furniture/               FurnitureSpec (+File, Defaults), FurnitureItem, FurnitureWorld, RoutePlanner, RoomSave
    Chat/                    ChatRules, ChatDirector (speech and notes), ChatPrompts, ChatText (+ChatMl), IChatModel
    Abstractions/            IRandom/SeededRandom, IClock/SystemClock
  DesktopPet.Platform.Windows/   net10.0-windows: thin Win32 adapter, no game rules
    Interop/NativeMethods.txt    CsWin32 source generator input (no hand-written DllImports)
    MonitorSource, WindowSource, DesktopSource    the desktop -> DesktopSnapshot
    CursorSource, PetWindowInterop, NativeIcon    our own window and cursor
    WindowManipulator                             moves other windows (pushing), guarded
  DesktopPet.Chat/           net10.0: LlamaChatModel (LLamaSharp, Vulkan + CPU backends), behind Core's IChatModel
  DesktopPet.App/            net10.0-windows WPF exe: composition root
    App.xaml(.cs)            startup, single instance, menu + tray wiring, save path
    PetHost                  owns one pet: simulation, window, loop, saving, lock detection
    GameLoop                 CompositionTarget.Rendering -> FixedStepper, re-entrancy guarded
    PetWindow.xaml(.cs)      the sprite window; reports press/release/cancel/right-click
    SpriteSheet/             DefaultArt (built-in rabbit, embedded rabbit.png), SpriteAtlas
    Furniture/               FurnitureHost (world, loop, windows, room.json), FurnitureArt, Art/*.png (defaults)
    UI/                      PetMenu (shared menu), TrayIcon, DebugOverlay
    Chat/                    ChatHost (model, director, bubbles, notes), SpeechBubble, StickyNote
    app.manifest             PerMonitorV2 DPI awareness
tests/
  DesktopPet.Core.Tests/     xUnit; Fakes/ has FakeDesktop, FakeRandom, ScriptedRandom, FakeClock
tools/sprites/make_rabbit.py generates the built-in rabbit sheet and furniture art (docs/SPRITE_GENERATOR.md)
tools/models/get-model.ps1   downloads the chat model into models/ (gitignored)
docs/PLAN.md                 this file
docs/CODEMAP.md              file-by-file code map, flows and change recipes
docs/SPRITES.md              custom sprite format (user-facing)
docs/FURNITURE.md            furniture and its definition files (user-facing)
docs/CHAT.md                 speech bubbles, sticky notes and the model (user-facing)
docs/SPRITE_GENERATOR.md     how the built-in sheet is generated
```

Dependencies: `App -> Platform.Windows -> Core`, `App -> Chat -> Core`, and `App -> Core`. Core depends on nothing.

---

## Core

### Coordinates
- Everything in Core is in **physical virtual-screen pixels**, the one space that's consistent across monitors with
  different scaling. The process is PerMonitorV2-aware, so Win32 reports physical pixels.
- Sizes and speeds are configured in **DIPs** and multiplied by the scale of the monitor the pet is on, so it looks and
  moves the same everywhere.
- The pet's position is its **feet point** (bottom-centre of the sprite). When the scale changes at a monitor seam, the
  sprite resizes around the feet, so it never pokes through a floor.

### The simulation boundary (`PetSimulation`)
```
new PetSimulation(PetConfig, DesktopSnapshot, IRandom?, IClock?, SaveData?)
Tick(double dt, DesktopSnapshot)         one fixed step against the latest desktop
Current / Previous : PetFrame            feet, size, facing, animation + frame, state, SinkY
PointerDown/Move/Up(Vec2, time), PointerCancel()
Apply(PetAction)                         Feed, Play, Sleep, WakeUp
TakeCommands() : WindowCommand[]         window moves for the host to carry out
UserAway, PushingEnabled                 set by the host / menu
Stats, CreateSave()                      for the menu and the save file
Platforms, Walls, GroundKey, Mode, State for the debug overlay and tests
```
Each tick: the current behavior acts, then physics resolves, then physics events force Fall, Land, Cling (or Hang),
then the safety net, then animation. `Current` and `Previous` let the renderer interpolate.

### World
- **Floors:** each monitor's work-area bottom (the top of the taskbar).
- **Window tops** (`PlatformBuilder`): visible windows, front to back. Each window's top edge, minus the spans covered by
  windows in front, clipped to where a standing pet would be on screen, slivers under 8 px dropped. Maximized windows
  aren't standable. Platforms are one-way: only a downward-moving body collides.
- **Walls** (`WallBuilder`): the outside faces of window sides (covered parts removed, clipped to the screen) and the
  outer work-area edges. Seams between monitors aren't walls, except where one monitor's floor is higher than its
  neighbour's.
- **Hang bars** (off by default): the bottom of the title bar (32 DIP) of windows that can't be stood on.
- **Walking walls** (`WorldQuery.IsWalkable`): the pet's leading edge must stay inside some work area. This covers
  screen edges, side taskbars and a higher neighbouring floor with one rule.
- **Lost** (`WorldQuery.IsLost`): beside every monitor, below one, or in a gap between monitors. After 0.5 s the pet
  respawns at the top of the nearest monitor. Above a monitor doesn't count; gravity brings it back.

### Physics (`PhysicsSystem`)
Body modes: **Airborne, Grounded, Held, Clinging, Hanging**.
- **Airborne:** gravity (3000 DIP/s²) up to terminal velocity (2200 DIP/s), horizontal air drag, swept landing (fast
  bodies can't tunnel; diagonal landings use the x where the path crosses). Flying into a wall it faces slower than
  2500 DIP/s clings; faster bounces off screen edges (55% restitution) and passes window sides.
- **Grounded:** re-finds its surface each tick by key. It rides a window that moved (only when the width is unchanged, so
  resizing doesn't drag it), follows windows down, falls when a floor drops away (taskbar shrank), and hands over to a
  same-height neighbour (monitor seams). A landing keeps horizontal speed and skids to a stop under friction.
- **Clinging:** stays flush against its wall, rides a moved window horizontally and vertically, and lets go when the
  wall disappears.
- **Held:** untouched; the mouse owns it.

### Behavior (`Behavior/States.cs`)
| State | What it does |
|---|---|
| Fall, Land | forced by physics; Land waits for the squash animation and any skid |
| Idle, Walk, Sit | voluntary; random duration, then `BehaviorPicker` chooses again. Idle looks sad when unhappy; Sit on a title bar is a perch with legs dangling |
| Sleep | a nap, or tucked in from the menu until rested; clicking wakes it |
| Eat, Play | from the menu; Play does three real physics hops |
| Happy | after a click ("petting") |
| Held | while dragged |
| Climb, Cling | on a wall: climb up/down, hold, or let go; climbs over onto a title bar or higher floor at the top |
| Push | leans on a window and pushes it, staying against wherever the window actually is |
| Hang, Shimmy | title-bar hanging (disabled) |
| GoToFurniture | on the way to furniture: follows a planned route (walk, climb up, climb down, drop) and replans as it goes |
| UseFurniture | at its slot: the use's animation, rate and effect (hop, bounce and launch, chase and kick the ball) |

**Picker weights** start at Idle 3, Walk 5, Sit 1.5. Below 50 energy, sleep becomes likely and walking slows; below
30 happiness, the pet sits more and walks less. "Use furniture" weighs 4 × the urgency of the lowest need some free,
reachable furniture meets (0 at 75, 1 at empty), plus 0.4 for fun furniture (happiness or no need).

**Walking into a wall:** 35% climb. For window sides only, 25% push (if pushing is on and that window hasn't proved
immovable). Otherwise it turns at screen edges and walks on past window sides. At the end of a window top: 40% climb
down that window's side.

All tuning lives in `PetConfig` (with `StatRules` nested), ready to be loaded from JSON.

### Stats (`Stats/`)
- **Values:** Fed, Happy and Energy, 0–100 where 100 is good. A fresh pet starts at 80/80/80.
- **Decay per real hour:** hunger −8, happiness −5, energy −6. Walking, playing, climbing or pushing costs another −6
  energy. Sleeping restores +45 energy, and hunger and happiness decay at half speed.
- **Critical** is below 15. **While the user is away**, no stat can drop below 20, and one already lower doesn't drop
  further. Away means:
  - the screen is locked (`SessionSwitch`);
  - the process was frozen for more than a minute (PC asleep);
  - the app was closed (catch-up on load).

  Away time also counts as a nap, so the pet comes back rested.
- **Actions:** Feed +35 fed. Play +22 happy, −8 energy, −4 fed. A click (petting) +3 happy.
- Stats run on wall-clock time from `IClock`, not on ticks, so tests run instantly.

### Input
Pointer positions are physical pixels from `GetCursorPos`, with timestamps. A press that moves 4 DIP or more is a drag;
less is a click. While pressed, the host samples the cursor every frame. On release, `ThrowEstimator` fits a
least-squares line through the last 100 ms of samples to get the velocity (capped at 3500 DIP/s). Released with its side
near a wall (16 DIP), it clings.

### Pushing and the move guard
`PushState` asks for window moves as `WindowCommand`s (about 30 DIP/s). It stops at the edge of the window's monitor,
and gives up for the session on any window that doesn't move within 0.6 s (for example an elevated app, which Windows
won't let us move).

`WindowManipulator` applies `WindowMoveGuard` against the window's **live** visible frame and its monitor's work area
right before every move. The window stays inside; one already partly outside can only move back in. No bug upstream can
push a window off screen.

---

## Windows layer
- **Window list** (`WindowSource`, about 30 times a second): `EnumWindows` in front-to-back order, bounds from DWM's
  visible frame, not `GetWindowRect`, which adds invisible borders. It skips:
  - invisible, minimized and cloaked windows (other virtual desktops);
  - tool, no-activate and click-through windows;
  - untitled windows;
  - the desktop, taskbars and shell flyouts;
  - our own process.
- **Monitors** (`MonitorSource`, about once a second): bounds, work area, scale, and the device name as a stable ID.
- **Snapshots** (`DesktopSource`): returns the same snapshot object while nothing changed, so Core can cache what it
  derives from it.

## App
- **Pet window:** borderless, transparent, topmost, sprite-sized, moved with `SetWindowPos` in physical pixels.
  Transparent pixels are click-through because WPF makes it a per-pixel-alpha layered window. It never takes focus and
  is hidden from Alt-Tab and the taskbar.
- **Game loop:** WPF's per-frame event drives the fixed 60 Hz stepper (at most 5 catch-up steps). It ignores the event
  when it fires re-entrantly: resizing a WPF window renders synchronously, which once caused a crash when enabling the
  overlay.
- **Art:** per animation, the pet's own `pets\<n>\sprites\`, then the shared `sprites\` (see `docs/SPRITES.md`),
  otherwise `DefaultArt`, the built-in chibi rabbit. Facing and smoothing are per animation, so layers can mix. The
  rabbit is a 32×32 pixel-art sheet (`SpriteSheet/rabbit.png`, embedded in the exe), drawn facing right and
  mirrored when facing left. The sheet is generated by `tools/sprites/make_rabbit.py` (see "Default rabbit" below).
- **Customization** (`Customization/`): `DataFolder` (paths, settings and pet files, migration, `problems.txt`), and
  `CustomizationWatcher`, which notices edits to `settings.json`, `sprites\` and each pet's `traits.json`,
  `personality.txt` and `sprites\`, and reloads them after a 400 ms pause.
- **Menu** (right-click the pet or the tray icon): the pet's name and mood, stat bars, About *name* (personality,
  neediness and behavior, read-only; Edit traits, Open its folder), Feed / Play / Sleep or Wake up, Add / Remove pet,
  Furniture (Add each kind beside the pet, Remove all, Open furniture folder), "Let it push windows", "Let them talk",
  "Let them leave notes", Customize (Edit
  settings, Open data folder, Export sprite template), Debug overlay, Exit. Right-clicking furniture offers Remove.
- **Debug overlay:** click-through, one per monitor. Floors green, window tops orange, walls blue (screen edges dotted),
  hang bars dashed magenta, the current surface yellow, plus the pet box, state and a summary line.
- **Data folder** `%LOCALAPPDATA%\DesktopPet\`: `settings.json`, `sprites\` (shared), `sprite-template\`,
  `problems.txt`, `furniture\` (a `.json` and image per kind), `room.json` (where the furniture stands), `models\`
  (optional second place for the chat model), and
  `pets\<n>\` per pet: `save.json`, `traits.json`, `personality.txt`, `sprites\`.
- **Saving:** each pet's `save.json`, written atomically (temp file, then swap) every minute, after
  actions, on exit and on logoff. A missing or corrupt file gives a fresh pet.

---

## Testing
- `FakeDesktop` builds monitor and window layouts, with windows added front to back.
- `NewSim` gives every test fixed randomness and a frozen clock.
- `ScriptedRandom` scripts exact roll sequences (for example: walk, don't turn, then climb at the wall).
- Scenario tests drive whole behaviors with `RunUntil` (landing, riding, climbing over, pushing with a slow host).
- Pure geometry tests cover platforms, walls and the move guard.
- The Windows layer is kept thin and checked live: the debug overlay, plus throwaway programs that run the real
  `DesktopSource` against test windows from another process. (`DesktopSource` ignores its own process's windows by
  design, so test windows must live elsewhere.)
- Don't drive the user's real mouse in automated checks without asking.
- **Starting and stopping the pet app for testing is allowed without asking** (granted by the user 2026-11-03),
  including closing a running instance to rebuild. Back up the data folder first when a check could change it.

---

## Known limitations
- **Idle CPU** is about 3–5% of one core: the loop ticks at the display refresh rate even when nothing moves.
- **Change detection is polled:** window changes take up to about 33 ms to be seen, and monitor or taskbar changes up
  to 1 s.
- **Hang bars assume a 32 DIP title bar.** Apps with taller custom title bars (Chrome's tab strip) would grip slightly
  inside it. Hanging is off anyway.
- **Maximized windows have no platforms or walls,** so with everything maximized the pet lives on the taskbars and
  screen edges.
- **No fullscreen detection:** the pet stays on top of games and videos.
- **Memory** is about 127 MB (WPF plus WinForms for the tray icon and menu).
- **Two settings need a restart:** `StepSeconds` and `MaxStepsPerFrame`. Everything else applies live.
- **Furniture is always in front of other windows** (topmost, like the pets), even when its window is behind another.
- **Furniture positions are screen pixels:** after a monitor layout change, saved furniture drops from where it was and
  respawns at the top of the nearest monitor if that's off screen.
- **Memory with chat:** the process is about 750 MB to 1 GB (the model's weights are memory-mapped, plus the GPU
  backend), up from 127 MB. With chat's model missing it stays as before.
- **Sticky notes don't survive a restart**, and notes on a window only follow it while the app runs.
- **Pets beside furniture face it from the side** (the art is a side view), so TV watchers stand left and right of the
  TV rather than in front of it.

---

## M8: settings file and real sprite sheets (done)
Chosen and built 2026-11-03. Checked live: a dropped-in sprite sheet and edited settings applied within about a second,
and an invalid value was rejected while the pet kept running. Everything customizable lives in the data folder `%LOCALAPPDATA%\DesktopPet\` next to `save.json`.

**Settings (`settings.json`)**
- The whole `PetConfig` (with `StatRules` nested) as JSON. Written with the defaults on first run.
- Comments and trailing commas are allowed. Missing keys keep their defaults; unknown keys are ignored.
- Every value is range-checked (`PetConfig.Validate`). Problems are listed in a tray notification.
- **Live reload:** saving the file applies it within half a second (`PetSimulation.Reconfigure`). A file with errors is
  ignored and the previous settings stay in force; at startup, errors fall back to the defaults.
  `StepSeconds` and `MaxStepsPerFrame` only apply on restart.
- Parsing and validation are pure Core code (`PetConfigFile`), unit-tested.

**Sprites (`sprites\atlas.json` plus PNGs)**
- `atlas.json` gives a default image and cell size, and for each animation id a strip of frames: row, starting column,
  frame count, optionally its own image. Options: `facesLeft` (art drawn facing left) and `smooth` (high-quality scaling
  instead of nearest-neighbour, for non-pixel art).
- Fewer frames than the game expects cycle; extras are ignored. **Animations not listed use the built-in rabbit,** so art
  can be replaced one animation at a time. Problems (missing image, frame outside the image, unknown animation) are
  reported in a tray notification, and the affected animations fall back to the built-in art.
- Frames are drawn uniformly scaled into the square sprite box, aligned bottom-centre (the feet point).
- **Live reload** of `sprites\` like settings.
- The atlas is parsed and checked by pure Core code (`SpriteSheetSpec`); the App only decodes and crops PNGs.
- **Menu → Customize:** Edit settings, Open data folder, Export sprite template. The template is the built-in rabbit art as
  one sheet plus a matching `atlas.json` in `sprite-template\`, to copy into `sprites\` and paint over.
- Art conventions (documented in `docs/SPRITES.md`): feet at the bottom centre; climb, cling and push art touches the
  wall at the side the art faces; hang art grips at the top edge; `sit_ledge` sits 9/32 of the height above the bottom.

## M9: multiple pets (done)
Chosen and built 2026-11-03. Checked live: three saves started three pets in three colours, spread across the screen.
- **One `PetHost` per pet** (window, simulation, loop, save). They share one `DesktopSource`, so window and monitor
  polling happens once. Pets don't interact with each other; each excludes all of our process's windows anyway.
- **Which pets exist = which save files exist.** Pet 1 is `save.json` (unchanged, so existing saves carry over); pet *n*
  is `save-n.json`. At startup every save becomes a pet; with none, pet 1 is created. (M10 replaced the files with pet
  folders.) The naming and ID logic is pure
  Core code (`PetRoster`), unit-tested.
- **Menu:** *Add a pet* drops a new pet from the top of the primary monitor at a random x (`PetSimulation.DropFrom`) and
  saves it right away. At startup, every pet after the first also drops in at a random x; otherwise they'd all spawn at
  the top centre and land in a pile. *Remove this pet* closes it and deletes its save, and is disabled for the last pet. At most 8.
- **Which pet the menu is for:** right-click opens it for that pet; the header names it ("Pet 2 · happy") and Feed /
  Play / Sleep act on it. The tray opens it for the pet last right-clicked (the first pet by default).
  "Let it push windows" applies to every pet, including new ones.
- **Telling them apart:** the built-in rabbit gets a jacket colour per pet (charcoal, blue, green, red, purple, then
  repeating).
  Custom sprites were shared by all pets until M10.
- **Debug overlay** moves to the App: one overlay showing the shared surfaces and every pet's box and state.

## Default rabbit (done)
Built 2026-11-03 from the user's reference sketches of their rabbit character: a chibi, 32×32 pixel-art version
replaces the procedural orange blob as the built-in art. Checked live: the pet walks, mirrors and shows a per-pet
jacket colour.
- **Look:** grey fur, dark grey swept bangs with a tuft sticking out at the back, one ear upright and one angled
  forward, big dark eyes, small nose at the front of a light muzzle, dark jacket with a collar over a light shirt.
  The references are greyscale, so the colours are a guess apart from the pink inner ears and blush.
- **Sheet:** `src/DesktopPet.App/SpriteSheet/rabbit.png`, 16 columns × 16 rows of 32×32 cells, one row per animation
  in `Animations.All` order (the same layout as the exported template). Embedded as a resource; `DefaultArt` crops
  frames from it.
- **Generator:** `tools/sprites/make_rabbit.py` (Python 3 + Pillow) composes every frame from ASCII parts: ears
  (up, blown up, flopped back), head, face features (eyes: open, blink, happy, wide, squeezed, droopy; mouths), torsos,
  legs, auto-outlined limbs, and props (apple, z's). Edit it and rerun to change the art;
  `--preview out.png` writes an 8× zoomed copy on a checkerboard. Pose conventions from `docs/SPRITES.md` hold
  (climb/push hands on the right edge, hang hands on row 0, ledge seat at row 23).
- **Per-pet colours:** an exact swap of the three jacket colours, which appear nowhere else in the sheet.
- The old procedural `PlaceholderArt` was removed. In Core, `SpriteSheetSpec.UsingPlaceholder` became `UsingDefault`.

## Planned: M10–M14
Requested 2026-11-03. M10, M11 and M12 are done; M12b is next; the rest aren't started. **Suggested order:** M10 first, because M12 and M13 build on per-pet traits. M11
and M12 don't depend on each other. M13 reuses M11's world objects for the egg. M14 goes last, because its warnings use
M12's needy chat.

**New art reuses the existing generator.** Furniture, eggs, gravestones, hearts and new pet animations are made by
extending `tools/sprites/make_rabbit.py` (its ASCII parts, palette, auto-outlining and preview) wherever it fits,
rather than writing new image code.

### M10: per-pet traits (done)
Built 2026-11-03. Checked live against the user's 7 old saves: they moved into `pets\1\`…`pets\7\` with their stats,
each got a name, traits and a personality, and a broken `traits.json` was rejected (reported, old traits kept) while
the pets ran. The user then checked the rest by hand (About submenu, per-pet sprites, removing a pet) and confirmed it works.
- **A folder per pet:** `pets\<n>\` holds `save.json` (stats), `traits.json`, `personality.txt` and `sprites\`. Copying
  the folder moves the pet. A folder counts as a pet if it has a save or traits. On first run the old `save.json` /
  `save-n.json` move into `pets\1\`, `pets\n\`; a clash with an existing folder gets a new id, nothing is overwritten
  (`PetRoster.PlanMigration`). `PetRoster` now works on folder names.
- **Own sprite sheet:** `pets\<n>\sprites\` (same `atlas.json` format, live-reloaded) wins per animation over the
  shared `sprites\`, which wins over the built-in rabbit. Facing and smoothing are per animation (this also fixed
  built-in fallback frames being mirrored when a custom atlas set `facesLeft`). Without custom art the per-pet jacket
  colours stay.
- **Names:** a new pet (or an old one without `traits.json`) gets a random name from `PetNames` (200 names; see
  "Changes from the original plan"), avoiding every existing pet's name, ignoring case; once all are taken, "Olivia 2".
  The name shows in the menu header, the debug overlay and notifications.
- **Traits (`PetTraits`):** neediness multipliers on the hunger, happiness and energy decay (energy: the drain and the
  extra active drain; sleep restores at the normal rate; care actions are the same for every pet), and behavior
  multipliers on the Idle, Walk, Sit and Sleep weights and on the climb and push chances (scaled down together if
  they'd pass 1). `PetTraits.ApplyTo(shared config)` gives the config a pet runs on; `PetSimulation` takes the traits,
  re-applies them on `Reconfigure`, and `SetTraits` swaps them live (time so far decays at the old rates).
- **Rolling:** `TraitRoller` picks every multiplier log-uniformly in `settings.json` ranges, `NewPetNeediness` and
  `NewPetBehavior` (both 0.5–2 by default, so halving is as likely as doubling and the median is average), rounded to
  2 decimals.
- **`traits.json`:** name and multipliers, forgiving like `settings.json` (comments, any casing, missing multipliers
  are 1), validated (name 1–24 characters on one line, multipliers 0–10, Idle/Walk/Sit not all 0). Live-reloaded; a
  broken file keeps the previous traits and is reported. At startup a broken file runs the pet as average, named
  "Pet *n*", until it's fixed.
- **Personality prompt:** `personality.txt`, plain text (trimmed, at most 2,000 characters), for chat in M12. A new
  pet's starts as a sentence from its strongest traits, such as "Mia is a little grey rabbit who tires easily, loves
  climbing and needs a lot of attention." Live-reloaded.
- **Menu:** About *name* shows the personality, neediness and behavior read-only, with Edit traits (opens
  `traits.json`) and Open its folder. Adding a pet says "Meet *name*" with its personality. Removing a pet moves its
  folder to the Recycle Bin.
- **Duplicate names** from hand edits or copied-in folders are allowed; only generated names avoid each other.

### M11: furniture (done)
Built 2026-11-03. Objects in the world that pets use on their own to meet their needs. Checked live: the defaults were
written on first run, all seven landed on the taskbar, and two pets walked to the trough and ate. User-facing guide:
`docs/FURNITURE.md`. See "Changes from the original plan" for the rate semantics, drops and pens.
- **Definitions:** one JSON file per kind in `furniture\` (the file name is the kind) with its image next to it,
  parsed and validated by Core (`FurnitureSpecFile`), forgiving like `settings.json`, live-reloaded. A broken file keeps
  its last good version (at startup, the default for a default kind), so a typo doesn't empty the room. Deleting a file
  removes that kind and its pieces. The defaults (`DefaultFurniture`) are written only when the folder doesn't exist.
- **A definition** gives name, size (DIP), art (frames side by side, `Animate` Always / Moving / InUse, smoothing),
  slots and optional slot offsets, bounciness and friction, and uses. **A use** gives the need (Hunger, Happiness,
  Energy, None), the rate per hour of use, the pet's animation, the duration, the effect (None, Hop, Bounce, Kick,
  Confine) and a seat height (drawing only).
- **In the world** (`FurnitureWorld`, shared by all pets, ticked by `FurnitureHost`'s own loop): each piece is a
  `PetBody` stepped by `PhysicsSystem` with its own size, so it lands on floors and window tops, rides moving windows,
  falls when its window goes and respawns if lost. A ball bounces and rolls (bounciness 0.5, friction 0.1). Drag to move
  or throw; click a ball to poke it. Placed from the pet menu (dropped just above the pet), removed by right-click,
  at most 24 pieces. Positions are saved in `room.json` once things settle and every minute.
- **Choosing:** see the picker weights above. `FurnitureChooser` ranks the uses by urgency and distance, reserves a
  slot (slots are per piece; the TV has 4) and plans a route; unreachable pieces are skipped for 30 s.
- **Getting there** (`RoutePlanner`): Dijkstra over ground segments using the pet's own moves: walk, step across a seam,
  drop off an end, climb a wall up and over onto the surface at its top, climb down a window side. Climbing costs
  double. The trip replans every 0.5 s and whenever the pet or the target changes surface.
- **Using it:** the use's animation at the slot, facing the furniture when beside it, for the duration or until the
  need is full (energy: well rested). The rate is added in `UpdateStats`. Activity: sleeping for `sleep` uses, active
  for hops, bounces and kicks. Clicking a pet asleep on furniture wakes it; menu care, grabbing it, or the furniture
  being removed or picked up calls the trip off.
- **Defaults:**

  | Furniture | Use |
  |---|---|
  | Ball | chase and kick (happiness +1500/h, 15–30 s), 2 pets |
  | TV | play games (happiness +900/h, 30–90 s), 4 pets |
  | Couch | nap (energy +5/h, 2–5 min) or jump on it (happiness +1200/h, 6–12 s), 2 pets |
  | Trampoline | 3 bounces then a launch of about 900 DIP (happiness +600/h) |
  | Bed | sleep (energy +12/h, 5–15 min) |
  | Play pen | confine |
  | Trough | eat (hunger +2400/h, 15–25 s), 2 pets |

- **Play pen:** a pet the user drops in (released by the mouse, landing inside) is shut in: it walks to and fro inside,
  never chooses furniture, and its needs don't drop. Dragging it out lets it go. Other pets turn at the pen's sides.
- **Art:** the generator draws the furniture (`FURNITURE` in `make_rabbit.py`, embedded from
  `src/DesktopPet.App/Furniture/Art/`) and three new rabbit animations: `kick`, `game`, `bounce`.
- **Tuning** in `settings.json` under `Furniture`: `NeedBelow`, `Weight`, `CuriosityWeight`, `ErrandSeconds`,
  `RetrySeconds`, kick, hop and bounce speeds.

### M12: speech bubbles and sticky notes (done)
Built 2026-11-03. **Two distinct behaviors that both use the language model:** pets *talk* (short lines in speech
bubbles) and, separately, *leave sticky notes* on windows. Neither leads to the other. Checked live: the model loaded
on the GPU in about 2.5 s and each line took about 0.5 s. Two pets talked (one line, then the other's answer), three
notes appeared on a test window from another process, and they went when it closed. The published single-file build
loads the model too. User-facing guide: `docs/CHAT.md`.
- **Model:** [Qwen3.5-2B](https://huggingface.co/Qwen/Qwen3.5-2B) as `Qwen3.5-2B-Q6_K.gguf` (unsloth's quant,
  1.5 GB), run by LLamaSharp 0.30 (llama.cpp). **One instance is shared by every pet** and answers one request at a
  time. It's loaded once at startup in the background, then warmed up with one throwaway answer (the GPU compiles its
  shaders on first use, which made the first real line take 12 s). Replies are capped at 60 tokens; thinking is off
  (an empty think block, as Qwen's template does it).
- **Ships with the app:** in a `models\` folder next to the exe, never inside the single-file exe. The build copies
  (hard-links) `models\*.gguf` from the repo root into `bin\...\models\`, and the publish task into `publish\models\`.
  `tools\models\get-model.ps1` downloads it there (gitignored). The llama.cpp DLLs (`runtimes\win-x64\native\...`)
  also stay beside the exe when publishing, because LLamaSharp doesn't search single-file's unpack folder. A model
  missing or failing to load leaves the pets running without chat, with a notification.
- **GPU or CPU:** `Chat.UseGpu` (default on) uses the Vulkan backend on any recent GPU; without one it falls back to
  the CPU and says so.
- **Architecture:** Core's `ChatDirector` decides when each pet talks or writes, and what about. It talks to the model
  only through `IChatModel`, with a fake in the tests. The model lives in the new project `DesktopPet.Chat`
  (`LlamaChatModel`, no WPF). Requests run off the UI thread; the pets keep acting while one is in flight. A reply
  slower than 25 s, or a failed one, is skipped (a failure is reported once).
- **Speech** (`Chat.Enabled`, menu "Let them talk"):
  - **Needs:** a need below 30 makes the pet say so (told its mood, like click reactions; other lines aren't).
  - **Banter:** when another pet is within 260 DIP, one speaks to the other, and the other answers at least 2 s later.
  - **Jokes.**
  - **Clicked:** every click on a pet (never a drag or throw) gets a spoken reaction to what the click did: being
    petted, woken up, or poked while climbing or falling (requested 2026-11-03). Told its mood, so a hungry pet may say
    so. It goes ahead of everything else and skips the gaps below, waits only for the model (a newer click on the same
    pet replaces a waiting one), and is dropped if it can't start within 8 s.
  - Rates per hour while the kind makes sense: 10, 8, 3. At least 20 s between any two lines, and 60 s between two
    from the same pet (answers excepted).
  - **Bubble:** a click-through window above the pet that follows it, stays 3 s + 0.06 s a character, then fades.
    Bubbles of pets side by side stack instead of covering each other.
- **Sticky notes** (`Chat.NotesEnabled`, menu "Let them leave notes"):
  - A pet standing on a window now and then writes a note and sticks it there, about where it stands, below the title
    bar. Its prompt is about writing, not speaking, with one topic picked per note (water, stretching, a break,
    cheering on, a kind word, a silly thought …).
  - 2 per hour while on a window; at least 2 min between notes and 10 min per pet; at most 3 on screen.
  - The note is a small window anchored to its window: it moves with it, hides while it's minimized, goes when it
    closes, and sits just above it in z-order (so whatever covers the window covers the note). Clicking dismisses it.
- **Both:** sleeping pets do neither; nothing happens while the screen is locked. Each behavior keeps its own memory
  of the pet's last 5 lines or notes, passed to the prompt so it doesn't repeat itself. The output is cleaned up:
  quotes, name prefixes, narration, actions, emojis and think tags are removed, at most two sentences, 160 characters.
- **Per pet:** `traits.json` has `Chat` multipliers (`Needy`, `Banter`, `Jokes`, `Notes`), rolled for new pets in
  `NewPetChat` (0.5–2). Older files without them count as 1. Shown in About. The personality prompt is the pet's
  `personality.txt` (M10).
- **Later:** unloading the model when idle (see Leftovers).

### M12b: reading the screen (planned)
Requested 2026-11-03: pets can read text shown in applications and refer to it when talking or writing notes, if they
want to. To be designed and built next.
### M13: breeding (planned)
For now, a rare, automatic mating routine. This will be expanded later.
- **Mating:** two pets close together, both with good stats, past a cooldown, with a small chance per hour. A short
  courtship (hearts), then an egg appears between them. It needs no approval from the user. Rarity and an off switch
  are in `settings.json`.
- **Egg:** a world object (like furniture) that can be dragged and is saved. It hatches after a set time from
  `settings.json` into a new pet with its own folder and a new random name. **Children hatch fully grown;** growing up
  comes later.
- **The child:** each number (neediness, behaviour traits, chat frequencies) comes from one parent or the average,
  with a little random variation. With the built-in rabbit, the child's colours blend the parents'. With custom art,
  the child takes one parent's sheet.
- **Personality:** the child's prompt starts out random, built from a few traits drawn from a list (easily content,
  voracious, lazy, extremely interested in mating, and similar). The list is a user-editable file. Each trait also
  nudges the matching numbers: voracious raises hunger decay, lazy raises Sit and Sleep weights, interested in mating
  raises the mating chance. Proposed: new pets from *Add a pet* use the same generator.
- **Pet cap:** eggs count toward the cap of 8, and there's no mating at the cap.

### M14: death (planned)
Pets whose needs go unmet for long enough die, leaving a gravestone.
- **Rule:** a pet dies when a stat stays at 0 (or all stats stay critical) for a set time from `settings.json`. There
  is an off switch.
- **Warnings first:** sad looks, needy chat (M12), and a tray notification when a stat goes critical.
- **The away rule stays:** stats can't fall below 20 while the user is away, so a pet can only die of neglect while the
  user is at the PC. Closing the app, sleeping the PC or locking the screen never kills a pet.
- **Gravestone:** a world object where the pet died, showing its name and dates. The pet's folder stays, marked dead,
  so the gravestone survives restarts. Right-clicking it offers:
  - **Revive:** the pet comes back where the gravestone was, with its traits, personality and art, and stats reset to a
    safe level (set in `settings.json`).
  - **Delete:** removes the gravestone and the pet's folder.
- **The last pet dying** is nothing special: it leaves a gravestone like any other, and the user can revive it or add a
  new pet.
- **Pet cap:** gravestones don't count toward the cap of 8. Reviving is disabled while at the cap.

## Leftovers (not started)
Candidates, none chosen yet:
- **Start with Windows:** a menu toggle using the per-user Run registry key.
- **Hide for fullscreen apps:** hide the pet, and pause pushing, on a monitor with a fullscreen app in front.
- **Lower idle CPU:** tick less often while nothing on screen changes.
- **Unload the chat model when idle** (stretch, after M12): free its VRAM after a long quiet spell and reload it on
  demand.
- **OS notifications instead of polling:** `SetWinEventHook` for window moves, plus display-change and work-area
  messages.
