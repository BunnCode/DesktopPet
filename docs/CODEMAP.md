# DesktopPet code map

A navigation guide to the source: what lives where, how a frame flows, and which files to touch for common changes.
Read this instead of exploring the tree. **`docs/PLAN.md`** remains the source of truth for design decisions, status,
tuning values and plans; this file deliberately doesn't repeat them. The sprite generator has its own doc:
**`docs/SPRITE_GENERATOR.md`**.

Keep this file current: when you add, move or rename a file or public type, or change a flow below, update the
matching line here.

---

## At a glance

- C# / .NET 10 / WPF desktop pet (Shimeji-style), Windows only. About 6,700 lines including tests.
- Build `dotnet build DesktopPet.slnx`, test `dotnet test DesktopPet.slnx`. Warnings are errors (`Directory.Build.props`).
  Tests take well under a second.
- Exe: `src/DesktopPet.App/bin/Debug/net10.0-windows/DesktopPet.App.exe`. A running pet locks `bin/`.
  Single instance (named mutex `Local\DesktopPet.SingleInstance`).
- Projects (dependencies point down):

```
DesktopPet.App               net10.0-windows WPF exe (+WinForms for the tray/menu only). Composition root, glue.
  ├─ DesktopPet.Platform.Windows   net10.0-windows. Thin Win32 adapter via CsWin32; no game rules.
  │    └─ DesktopPet.Core
  └─ DesktopPet.Core         net10.0, NO WPF/Win32. All game logic. InternalsVisibleTo the tests.
tests/DesktopPet.Core.Tests  xUnit, Core only (no App/Platform tests).
tools/sprites/make_rabbit.py Python generator for the built-in sprite sheet (not part of the build).
```

## Conventions you need everywhere

- **Coordinates:** Core uses physical virtual-screen pixels (`Vec2` doubles, `Vec2I`/`RectI` ints; `RectI` is
  half-open like Win32 RECT). Tuning is in DIPs and multiplied by `MonitorInfo.Scale` of the pet's monitor.
- **Feet point:** the pet's position is the bottom-centre of its square sprite box (`PetFrame.SpriteRect(feet, size)`).
- **Facing:** art faces right; `PetFrame.FacingLeft` mirrors it. `PetContext.Direction` is +1/-1.
- **Surface identity by string key:** `PlatformBuilder.FloorKey(monitor)`, `WindowKey(id)` (`"window:<id>"`),
  `HangKey(id)` (`"hang:<id>"`), `WallBuilder.SideKey(id, side)` (`"wall:<id>:<side>"`). A body stores its surface
  in `PetBody.GroundKey` and re-finds it every tick, so windows can move under it.
- **Window ids** are HWND values as `long`.
- **Time:** simulation steps are fixed `dt` (1/60 s by default); stats use wall-clock time from `IClock`;
  randomness comes only from `IRandom` (tests inject fakes).
- **Style:** nullable on, analyzers at latest, file-scoped namespaces, XML doc comments on most members.

---

## Core (`src/DesktopPet.Core/`)

### Public entry point: `Simulation/PetSimulation.cs`
One pet. The App talks to Core almost only through this class.

| Member | Purpose |
|---|---|
| `new PetSimulation(config, snapshot, random?, clock?, save?, traits?)` | Spawns at the top of the primary monitor; catches stats up from the save. `config` is the shared one; the pet runs on `traits.ApplyTo(config)` |
| `Tick(dt, snapshot)` | One fixed step (see flow below) |
| `Current` / `Previous` : `PetFrame` | What to draw now / last step (renderer interpolates feet) |
| `PointerDown/Move/Up(pos, time)`, `PointerCancel()` | Mouse gestures: click vs drag (4 DIP threshold), throw on release |
| `Apply(PetAction)` | Feed / Play / Sleep / WakeUp from the menu |
| `TakeCommands()` | Drains `WindowCommand(WindowId, Dx, Dy)`s produced by pushing |
| `Reconfigure(PetConfig)` | Live settings swap (shared config; traits re-applied); forces platform/wall rebuild |
| `Traits`, `SetTraits(PetTraits)` | The pet's name and multipliers; live swap when its traits.json changes |
| `DropFrom(feet)` | Place an airborne pet (new pets arriving) |
| `UserAway`, `PushingEnabled` | Set by host / menu |
| `Config` (effective, traits applied), `Stats`, `CreateSave()`, `IsHeld`, `Mode`, `State`, `Platforms`, `Walls`, `GroundKey`, `RespawnCount` | Read-outs for menu, saving, overlay, tests |
| `internal Place(...)`, `internal ForceState(...)` | Test hooks |

