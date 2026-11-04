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
| M12 | Speech bubbles and sticky notes (two behaviors) from a shared local LLM (Qwen3.5-2B) | Done | `1045cdf` |
| M12b | Reading on-screen text (Windows OCR) and referring to it when talking or writing notes | Done | `630c904` |
| M13 | Breeding: courtship, eggs that hatch into mixed children, fixing pets, personality traits list | Done | `d1e00b3` |
| M14 | Death and gravestones: neglect timers, warnings, gravestones with name and dates, revive or delete | Done | `a9be018` |
| M15 | Breeding pill: consumable furniture; the pet that swallows it asks every other pet to mate | Done | `98577cc` |
| — | Combined save file: traits.json folded into save.json (version 4), hand edits of the traits kept by autosaves | Done | |

553 unit tests, all in `DesktopPet.Core.Tests`.

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
- **M10 traits lived in `traits.json`, not in `SaveData`,** because the app rewrites `save.json` every minute and hand
  edits there would race with autosaves. **Superseded 2026-11-04** at the user's request: the two files are one
  (`save.json` version 4, with a `Traits` section), and the race is handled by never overwriting the file's `Traits`
  on an autosave (see "Combined save file" below).
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
- **M12 chat frequencies are traits** (`Chat` multipliers), not stats: they were in `traits.json` like the rest of M10's traits (now the `Traits` in `save.json`).
- **M12 runtime:** LLamaSharp 0.30 with its **Vulkan** backend (17 MB, works with any recent GPU's driver) instead of
  CUDA (200+ MB, NVIDIA only), plus the CPU backend as the fallback. The model is `Qwen3.5-2B-Q6_K.gguf` (1.5 GB,
  smaller than the 2–3 GB guessed).
- **M12 prompts:** the pet's mood only goes into lines about its needs (with it everywhere, a 2B model turned every
  joke into a complaint), and each note gets one concrete topic (given "a reminder, a kind word or a silly thought",
  it wrote that phrase back).
- **M13 fixing pets** (requested 2026-11-04): the menu's "*name* is fixed (no babies)" toggle writes `Fixed` to the
  pet's traits (now in `save.json`); hand-editing it works too. Not in the original plan. It's a toggle, so a mistake can be undone.
- **M13 personality traits for every new pet:** *Add a pet* uses the same generator as hatching (the user chose the
  proposal). Old pets keep their `personality.txt`.
- **M13 jackets:** the traits have an optional `Jacket` colour; without it the pet wears its number's colour as
  before. Hatchlings get a blend of their parents' (half and half, ±14 per channel).
- **M13 courtship** brings the pair together: both walk to a point beside each other on their surface and face each
  other before the hearts.
- **M14 both death rules:** each need has its own timer at 0 (`ZeroHours`, 2), and all three critical together have
  another (`AllCriticalHours`, 4). A need rising off 0 resets its timer; time that doesn't count pauses it.
