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
| M9 | Multiple pets | Done | |

273 unit tests, all in `DesktopPet.Core.Tests`.

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
    Abstractions/            IRandom/SeededRandom, IClock/SystemClock
  DesktopPet.Platform.Windows/   net10.0-windows: thin Win32 adapter, no game rules
    Interop/NativeMethods.txt    CsWin32 source generator input (no hand-written DllImports)
    MonitorSource, WindowSource, DesktopSource    the desktop -> DesktopSnapshot
    CursorSource, PetWindowInterop, NativeIcon    our own window and cursor
    WindowManipulator                             moves other windows (pushing), guarded
  DesktopPet.App/            net10.0-windows WPF exe: composition root
    App.xaml(.cs)            startup, single instance, menu + tray wiring, save path
    PetHost                  owns one pet: simulation, window, loop, saving, lock detection
    GameLoop                 CompositionTarget.Rendering -> FixedStepper, re-entrancy guarded
    PetWindow.xaml(.cs)      the sprite window; reports press/release/cancel/right-click
    SpriteSheet/             PlaceholderArt (procedural 32x32 frames), SpriteAtlas
    UI/                      PetMenu (shared menu), TrayIcon, DebugOverlay
    app.manifest             PerMonitorV2 DPI awareness
tests/
  DesktopPet.Core.Tests/     xUnit; Fakes/ has FakeDesktop, FakeRandom, ScriptedRandom, FakeClock
docs/PLAN.md                 this file
```

Dependencies: `App -> Platform.Windows -> Core`, and `App -> Core`. Core depends on nothing.

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

**Picker weights** start at Idle 3, Walk 5, Sit 1.5. Below 50 energy, sleep becomes likely and walking slows; below
30 happiness, the pet sits more and walks less.

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
- **Art:** custom sprites from `sprites\` where defined (see `docs/SPRITES.md`), otherwise `PlaceholderArt`, which draws
  every animation procedurally as 32×32 pixel art (drawn facing right, mirrored when facing left).
- **Customization** (`Customization/`): `DataFolder` (paths, settings load, `problems.txt`), and `CustomizationWatcher`,
  which notices edits to `settings.json` and `sprites\` and reloads them after a 400 ms pause.
- **Menu** (right-click the pet or the tray icon): mood line, stat bars, Feed / Play / Sleep or Wake up,
  "Let it push windows", Customize (Edit settings, Open data folder, Export sprite template), Debug overlay, Exit.
- **Debug overlay:** click-through, one per monitor. Floors green, window tops orange, walls blue (screen edges dotted),
  hang bars dashed magenta, the current surface yellow, plus the pet box, state and a summary line.
- **Data folder** `%LOCALAPPDATA%\DesktopPet\`: `save.json` (plus `save-n.json` per extra pet), `settings.json`,
  `sprites\`, `sprite-template\`, `problems.txt`.
- **Saving:** `save.json`, written atomically (temp file, then swap) every minute, after
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
- Fewer frames than the game expects cycle; extras are ignored. **Animations not listed use the placeholder art,** so art
  can be replaced one animation at a time. Problems (missing image, frame outside the image, unknown animation) are
  reported in a tray notification, and the affected animations fall back to placeholders.
- Frames are drawn uniformly scaled into the square sprite box, aligned bottom-centre (the feet point).
- **Live reload** of `sprites\` like settings.
- The atlas is parsed and checked by pure Core code (`SpriteSheetSpec`); the App only decodes and crops PNGs.
- **Menu → Customize:** Edit settings, Open data folder, Export sprite template. The template is the placeholder art as
  one sheet plus a matching `atlas.json` in `sprite-template\`, to copy into `sprites\` and paint over.
- Art conventions (documented in `docs/SPRITES.md`): feet at the bottom centre; climb, cling and push art touches the
  wall at the side the art faces; hang art grips at the top edge; `sit_ledge` sits 9/32 of the height above the bottom.

## M9: multiple pets (done)
Chosen and built 2026-11-03. Checked live: three saves started three pets in three colours, spread across the screen.
- **One `PetHost` per pet** (window, simulation, loop, save). They share one `DesktopSource`, so window and monitor
  polling happens once. Pets don't interact with each other; each excludes all of our process's windows anyway.
- **Which pets exist = which save files exist.** Pet 1 is `save.json` (unchanged, so existing saves carry over); pet *n*
  is `save-n.json`. At startup every save becomes a pet; with none, pet 1 is created. The naming and ID logic is pure
  Core code (`PetRoster`), unit-tested.
- **Menu:** *Add a pet* drops a new pet from the top of the primary monitor at a random x (`PetSimulation.DropFrom`) and
  saves it right away. At startup, every pet after the first also drops in at a random x; otherwise they'd all spawn at
  the top centre and land in a pile. *Remove this pet* closes it and deletes its save, and is disabled for the last pet. At most 8.
- **Which pet the menu is for:** right-click opens it for that pet; the header names it ("Pet 2 · happy") and Feed /
  Play / Sleep act on it. The tray opens it for the pet last right-clicked (the first pet by default).
  "Let it push windows" applies to every pet, including new ones.
- **Telling them apart:** placeholder art gets a colour per pet (orange, blue, green, pink, lavender, then repeating).
  Custom sprites are shared by all pets.
- **Debug overlay** moves to the App: one overlay showing the shared surfaces and every pet's box and state.

## Next: leftovers (not started)
Candidates, none chosen yet:
- **Start with Windows:** a menu toggle using the per-user Run registry key.
- **Hide for fullscreen apps:** hide the pet, and pause pushing, on a monitor with a fullscreen app in front.
- **Lower idle CPU:** tick less often while nothing on screen changes.
- **OS notifications instead of polling:** `SetWinEventHook` for window moves, plus display-change and work-area
  messages.