**`Tick` order:** `UpdateWorld` (rebuild platforms/walls only when the snapshot object changed) → `UpdateStats`
(wall-clock decay, activity from the state; a gap longer than `StatRules.AwayGap` means the PC slept and counts as
away + asleep) → `_fsm.Update` (behavior acts on the body) → `PhysicsSystem.Step` →
physics event forces a state (`Landed`→Land unless Play, `LostGround`→Fall, `Grabbed`→Hang, `Clung`→Cling,
`HitWall`→turn) → `ApplySafetyNet` (respawn after 0.5 s lost; skips interpolation) → update DPI scale →
`Animation.Update` → `BuildFrame` (adds `SinkY` for `sit_ledge`).

### Folders

| Folder / file | Contents |
|---|---|
| `Math/Vec2.cs`, `RectI.cs` | `Vec2`, `Vec2I`, `RectI` value types |
| `World/DesktopSnapshot.cs` | `DesktopSnapshot(Monitors, Windows)`: the whole desktop as data. Windows are front-to-back |
| `World/MonitorInfo.cs`, `WindowInfo.cs` | `MonitorInfo(Id, Bounds, WorkArea, Scale, IsPrimary)`, `WindowInfo(Id, Bounds, IsMaximized)` |
| `World/Platform.cs`, `PlatformBuilder.cs` | One-way surfaces (`Floor`, `WindowTop`, `HangBar`) and the occlusion/clipping that builds them |
| `World/Wall.cs`, `WallBuilder.cs` | Vertical climbable faces (`WallKind`, `WallSide`) from window sides and outer screen edges |
| `World/WorldQuery.cs` | `MonitorAt`, `Nearest`, `IsWalkable` (leading edge inside a work area), `IsLost` |
| `World/WindowMoveGuard.cs` | `Clamp(frame, workArea, dx, dy)`: a pushed window can't leave the screen |
| `Physics/PetBody.cs` | `BodyMode` (Airborne, Grounded, Held, Clinging, Hanging) + mutable body: `Feet`, `Velocity`, `GroundKey`, transitions (`Land`, `Hang`, `Cling`, `Grab`, `Release`, `Track*`) |
| `Physics/PhysicsSystem.cs` | Stateless `Step(body, PhysicsWorld, dt, config)` → `PhysicsEvent`; gravity, swept landing, bounces, riding moved windows, skids; `FindWall`, `FindWallCrossing`, `FindBarInReach` |
| `Behavior/PetContext.cs` | Shared mutable state for states: config, random, body, animation player, monitors/platforms/walls, stats, facing, scale, push bookkeeping (`ImmovableWindows`, `Commands`), `TuckedIn` |
| `Behavior/PetState.cs` | `PetStateId` enum; `PetState` (`Enter`, `Update` → next id or null); `TimedState` (random duration from config, then `BehaviorPicker`) |
| `Behavior/States.cs` | All states: Fall, Land, Held, Hang, Shimmy, Happy, Idle, Sleep, Eat, Play, Sit, Walk, Climb, Cling, Push, plus `WallBehavior` (what to do on reaching a wall). Walk holds the wall/edge decision logic |
| `Behavior/StateMachine.cs` | Instantiates every state (a fixed array) and switches between them |
| `Behavior/BehaviorPicker.cs` | Weighted choice of the next voluntary state from `BehaviorWeights` and stats |
| `Animation/AnimationDef.cs`, `AnimationPlayer.cs` | Animation definition (id, frame count, fps, loop) and its player |
| `Animation/Animations.cs` | **The animation set** and `All` (its order is the sprite sheet row order); `LedgeSinkFraction` |
| `Input/ThrowEstimator.cs` | Least-squares release velocity over the last `ThrowWindowSeconds` |
| `Stats/PetStats.cs` | `PetStats(Hunger, Happiness, Energy)` (100 = good), `StatActivity`, `PetAction` |
| `Stats/StatRules.cs`, `StatsSystem.cs` | Decay/restore rates, critical and away floors; pure `Advance`, `CatchUp`, `Apply`, `Petted` |
| `Persistence/SaveData.cs` | `SaveData` record (versioned), `ISaveStore`, `JsonFileSaveStore` (atomic write) |
| `Persistence/PetConfigFile.cs` | `settings.json` (de)serialization with comments/trailing commas; returns `ConfigParseResult(Config, Errors)` |
| `Persistence/PetRoster.cs` | Pet ids ↔ pet folder names (`pets\<n>\`), `MaxPets = 8`, `NextId`, `StartupPets`; legacy `save.json`/`save-n.json` names and `PlanMigration`; `Classify(relative path)` → `(PetId, PetFileKind)` for the watcher |
| `Traits/PetTraits.cs` | `PetTraits(Name, Neediness, Behavior)`, `Neediness`, `BehaviorTraits` (multipliers, 1 = average), `TraitRange`; `ApplyTo(shared config)` |
| `Traits/TraitRoller.cs` | `Roll(config, random, takenNames)`: free name + log-uniform multipliers in `NewPetNeediness`/`NewPetBehavior` |
| `Traits/PetNames.cs` | `Popular`, `Rare`, `All` (200 names); `Pick(random, taken)` |
| `Traits/PetTraitsFile.cs` | `traits.json` (de)serialization and validation → `TraitsParseResult(Traits, Errors)` |
| `Traits/Personality.cs` | `personality.txt`: `Describe(traits)` (default sentence from the strongest traits), `Clean` |
| `Rendering/SpriteSheetSpec.cs` | `SpriteSheetParser.Parse(atlas json, animations, imageSize callback)` → `SpriteSheetSpec(Animations, FacesLeft, Smooth, Problems, UsingDefault)`; `TemplateJson` |
| `Simulation/PetConfig.cs` | **All tuning** (`PetConfig`, nested `StatRules`, `BehaviorWeights`, `DurationRange`), feature switches `EnableClimbing/Pushing/Hanging`, new-pet trait ranges |
| `Simulation/PetConfigValidator.cs` | Range checks for every setting |
| `Simulation/PetFrame.cs` | Render output record; `SpriteRect` |
| `Simulation/FixedStepper.cs` | Accumulator → number of fixed steps, capped |
| `Simulation/WindowCommand.cs` | Window move request |
| `Abstractions/IClock.cs`, `IRandom.cs` | `SystemClock`, `SeededRandom`, `Range`/`Chance` extensions |

---

## Platform.Windows (`src/DesktopPet.Platform.Windows/`)

Win32 through CsWin32: add API names to `Interop/NativeMethods.txt` (one per line); never hand-write DllImports.

| File | Purpose |
|---|---|
| `DesktopSource.cs` | `GetSnapshot()`: windows about every 33 ms, monitors about every 1 s; returns the **same object** when nothing changed (Core caches on reference). Excludes our own process's windows |
| `WindowSource.cs` | `EnumWindows` filter (visible, not minimized/cloaked/tool/untitled/shell) with DWM frame bounds |
| `MonitorSource.cs` | Monitors: bounds, work area, DPI scale, device name as id |
| `CursorSource.cs` | `GetCursorPos` in physical pixels |
| `PetWindowInterop.cs` | Extended styles (no-activate, tool window), `SetBounds` in physical pixels, `BringToTopmost`, overlay styles |
| `WindowManipulator.cs` | `MoveBy(windowId, dx, dy)` with `WindowMoveGuard` against the live frame |
| `NativeIcon.cs` | `DestroyIcon` |

---

## App (`src/DesktopPet.App/`)

| File | Purpose |
|---|---|
| `App.xaml.cs` | Composition root: single-instance mutex, data folder + migration, settings, one `PetHost` per pet folder (`StartPet`: traits rolled/written if missing, personality, layered sprites), shared `PetMenu` + `TrayIcon`, `CustomizationWatcher` callbacks (`OnSettingsChanged`, `OnSpritesChanged`, `OnPetChanged`), add/remove pets, push toggle, debug overlay timer, problem reporting (tray + `problems.txt`) |
| `PetHost.cs` | One pet: `PetSimulation` + `PetWindow` + `GameLoop` + save store, plus `Name`/`Traits`/`Personality`, `SetTraits`. `BeginFrame` (snapshot, cursor sampling while pressed) → `Step` (Tick + carry out `WindowCommand`s) → `Render` (interpolate, pick frame from `SpriteAtlas`, autosave each minute). Lock/unlock → `UserAway`. Implements `IPetControls` for the menu |
| `GameLoop.cs` | `CompositionTarget.Rendering` → `FixedStepper` → step/render; re-entrancy guarded |
| `PetWindow.xaml(.cs)` | Borderless transparent topmost window showing one `Image`; `Present(rect, bitmap, mirror, smooth)`; raises `PointerPressed/Released/Canceled`, `MenuRequested` |
| `SpriteSheet/DefaultArt.cs` | Built-in art: crops 32×32 frames from embedded `rabbit.png`, recolours the jacket per pet (`VariantCount = 5`), `TrayIconPixels()` |
| `SpriteSheet/rabbit.png` | The built-in sheet (EmbeddedResource `DesktopPet.App.rabbit.png`). Generated; don't hand-edit (see `docs/SPRITE_GENERATOR.md`) |
| `SpriteSheet/SpriteAtlas.cs` | (animation id, frame) → `AtlasFrame(Bitmap, FacesLeft, Smooth)`. `Load(folders, animations, variant)` → `(atlas, SpriteLayer[])`: per animation the first folder whose `atlas.json` defines it (pet's own, then shared), built-in art elsewhere; `CustomCount` |
| `SpriteSheet/SpriteTemplateExporter.cs` | Menu → Export sprite template: the built-in art as `template.png` + `atlas.json` + README |
| `Customization/DataFolder.cs` | `%LOCALAPPDATA%\DesktopPet\` paths (`settings.json`, `sprites\`, `sprite-template\`, `problems.txt`, `pets\<n>\` with save, traits, personality, sprites), `ExistingPets`, `MigrateLegacySaves`, traits/personality load and write, `RecyclePetFolder`, settings load, `Open` |
| `Customization/CustomizationWatcher.cs` | `FileSystemWatcher` on settings, shared sprites and pet folders (via `PetRoster.Classify`), 400 ms debounce per file kind and pet, callbacks on the UI thread |
| `UI/PetMenu.cs` | WinForms `ContextMenuStrip` shared by all pets: name + mood header, stat bars, About submenu (personality, traits read-only, Edit traits, Open its folder), Feed/Play/Sleep, add/remove pet, push toggle, Customize submenu, debug overlay, Exit. Acts on the current `IPetControls` target |
| `UI/IPetControls.cs` | What the menu needs from a pet |
| `UI/TrayIcon.cs` | `NotifyIcon` with the shared menu; `Notify(title, text, warning)` |
| `UI/DebugOverlay.cs` | Click-through overlay per monitor drawing floors, window tops, walls, hang bars, pet boxes (`DebugView`, `DebugPet`) |
| `app.manifest` | PerMonitorV2 DPI awareness |

---

## Flows

**Startup:** `App.OnStartup` → mutex → `DataFolder.EnsureExists` → `MigrateLegacySaves` → `LoadSettings` (errors →
defaults) → `DesktopSource` → read every startup pet's `traits.json` (names first, so rolled ones avoid them) → for
each id `StartPet`: roll + write traits if missing, write personality if missing,
`SpriteAtlas.Load(SpriteFolders(id), Animations.All, variant)` → `new PetHost(...)` (loads save, builds
`PetSimulation` with the traits, window, loop) → `Start` → menu, tray → one problems toast → watcher.

**Per display frame (per pet):** `GameLoop.OnRendering` → `PetHost.BeginFrame` (shared `DesktopSource.GetSnapshot`)
→ N × `PetHost.Step(dt)` = `PetSimulation.Tick` + `WindowManipulator.MoveBy` for each command → `PetHost.Render(alpha)`.

**Mouse:** `PetWindow` press/release/capture-lost → `PetHost` → `PetSimulation.Pointer*` with `CursorSource`
positions and `Stopwatch` timestamps; while pressed, `BeginFrame` feeds `PointerMove` every frame. Right-click →
`MenuRequested` → `App` shows `PetMenu` for that pet.

**Settings edit:** watcher → `App.OnSettingsChanged` → `PetConfigFile.Parse` + validate → on success each
`PetHost.Reconfigure` → `PetSimulation.Reconfigure`; on error keep the old config and toast.

**Sprites edit:** watcher → `App.OnSpritesChanged` → `SpriteAtlas.Load` per pet (its folders, its variant) →
`PetHost.SetAtlas`.

**Pet file edit:** watcher → `PetRoster.Classify` → `App.OnPetChanged(id, kind)`: traits → `PetTraitsFile.Parse` →
`PetHost.SetTraits` (unchanged → nothing; errors → keep old + toast); personality → `PetHost.Personality`; sprites →
reload that pet's atlas.

**Saving:** `PetHost.Save` → `JsonFileSaveStore` (`pets\<n>\save.json`) each minute, after menu actions, on exit,
and on session end. Traits and personality are only written when a pet is created. Removing a pet moves its folder to
the Recycle Bin.

---

## Recipes

| To... | Touch |
|---|---|
| Tune a number or add a setting | `PetConfig` (property with default + doc comment), `PetConfigValidator` (range). It serializes automatically. Mention it in PLAN.md if it's notable. Test in `PetConfigFileTests` / `ReconfigureTests` |
| Add a behavior state | `PetStateId` enum, a class in `States.cs` (`TimedState` for voluntary ones), register it in `StateMachine`'s array, pick it from `BehaviorPicker` or another state, decide its `StatActivity` in `PetSimulation.UpdateStats`, add tests under `tests/.../Behavior/` |
| Add an animation | `Animations.cs` (def + append to `All`; the order is the sheet row order), then the generator (`ORDER` + `frames()`), regenerate `rabbit.png`, play it from a state, add it to the table in `docs/SPRITES.md` |
| Change the built-in art | `tools/sprites/make_rabbit.py`, regenerate; see `docs/SPRITE_GENERATOR.md` |
| Change per-pet colours | `DefaultArt.Jackets` (and the jacket colours in the generator's palette must stay unique) |
| Add a per-pet trait | `Neediness`/`BehaviorTraits` (parameter default 1), `PetTraits.ApplyTo`, `PetTraitsFile.Validate`, `TraitRoller.Roll`, maybe `Personality.Describe` and the About lines in `PetMenu`; tests in `Traits/PetTraitsTests` |
| Add a menu item | `UI/PetMenu.cs`; pet-specific actions go through `IPetControls` (implemented by `PetHost`), app-wide ones are constructor callbacks wired in `App.OnStartup` |
| Use a new Win32 API | Add the name to `Interop/NativeMethods.txt`, wrap it in a small static class in Platform.Windows |
| Change what counts as a window/platform | `WindowSource` filter (Windows side) or `PlatformBuilder`/`WallBuilder` (geometry, tested) |
| Change the save format | `SaveData` (bump `CurrentVersion`; tolerate old files), `JsonFileSaveStoreTests` |
| Add a file to the pet folder | `DataFolder` (path, load/write), `PetFileKind` + `PetRoster.Classify` if it reloads live, `App.OnPetChanged` |

## Tests (`tests/DesktopPet.Core.Tests/`)

- `Fakes/FakeDesktop`: fluent `.Monitor(x, y, w, h, scale = 1, taskbar = 0, primary?, id?, workArea?)` (first is
  primary), `.Window(id, x, y, w, h, maximized)` (front to back), `.Snapshot()`.
- `Fakes/SimulationExtensions`: `NewSim(snapshot, random?, config?, clock?, save?)` (defaults: `FakeRandom(0.0)`,
  frozen `FakeClock`), `Dt = 1/60`,
  `sim.RunUntil(snapshot, predicate, maxTicks)`, `Run(ticks)`, `RunSeconds`, `Grounded(sim)`.
- `FakeRandom(values...)` cycles values; `ScriptedRandom(fallback, script...)` plays an exact roll sequence then the
  fallback; `FakeClock.Advance(TimeSpan)`.
- Test folders mirror Core's folders; scenario tests are in `Behavior/`, `Simulation/` and `Input/`.

## Gotchas

- A running pet locks `bin/` and the build fails: ask before closing the user's instance.
- Starting the app migrates old saves in the real data folder for good; back the folder up before live checks that
  could change it.
- `DesktopSource` ignores its own process's windows, so live checks need test windows from another process.
- Don't drive the real mouse in checks without asking.
- `DesktopSnapshot` reference equality matters: Core rebuilds platforms/walls only when the snapshot object changes.
- WinForms implicit usings are removed in the App csproj (they clash with WPF types); import WinForms types explicitly.
- Resizing a WPF window renders synchronously and can re-enter the loop; `GameLoop` guards against it.
- Windows PowerShell mangles double quotes in `git commit -m`; write the message to a file and use `-F`.