- **M14 the pen is safe too:** time shut in a play pen doesn't count toward death (needs can't drop there anyway).
- **M14 "marked dead"** is the pet's `save.json` (version 3: `DiedUtc`, `DiedOf`), not a separate file. Version 3
  also saves the neglect timers, so a restart doesn't reset a countdown, and `BornUtc` for the gravestone's dates
  (pets from before it get their folder's creation time).
- **M14 warnings only with death on:** with `Death.Enabled` off there are no new notifications at all. Each need warns
  once when it goes critical (again only after climbing 5 back above it) and once when it reaches 0.
- **M14 no dying animation:** the pet's window just goes and the gravestone drops in where it was, with a
  notification saying what happened.
- **M14 Delete** moves the folder to the Recycle Bin, like removing a pet, rather than deleting it outright.

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
    Chat/                    ChatRules, ChatDirector (speech and notes), ChatPrompts, ChatText (+ChatMl), IChatModel,
                             ScreenReading (+IScreenReader), TextList
    Abstractions/            IRandom/SeededRandom, IClock/SystemClock
  DesktopPet.Platform.Windows/   net10.0-windows10.0.19041.0: thin Win32 adapter (and Windows OCR), no game rules
    Interop/NativeMethods.txt    CsWin32 source generator input (no hand-written DllImports)
    MonitorSource, WindowSource, DesktopSource    the desktop -> DesktopSnapshot
    CursorSource, PetWindowInterop, NativeIcon    our own window and cursor
    WindowManipulator                             moves other windows (pushing), guarded
    ScreenText (ScreenCapture, TextRecognizer, WindowIdentity)   reading the screen (M12b)
  DesktopPet.Chat/           net10.0: LlamaChatModel (LLamaSharp, Vulkan + CPU backends), behind Core's IChatModel
  DesktopPet.App/            net10.0-windows10.0.19041.0 WPF exe: composition root
    App.xaml(.cs)            startup, single instance, menu + tray wiring, save path
    PetHost                  owns one pet: simulation, window, loop, saving, lock detection
    GameLoop                 CompositionTarget.Rendering -> FixedStepper, re-entrancy guarded
    PetWindow.xaml(.cs)      the sprite window; reports press/release/cancel/right-click
    SpriteSheet/             DefaultArt (built-in rabbit, embedded rabbit.png), SpriteAtlas
    Furniture/               FurnitureHost (world, loop, windows, room.json), FurnitureArt, Art/*.png (defaults)
    UI/                      PetMenu (shared menu), TrayIcon, DebugOverlay
    Chat/                    ChatHost (model, director, bubbles, notes), SpeechBubble, StickyNote, ScreenReader
    app.manifest             PerMonitorV2 DPI awareness
tests/
  DesktopPet.Core.Tests/     xUnit; Fakes/ has FakeDesktop, FakeRandom, ScriptedRandom, FakeClock
tools/sprites/make_rabbit.py generates the built-in rabbit sheet and furniture art (docs/SPRITE_GENERATOR.md)
tools/sprites/make_character.py, characters/, sprite_preview.py   other characters in the same style (docs/CHARACTER_SPRITES.md)
tools/models/get-model.ps1   downloads the chat model into models/ (gitignored)
docs/PLAN.md                 this file
docs/CODEMAP.md              file-by-file code map, flows and change recipes
docs/SPRITES.md              custom sprite format (user-facing)
docs/FURNITURE.md            furniture and its definition files (user-facing)
docs/CHAT.md                 speech bubbles, sticky notes and the model (user-facing)
docs/SPRITE_GENERATOR.md     how the built-in sheet is generated
docs/CHARACTER_SPRITES.md    the style and process for sprites of new characters
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
  rabbit is a 64×64 pixel-art sheet (`SpriteSheet/rabbit.png`, embedded in the exe), drawn facing right and
  mirrored when facing left. The sheet is generated by `tools/sprites/make_rabbit.py` (see "Default rabbit" below).
- **Customization** (`Customization/`): `DataFolder` (paths, settings and pet files, migration, `problems.txt`), and
  `CustomizationWatcher`, which notices edits to `settings.json`, `sprites\` and each pet's `save.json` (its traits),
  `personality.txt` and `sprites\`, and reloads them after a 400 ms pause.
- **Menu** (right-click the pet or the tray icon): the pet's name and mood, stat bars, About *name* (personality,
  neediness and behavior, read-only; Edit traits, Open its folder), Feed / Play / Sleep or Wake up, Add / Remove pet,
  Furniture (Add each kind beside the pet, Remove all, Open furniture folder), "Let it push windows", "Let them talk",
  "Let them leave notes", "Let them read the screen", Customize (Edit
  settings, Open data folder, Export sprite template), Debug overlay, Exit. Right-clicking furniture offers Remove.
- **Debug overlay:** click-through, one per monitor. Floors green, window tops orange, walls blue (screen edges dotted),
  hang bars dashed magenta, the current surface yellow, plus the pet box, state and a summary line.
- **Data folder** `%LOCALAPPDATA%\DesktopPet\`: `settings.json`, `sprites\` (shared), `sprite-template\`,
  `problems.txt`, `furniture\` (a `.json` and image per kind), `room.json` (where the furniture stands), `models\`
  (optional second place for the chat model), and
  `pets\<n>\` per pet: `save.json` (stats, dates and its traits), `personality.txt`, `sprites\`.
- **Saving:** each pet's `save.json`, written atomically (temp file, then swap) every minute, after
  actions, on exit and on logoff. Saving keeps the file's `Traits` section as it is (see "Combined save file"). A
  missing or corrupt file gives a fresh pet; a file that isn't valid JSON is copied to `save.bad.json` before it's
  saved over.

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
- **Reading the screen:** private browser windows are only excluded when their title says so (Edge and Firefox do;
  Chrome may not). Text in images and games is read too (it's OCR). Text drawn with very low contrast, or tiny text,
  is missed. The exe is larger, because the Windows SDK projection (`Microsoft.Windows.SDK.NET`) comes with it.
- **Eggs are drawn behind the pets** (they're furniture windows), so an egg laid between two pets is half hidden until
  they move away.
- **Only one courtship at a time,** and only between pets on the same surface.
- **A breeding pill round doesn't survive a restart:** closing the app mid-round ends it (the pill is already gone).
  At the pet cap a pet still swallows a pill, but its round ends at once.
- **Pets beside furniture face it from the side** (the art is a side view), so TV watchers stand left and right of the
  TV rather than in front of it.
- **A dead pet's files aren't watched:** renaming it in its save.json shows on the gravestone after a restart.
- **Stats in save.json are the app's:** hand edits to them while the app runs are overwritten by the next autosave
  (only the `Traits` are read back live). Edit them with the app closed.
- **A pet folder copied in with an old `traits.json`** is only folded into its save at the next start.
- **Gravestone dates** use the PC's culture for month names, and a long name is shrunk, then cut short with "…".

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
- **Redrawn 2026-11-04 at 64×64** from the user's turnaround sketch, for a cuter face and room for digitigrade legs:
  both ears up (the back one leaning back a little), a round head with big glossy eyes, a light muzzle and cheek
  fluff, the fringe swept forward from the crown in three locks with a tuft on top (fur on the back of the head), an
  oversized hoodie (hood bump, drawstrings, pocket, ribbed hem; it takes the per-pet colour) over khaki shorts,
  digitigrade legs with ankle tufts, and a fluffy white tail. The generator now draws from shapes (ellipses, polygons,
  tapered strokes, each part auto-outlined) instead of 32×32 ASCII grids. `DefaultArt.FrameSize` became 64; at the
  default `SizeDip` of 64 one art pixel is one DIP. The pose conventions scale (ledge seat at row 46 = 9/32). The
  exported template is now 64×64 cells too. Furniture stays at 1 art px = 2 DIP, so it looks chunkier than the pet.
- **Restyled 2026-11-04** after the user found the 64 px version still off: a chunky chibi sprite style (big round
  head about as tall as body plus legs, small pear-shaped body, stubby limbs, tall oval eyes, bold outlines), following
  a sprite sheet the user gave as a style reference, with details from their updated turnaround: the hands hide in
  the sleeves when the arms hang down, the hood's dark inside shows at the neck, spiky fur at the hocks.
  Then fixed: the walk cycle ran backwards (it lifted the leading foot), and the climb stretched the limbs to the
  wall (now the body moves to the wall instead). The arms' depth was backwards: in the 3/4 view the image-left arm
  is the near one, so it is now drawn over the body and the right one behind it.
- **More characters (2026-11-04):** the process is saved in `docs/CHARACTER_SPRITES.md` and the `character-sprites`
  project skill. A character is `tools/sprites/characters/<name>.py`, whose `apply(base)` overrides parts of
  `make_rabbit` (anything: palette, head, ears, face, clothing, legs, layout, `frames()`); `make_character.py`
  renders it to `sheet.png` + `atlas.json` for a pet's `sprites\` folder. `sprite_preview.py check` verifies the pose
  conventions (it caught ear tips clipping at row 0 in the jump frames, now fixed).
- **Sheet:** `src/DesktopPet.App/SpriteSheet/rabbit.png`, 16 columns × 20 rows of 64×64 cells, one row per animation
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
Requested 2026-11-03. All done (M14 on 2026-11-04). **Suggested order:** M10 first, because M12 and M13 build on per-pet traits. M11
and M12 don't depend on each other. M13 reuses M11's world objects for the egg. M14 goes last, because its warnings use
M12's needy chat.

**New art reuses the existing generator.** Furniture, eggs, gravestones, hearts and new pet animations are made by
extending `tools/sprites/make_rabbit.py` (its ASCII parts, palette, auto-outlining and preview) wherever it fits,
rather than writing new image code.

### M10: per-pet traits (done)
Built 2026-11-03. Checked live against the user's 7 old saves: they moved into `pets\1\`…`pets\7\` with their stats,
each got a name, traits and a personality, and a broken `traits.json` was rejected (reported, old traits kept) while
the pets ran. The user then checked the rest by hand (About submenu, per-pet sprites, removing a pet) and confirmed it works.
- **A folder per pet:** `pets\<n>\` holds `save.json` (stats), `traits.json`, `personality.txt` and `sprites\`
  (`traits.json` was folded into `save.json` on 2026-11-04). Copying
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
- **`traits.json`** (now the `Traits` section of `save.json`): name and multipliers, forgiving like `settings.json` (comments, any casing, missing multipliers
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

### Combined save file (done)
Requested and built 2026-11-04: a pet's `traits.json` and `save.json` are one file. Checked live against the user's
five pets: their `traits.json` files were folded in (and moved to the Recycle Bin), a broken one stayed broken and was
reported, and a name changed by hand while the app ran was applied and survived the next autosave.
- **`save.json` version 4** has a `Traits` section near the top (name, neediness, behavior, chat, `Fixed`, `Jacket`,
  `Parents`, as `traits.json` had) under a comment header explaining it; the stats and dates follow. The whole file is
  forgiving like `settings.json` (comments, trailing commas, any casing), and `+` and non-ASCII names are written as
  they are. The computed `Stats` block older saves wrote is gone.
- **Traits are read on their own** (`PetSaveFile.Parse`): broken traits don't lose the stats. They're reported once
  (not again on every autosave) and the pet keeps its previous traits; at startup it runs as average "Pet *n*".
- **No race with autosaves:** saving never replaces the file's `Traits` (`PetSaveFile.Merge`); it rewrites only the
  stats and dates around them, so a hand edit (even a half-finished one with a mistake in it) stays until it's fixed,
  and an edit that lands just before an autosave isn't lost. Only the app's own trait changes replace them: rolling
  traits for a pet that has none, hatching, and the "fixed" toggle (`replaceTraits`).
- **A file that isn't valid JSON** can't be kept around while saving, so before it's saved over it's copied to
  `save.bad.json` next to it and the user is told.
- **Watching:** the watcher now reports `save.json` (`PetFileKind.Save`); the app's own autosaves come through too and
  are ignored because the traits haven't changed.
- **Migration:** at startup each pet folder's `traits.json` is folded into its `save.json` as written (mistakes and
  all; a folder with only traits gets fresh stats), then moved to the Recycle Bin. One that isn't valid JSON stays
  and is reported. Menu → Edit traits now opens `save.json`.

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
- **Per pet:** the traits have `Chat` multipliers (`Needy`, `Banter`, `Jokes`, `Notes`), rolled for new pets in
  `NewPetChat` (0.5–2). Older files without them count as 1. Shown in About. The personality prompt is the pet's
  `personality.txt` (M10).
- **Later:** unloading the model when idle (see Leftovers).

### M12b: reading the screen (done)
Requested and built 2026-11-03: pets read the text on the window they're on, in front of or beside and may talk or write about it.
Design choices made by the user (all four were the recommended options): OCR rather than UI Automation, the window the
pet is on or beside rather than the active window, on by default with exclusions, and a new speech kind plus a note
topic. Checked live: Windows' OCR read a test window from another process in about 15 ms (capture) + 80 ms (OCR),
every line correct at 125%, and the real model's remarks and notes were about the text without reading it out
("Did you find the carrots crunchy or soft?"). User-facing guide: `docs/CHAT.md`, "Reading the screen".
- **Which window** (`PetSimulation.ReadableWindow`), in this order: the one the pet stands on, clings to or hangs
  from; standing anywhere (the taskbar included), the frontmost window behind the middle of its body (it's in front of
  it; added at the user's request); otherwise a window whose side is within `ReadReachDip` (40) of its box at its
  middle height. Airborne and held pets read nothing.
- **What it reads** (Core `ScreenReading`, pure and tested): a box around the pet (900 × 700 DIP: 450 to each side,
  200 up, 500 down), slid to stay inside the window where it fits (on a window's top it reads further down; on the
  taskbar in front of a window, further up), then clipped to it. Of the lines recognized there, only those whose centre is on the window and not under any
  window in front of it (other processes' windows except click-through ones) or under our own pets, bubbles and notes.
  The snippet is the lines nearest the pet (sideways distance at half weight) up to `ReadChars` (300), back in reading
  order; lines that are mostly not letters and the window's own title bar line are left out.
- **How** (Platform `ScreenCapture`, `TextRecognizer`, `WindowIdentity`; App `ScreenReader`): GDI `BitBlt` of that box
  from the screen, then `Windows.Media.Ocr` for the user's profile languages, enlarged 2× first below 150% scaling.
  This moved Platform.Windows and the App to `net10.0-windows10.0.19041.0` (Windows 10 2004 or later). No OCR language:
  one notification, and pets carry on without reading.
- **When:** only when a pet is about to make a remark or a reading note, never in between. The read runs as the first
  half of that one model request (`ChatDirector` reads, then asks), so it shares the one-at-a-time queue and the reply
  timeout. Nothing readable: a remark is skipped, a note falls back to a usual topic.
- **Speech:** a new kind, `Remark` (`RemarksPerHour` 4 while on, in front of or beside a window). Told the window's title and the
  snippet between `<<<` and `>>>`, asked to say something about one thing it read, in its own words, without reading
  it out. No mood, like jokes.
- **Notes:** with `NoteReadChance` (0.4) × curiosity, a note's topic is the text on its window instead of a usual one.
- **Per pet:** `ChatTraits.Curiosity` (`Traits.Chat.Curiosity` in save.json, rolled 0.5–2 like the others, 1 for older
  files) scales remarks and the reading chance of notes. Shown in About.
- **Privacy:** the menu switch "Let them read the screen" (`ReadingEnabled`, default on); nothing is read while the
  screen is locked; `ReadExcludedApps` (process names: KeePass, KeePassXC, 1Password, Bitwarden, LastPass, Dashlane,
  Enpass, RoboForm, NordPass, ProtonPass, CredentialUIBroker) and `ReadExcludedTitleWords` (InPrivate, Incognito,
  Private Browsing, Password, Sign in, Log in, Login) are never read. What a pet reads only goes into that one prompt;
  it's not logged or saved. The line the pet says or writes is remembered like any other (last 5) for the session.
- **Settings lists** are `TextList`, a list that compares by its items, so `PetConfig` stays equal after a round trip
  through `settings.json`.
### M13: breeding (done)
Built 2026-11-04. A rare, automatic mating routine (to be expanded later). Checked live with the rates turned up: two
pairs courted and laid eggs within 30 s, the cap stopped a third, both eggs hatched on time into pets with blended
jackets, their parents' names and drawn personality traits, and the user's data folder was restored afterwards. The
live check found furniture windows being doubled when an egg was added from a timer (a nested render); fixed.
- **Mating** (Core `BreedingDirector`, ticked by the App's `BreedingHost` every 100 ms): two pets on the same surface
  with their feet within `ReachDip` (160), both idling, walking or sitting (not penned, not on a furniture trip, not
  held), every stat at least `MinStat` (60), neither fixed, both past `CooldownHours` (24, saved as `LastMatedUtc` in
  `save.json`, which is now version 2), the user not away, and room under the cap. Each such pair has a chance of
  `MatesPerHour` (0.5) × the geometric mean of their `Mating` traits per hour. No approval from the user.
- **Courtship:** one at a time. Both walk to points beside each other, face each other and play `love` (gazing,
  blushing, hearts). Once both are there, after `CourtshipSeconds` (6) the egg drops in between them and they hop
  with joy. Picking one up, it falling, or not meeting within `ApproachSeconds` (15) calls it off with no egg and no
  cooldown.
- **Egg:** built-in furniture (`Eggs.Spec`, 24×26 DIP, wobbling; not offered in the Furniture menu, never removed by
  "Remove all furniture" or by deleting furniture files). It can be dragged and thrown, is saved in `room.json` with
  what it holds, and hatches after `HatchMinutes` (60), counted in real time, so an egg due while the app was closed
  hatches at startup. Right-click: who laid it, when it hatches, Remove.
- **The child** (`Inheritance`): each number (neediness, behaviour including `Mating`, chat) comes from one parent or
  their average (a third each), times a factor within ÷/× (1 + `Variation`) (0.15). The jacket colour blends the
  parents'. The art comes from one parent at random: if that one has its own sprite sheet, it's copied into the
  child's folder; otherwise the child uses the shared or built-in art. Its traits record its `Parents`.
- **Hatching:** a new pet folder with a free id and a new name (avoiding all pets), its traits, a personality from the
  trait list, a fresh save already on the mating cooldown, then it drops in where the egg was. Children hatch fully
  grown; growing up comes later.
- **Personality traits** (`PersonalityTraits`, user-editable `personality-traits.json`, written with 22 defaults when
  missing; a broken file falls back to the defaults and is reported): each has words ("is voracious", "is a bit
  lazy", "is extremely interested in mating", "is easily content", …) and nudges (factors on named numbers: voracious
  Hunger ×1.6, lazy Sit and Sleep ×1.6 and Walk ×0.7, interested in mating Mating ×2.5). A new pet gets 2 or 3, never
  two that pull a number opposite ways; they nudge its numbers and make its personality sentence ("Kit is a little
  grey rabbit who is voracious and loves climbing."). Used for hatched pets and for *Add a pet*.
- **Fixing:** the menu item "*name* is fixed (no babies)" sets `Fixed` in the pet's traits (in `save.json`; or clears it).
  About shows the parents, and whether it's fixed or how keen it is on mating.
- **Pet cap:** pets plus eggs are at most 8: no mating and no *Add a pet* at the cap.
- **Off switch and tuning:** `settings.json` under `Breeding` (`Enabled` and the numbers above).

### M14: death (done)
Built 2026-11-04. Pets whose needs go unmet for long enough die, leaving a gravestone. Checked live with
`ZeroHours` at 0.01 and furniture-seeking off: a pet at hunger 0 died after 36 s, its save was marked dead, a
gravestone with its name and dates engraved dropped onto the taskbar where it was, and after a restart the pet stayed
dead (not started, save untouched) with its one gravestone. The user's data folder was restored afterwards. Revive,
Delete and the menu with no pets haven't been clicked through live yet.
- **Rule** (Core `Mortality`, inside `PetSimulation.UpdateStats`): a pet dies when one need stays at 0 for
  `ZeroHours` (2), or all three stay below critical for `AllCriticalHours` (4). Each need has its own timer; it resets
  when the need rises off 0. Only time with the user there counts: not locked, not across a frozen-process gap, not with
  the app closed, and not shut in a pen. The timers are saved, so a restart doesn't reset them. The cause is the need
  that ran out (hunger, sadness, exhaustion) or neglect. A dead pet's needs stop changing.
- **Warnings first:** the sad looks and needy chat from before, plus tray notifications (`PetSimulation.TakeWarnings`
  → `PetHost.Warned`): a need goes critical ("Mia is very hungry. Feed Mia soon."), a need reaches 0 ("Mia is
  starving! Without food, Mia will die in about 2 hours."), all three critical. Each fires once until the need
  recovers.
- **The away rule stays:** stats can't fall below 20 while the user is away, and away time doesn't count, so closing
  the app, sleeping the PC or locking the screen never kills a pet.
- **Dying** (`PetHost.Died` → `App.OnDied`): the pet's window closes, its save is written marked dead (`DiedUtc`,
  `DiedOf`), and a gravestone drops in where it was. Chat and breeding forget it.
- **Gravestone** (`Graves.Spec`, built-in furniture like the egg, 64×60 DIP; not in the Furniture menu, never removed
  by "Remove all furniture" or furniture files): the stone art from the generator with the name and dates (born –
  died) engraved by the app (`GraveArt`), drawn at the exact pixel size so the small text stays sharp. Saved in
  `room.json` with whose it is. The pet's folder is what counts: at startup every dead pet gets one gravestone (where
  `room.json` had it, else dropped in at the top), and gravestones without a dead pet are removed. It can be dragged.
  Right-clicking it shows the dates and what happened, and offers:
  - **Revive:** the save is rewritten alive with every need at `ReviveStat` (60), no neglect, its birthday and mating
    cooldown kept; the gravestone goes and the pet drops in there with its traits, personality and art.
  - **Delete … for good:** the pet's folder goes to the Recycle Bin, then the gravestone goes.
- **The last pet dying** is nothing special: with no pets the menu shows only the app-wide items, with "Add a pet".
- **Pet cap:** gravestones don't count toward the cap of 8. Revive is disabled while pets and eggs are at the cap.
- **Off switch and tuning:** `settings.json` under `Death` (`Enabled`, `ZeroHours`, `AllCriticalHours`, `ReviveStat`).

## M15: breeding pill (done)
Requested and built 2026-11-04. A new default furniture kind, the **breeding pill** (`pill`, 20×12 DIP, a blue
capsule drawn by the generator).
Checked live against a copy of the user's five pets with a pill put in `room.json`: on start the pill was added to
the existing furniture folder, a pet swallowed the pill about 20 s later, and within 40 s it had asked three pets in
turn and laid three eggs with them (one parent each time was the pill taker), stopping at the pet cap. The data folder
was restored afterwards. The speech bubbles weren't looked at on screen.
- **Consumable furniture** (new, general): `FurnitureSpec.Consumable` removes the piece once a pet has used it for the
  whole time (an interrupted use doesn't count). The pill is the only default that uses it.
- **Taking it:** its use (`Effect: Breed`, `eat` for 2–3 s, no need) is tempting: it counts as if a need it meets were
  half empty (`FurnitureChooser.PillAppeal`), so a pet goes for a pill soon after it's placed. Fixed pets, pets already
  on a round, and everyone while breeding is switched off leave it alone.
- **The round** (Core `BreedingDirector`, pet side `VisitState`): the pet goes round every other pet, nearest first,
  walking, climbing and dropping like a furniture trip (the route-following code is now shared, `RouteFollower`). It
  stops beside each one and asks (faces it, gazes with hearts, and says a line such as "Leo, want to make babies?");
  the asked pet stops what it's doing and faces it. After `PillAskSeconds` (3) it answers:
  - **Yes** if it could mate anyway (not fixed, past its own cooldown, stats at least `MinStat`) and a roll of
    `PillAgreeChance` (0.75) × its Mating trait (at most 1) succeeds; another pet on a pill round always says yes
    (unless fixed). They then court as in M13 and an egg is laid. The pill overrides the asker's own cooldown and
    stats.
  - **No** otherwise, with a line that says why ("Sorry, I'm fixed.", "I just had babies!", "I'm not feeling up to
    it.", or a plain "No thanks."); it turns its back and walks off, and the asker moves on.
- A pet it can't reach within `PillVisitSeconds` (45), or with no way there for 2 s, or shut in a pen, is skipped.
  The round ends once everyone's been asked, after `PillMinutes` (10), at the pet cap (eggs count), or if the asker
  becomes fixed or breeding is switched off. Picking the asker up pauses it; it carries on after landing.
- **Speech:** the lines are fixed (not the chat model) and shown in the usual bubbles when "Let them talk" is on.
- **New defaults reach existing folders:** `furniture\defaults-written.txt` lists the defaults already written. A
  folder without it (from before M15) is taken to have the first seven, so the pill is added once; a default the user
  deleted stays deleted.
- **Tuning:** `settings.json` under `Breeding`: `PillMinutes`, `PillVisitSeconds`, `PillAskSeconds`, `PillAgreeChance`.

## Leftovers (not started)
Candidates, none chosen yet:
- **Start with Windows:** a menu toggle using the per-user Run registry key.
- **Hide for fullscreen apps:** hide the pet, and pause pushing, on a monitor with a fullscreen app in front.
- **Lower idle CPU:** tick less often while nothing on screen changes.
- **Unload the chat model when idle** (stretch, after M12): free its VRAM after a long quiet spell and reload it on
  demand.
- **OS notifications instead of polling:** `SetWinEventHook` for window moves, plus display-change and work-area
  messages.
