# DesktopPet code map

A navigation guide to the source: what lives where, how a frame flows, and which files to touch for common changes.
Read this instead of exploring the tree. **`docs/PLAN.md`** remains the source of truth for design decisions, status,
tuning values and plans; this file deliberately doesn't repeat them. The sprite generator has its own doc:
**`docs/SPRITE_GENERATOR.md`**.

Keep this file current: when you add, move or rename a file or public type, or change a flow below, update the
matching line here.

---

## At a glance

- C# / .NET 10 / WPF desktop pet (Shimeji-style), Windows only. About 11,000 lines including tests.
- Build `dotnet build DesktopPet.slnx`, test `dotnet test DesktopPet.slnx`. Warnings are errors (`Directory.Build.props`).
  Tests take well under a second.
- Exe: `src/DesktopPet.App/bin/Debug/net10.0-windows10.0.19041.0/DesktopPet.App.exe`. A running pet locks `bin/`.
  Single instance (named mutex `Local\DesktopPet.SingleInstance`).
- Projects (dependencies point down):

```
DesktopPet.App               net10.0-windows10.0.19041.0 WPF exe (+WinForms for the tray/menu only). Composition root, glue.
  ├─ DesktopPet.Platform.Windows   net10.0-windows10.0.19041.0. Thin Win32 adapter via CsWin32 (+ Windows OCR); no game rules.
  │    └─ DesktopPet.Core
  ├─ DesktopPet.Chat          net10.0 (win-x64). The local language model via LLamaSharp; implements Core's IChatModel.
  │    └─ DesktopPet.Core
  └─ DesktopPet.Core         net10.0, NO WPF/Win32. All game logic. InternalsVisibleTo the tests.
tests/DesktopPet.Core.Tests  xUnit, Core only (no App/Platform tests).
tools/sprites/make_rabbit.py Python generator for the built-in sprite sheet and furniture art (not part of the build).
tools/sprites/make_character.py  Renders characters/<name>.py (overrides on the rabbit's rig) to a drop-in sheet + atlas.json.
tools/sprites/sprite_preview.py  overview / zoom / check (pose conventions) for any generated sheet.
tools/models/get-model.ps1   Downloads the chat model into models/ at the repo root (gitignored); the build copies it.
tools/publish/publish.ps1    Stops running pets, builds, tests, publishes, then copies data/ into publish/data/ (backs
                             the old copy up to %TEMP%\DesktopPet-backups first). The `publish` Claude skill runs it.
tools/demo/                  README screenshots: forest.py (pixel-art backdrop), make_demo.py (staged data), stage.py
                             (backdrop + demo windows), grab/sheet/click.py. See docs/how_to_make_demo.md.
```

## Conventions you need everywhere

- **Coordinates:** Core uses physical virtual-screen pixels (`Vec2` doubles, `Vec2I`/`RectI` ints; `RectI` is
  half-open like Win32 RECT). Tuning is in DIPs and multiplied by `MonitorInfo.Scale` of the pet's monitor. Drawn sizes are snapped to whole art-pixel
  multiples: `PetConfig.SizePxAt(scale)` (pet) and `FurnitureItem.PixelScale` (furniture); don't use `SizeDip * Scale`.
- **Feet point:** the pet's position is the bottom-centre of its square sprite box (`PetFrame.SpriteRect(feet, size)`).
- **Facing:** art faces right; `PetFrame.FacingLeft` mirrors it. `PetContext.Direction` is +1/-1.
- **Surface identity by string key:** `PlatformBuilder.FloorKey(monitor)`, `WindowKey(id)` (`"window:<id>"`),
  `HangKey(id)` (`"hang:<id>"`), `WallBuilder.SideKey(id, side)` (`"wall:<id>:<side>"`). A body stores its surface
  in `PetBody.GroundKey` and re-finds it every tick, so windows can move under it.
- **Window ids** are HWND values as `long`.
- **Time:** simulation steps are fixed `dt` (1/60 s by default); stats use wall-clock time from `IClock`;
  randomness comes only from `IRandom` (tests inject fakes).
- **Style:** see `docs/PLAN.md`, "Coding standards" (naming, formatting, layout). Naming, formatting, unused usings,
  namespaces and readonly fields are checked by the build (`.editorconfig`), so a violation fails it.

---

## Core (`src/DesktopPet.Core/`)

### Public entry point: `Simulation/PetSimulation.cs`
One pet. The App talks to Core almost only through this class.

| Member | Purpose |
|---|---|
| `new PetSimulation(config, snapshot, random?, clock?, save?, traits?, furniture?)` | Spawns at the top of the primary monitor; catches stats up from the save. `config` is the shared one; the pet runs on `traits.ApplyTo(config)`. `furniture` is the shared `FurnitureWorld` (an empty one if omitted) |
| `Tick(dt, snapshot)` | One fixed step (see flow below) |
| `Current` / `Previous` : `PetFrame` | What to draw now / last step (renderer interpolates feet) |
| `PointerDown/Move/Up(pos, time)`, `PointerCancel()` | Mouse gestures: click vs drag (4 DIP threshold), throw on release |
| `Apply(PetAction)` | Feed / Play / Sleep / WakeUp from the menu |
| `TakeClick()` | The last click (not a drag) and what it did (`PetClick`), for the spoken reaction |
| `TakeCommands()` | Drains `WindowCommand(WindowId, Dx, Dy)`s produced by pushing |
| `Reconfigure(PetConfig)` | Live settings swap (shared config; traits re-applied); forces platform/wall rebuild |
| `Traits`, `SetTraits(PetTraits)` | The pet's name and multipliers; live swap when the Traits in its save.json change |
| `Death`, `TakeWarnings()`, `Neglect`, `BornUtc` | M14: set once it has died (`PetDeath`); warnings since the last call (`PetWarning`); the neglect timers; its birthday (from the save, else now) |
| `DropFrom(feet)` | Place an airborne pet (new pets arriving) |
| `ReadableWindow(reachDip)` | The window the pet can read (M12b): stands on or clings to, else the one behind it, else one beside it |
| `WindowBehind()` | The frontmost window behind the pet's middle while it stands somewhere other than on a window (notes go there) |
| `OnPillRound`, `PillRoundBlocked`, `Seek(groundKey, x)`, `AskToMate`, `EndPillRound`, `Listen`, `Refuse` | M15: the breeding pill round, driven by `BreedingDirector` (the asker heads for a spot and asks; the asked pet stops to listen, or walks off) |
| `FurnitureTarget`, `Pen`, `LeaveFurniture()` | The furniture it's heading for or using, the pen it's shut in; free its slot (pet removed) |
| `UserAway`, `PushingEnabled` | Set by host / menu |
| `Config` (effective, traits applied), `Stats`, `CreateSave()`, `IsHeld`, `Mode`, `State`, `Platforms`, `Walls`, `GroundKey`, `RespawnCount` | Read-outs for menu, saving, overlay, tests |
| `internal Place(...)`, `internal ForceState(...)` | Test hooks |

**`Tick` order:** `UpdateWorld` (rebuild platforms/walls only when the snapshot object changed) → `UpdateStats`
(wall-clock decay, activity from the state; a gap longer than `StatRules.AwayGap` means the PC slept and counts as
away + asleep; plus the furniture use's rate; no drop while penned; then `Mortality.Update`, which may set `Death`;
nothing changes once dead) → `CheckPen` (let out if the pen moved, went or
was picked up) → `GroupPlay.TryJoin` while Idle/Walk/Sit (on a join → GoToFurniture) → `_fsm.Update` (behavior acts on the body) → `PhysicsSystem.Step` →
physics event forces a state (`Landed`→`OnLanded`: shut in a pen if the user dropped it there, Land unless Play or
UseFurniture; `LostGround`→Fall, `Grabbed`→Hang, `Clung`→Cling, `HitWall`→turn) → `ApplySafetyNet` (respawn after 0.5 s lost; skips interpolation) → update DPI scale →
`Animation.Update` → `BuildFrame` (`SinkY` for `sit_ledge`, minus the seat height (`Spec.SeatDip(slot, use)`) while on a bed, couch or tower seat).

### Folders

| Folder / file | Contents |
|---|---|
| `Math/Vec2.cs`, `RectI.cs` | `Vec2`, `Vec2I`, `RectI` value types |
| `World/DesktopSnapshot.cs` | `DesktopSnapshot(Monitors, Windows)`: the whole desktop as data. Windows are front-to-back |
| `World/MonitorInfo.cs`, `WindowInfo.cs` | `MonitorInfo(Id, Bounds, WorkArea, Scale, IsPrimary)`, `WindowInfo(Id, Bounds, IsMaximized)` |
| `World/Platform.cs`, `PlatformBuilder.cs` (+ `WindowIdFromKey`) | One-way surfaces (`Floor`, `WindowTop`, `HangBar`) and the occlusion/clipping that builds them |
| `World/Wall.cs`, `WallBuilder.cs` | Vertical climbable faces (`WallKind`, `WallSide`) from window sides and outer screen edges |
| `World/WorldQuery.cs` | `MonitorAt`, `Nearest`, `IsWalkable` (leading edge inside a work area), `IsLost` |
| `World/WindowMoveGuard.cs` | `Clamp(frame, workArea, dx, dy)`: a pushed window can't leave the screen |
| `Physics/PetBody.cs` | `BodyMode` (Airborne, Grounded, Held, Clinging, Hanging) + mutable body: `Feet`, `Velocity`, `GroundKey`, transitions (`Land`, `Hang`, `Cling`, `Grab`, `Release`, `Track*`) |
| `Physics/PhysicsSystem.cs` | Stateless `Step(body, PhysicsWorld, dt, config)` → `PhysicsEvent`; gravity, swept landing, bounces, riding moved windows, skids; `FindWall`, `FindWallCrossing`, `FindBarInReach` |
| `Behavior/PetContext.cs` | Shared mutable state for states: config, random, body, animation player, monitors/platforms/walls, stats, facing, scale, push bookkeeping (`ImmovableWindows`, `Commands`), `TuckedIn`; furniture: `Furniture`, `Owner`, `Errand`, `ActiveUse`, `Pen`, `UserDropped`, `Unreachable`, `NoJoinUntil` (group-play cooldown), `CancelErrand()`; M15: `Visit`, `Fixed`, `MayTakePill`; **`PickNext()`** (carry on with a trip, `OnTrip`/`TripState`: furniture or a pill round; else `BehaviorPicker.Pick(context)`) |
| `Behavior/PetState.cs` | `PetStateId` enum; `PetState` (`Enter`, `Update` → next id or null, `Exit`); `TimedState` (random duration from config, then `PickNext`) |
| `Behavior/States.cs` | All states: Fall, Land, Held, Hang, Shimmy, Happy, Idle, Sleep, Eat, Play, Sit, Walk, Climb, Cling, Push, plus `WallBehavior` (what to do on reaching a wall). Walk holds the wall/edge decision logic |
| `Behavior/RouteFollower.cs` | Walks one tick along a route's first step → `RouteProgress` (Moving, Arrived, Climbing, Blocked, Replan); `Walker(context)`. Shared by furniture trips and pill rounds |
| `Behavior/VisitState.cs` | M15: `Visit` (target surface and x, asking, route, blocked time) + `VisitState`: follow a route to the spot the director gives, wait, ask with `love` |
| `Behavior/StateMachine.cs` | Instantiates every state (a fixed array) and switches between them |
| `Behavior/BehaviorPicker.cs` | Weighted choice of the next voluntary state from `BehaviorWeights` and stats (penned: sit raised to `FurnitureRules.PenSitWeight`); `Pick(context)` adds "go use furniture" (`FurnitureChooser.Weight`) |
| `Behavior/Errand.cs` | `Errand` (item, use, slot, route, timers, `PlaySeconds`: a joiner's cap on its use); `FurnitureChooser`: `Urgency`, `Weight` (incl. the nearby-toy bonus), `TryStart` (rank, then `TryGo`), `TryGo` (reserve a slot, plan, or mark unreachable), `Closeness`, `IsFun`, `TargetX`, `Plan` |
| `Behavior/GroupPlay.cs` | `TryJoin(context, dt)`: a pet idling, walking or sitting near another pet's group game (a fun use of a multi-slot piece with a free slot) rolls to join it for `JoinSeconds`, then waits `JoinCooldownSeconds` before joining again |
| `Behavior/CourtState.cs` | M13: `Courtship(MeetX, FaceLeft, GiveUpSeconds)` + `CourtState`: walk to the meeting point, face the partner, play `love` until the director ends it (gives up by itself after a while) |
| `Behavior/FurnitureStates.cs` | `GoToFurnitureState` (follows the route via `RouteFollower`: walks, hands climbs to Climb, drops to physics, replans, gives up) and `UseFurnitureState` (animation, duration, effects Hop / Bounce / Kick; at the end a `Consumable` piece is removed and a `Breed` use starts a pill round) |
| `Animation/AnimationDef.cs`, `AnimationPlayer.cs` | Animation definition (id, frame count, fps, loop) and its player |
| `Animation/Animations.cs` | **The animation set** and `All` (its order is the sprite sheet row order); `Find(id)`; `LedgeSinkFraction` |
| `Input/ThrowEstimator.cs` | Least-squares release velocity over the last `ThrowWindowSeconds` |
| `Stats/PetStats.cs` | `PetStats(Hunger, Happiness, Energy)` (100 = good), `StatActivity`, `PetAction` |
| `Stats/StatRules.cs`, `StatsSystem.cs` | Decay/restore rates, critical and away floors; pure `Advance`, `CatchUp`, `Apply`, `Petted` |
| `Persistence/SaveData.cs` | `SaveData` record (versioned; v2 adds `LastMatedUtc`; v3 adds `BornUtc`, `Neglect`, `DiedUtc`, `DiedOf`, `IsDead`, `Revived(rules, now)`; v4 adds `Traits`), `ISaveStore` (`Save(data, replaceTraits)`), `JsonFileSaveStore` (atomic write via `PetSaveFile.Merge`; `LoadFile`; a file that isn't JSON → `save.bad.json` + `BrokenFileKept`) |
| `Persistence/PetSaveFile.cs` | A pet's `save.json` with its `Traits` section: `Serialize`, `Parse` → `PetFileParseResult(Save, Traits)` (traits read on their own), `Merge(onDisk, data, replaceTraits)` (keeps the file's Traits as they are unless replacing), `FoldIn(save, traits.json, now)` (migration) |
| `Persistence/PetConfigFile.cs` | `settings.json` (de)serialization with comments/trailing commas; returns `ConfigParseResult(Config, Errors)` |
| `Persistence/PetRoster.cs` | Pet ids ↔ pet folder names (`pets\<n>\`), `MaxPets = 8`, `NextId`, `StartupPets(existing, dead)` (dead ones skipped; none if all are dead); legacy `save.json`/`save-n.json` names and `PlanMigration`; `Classify(relative path)` → `(PetId, PetFileKind)` (`Save`, `Personality`, `Sprites`) for the watcher |
| `Traits/PetTraits.cs` | `PetTraits(Name, Neediness, Behavior, Chat, Fixed, Character, Jacket, Parents)`, `Neediness`, `BehaviorTraits` (incl. `Mating`), `ChatTraits` (multipliers, 1 = average), `TraitRange`; `ApplyTo(shared config)` (chat and mating aren't applied there; `ChatDirector` and `BreedingDirector` read them) |
| `Traits/TraitRoller.cs` | `Roll(config, random, takenNames, wornCharacters)`: free name + log-uniform multipliers in `NewPetNeediness`/`NewPetBehavior` + the least worn default character |
| `Traits/Characters.cs` | The built-in characters: `CharacterInfo(Id, Name, Species)`; `Defaults` (the six Fakemon), `All` (+ `Rabbit`); `Find`, `For(petId, character)` (unset → by number), `SpeciesOf`, `Pick(random, worn)` |
| `Traits/PetNames.cs` | `Popular`, `Rare`, `All` (200 names); `Pick(random, taken)` |
| `Traits/PetTraitsFile.cs` | Traits JSON (the `Traits` section of save.json; `LegacyFileName` = the old traits.json): `Serialize`, `ToNode`, `Parse(text or JsonNode)` and validation → `TraitsParseResult(Traits, Errors)` |
| `Traits/Personality.cs` | `personality.txt`: `Describe(traits)` (sentence from the strongest numbers, for old pets without one), `Clean` |
| `Traits/PersonalityTraits.cs` | M13: `personality-traits.json` (`Defaults`, `Parse`, `Serialize`); `PersonalityTrait(Text, Nudge)`; `Draw` (2–3, never two pulling a number opposite ways), `Apply` (nudges), **`Give(traits, list, random)`** → nudged traits + personality sentence (used for added and hatched pets) |
| `Traits/JacketColors.cs` | `JacketShades(Base, Shade, Light)`; `Variants` (by pet number), `For(id, jacket)`, `ShadesOf`, `Blend` (a child's colour), `TryParse`/`Format` (#RRGGBB) |
| `Rendering/SpriteSheetSpec.cs` | `SpriteSheetParser.Parse(atlas json, animations, imageSize callback)` → `SpriteSheetSpec(Animations, FacesLeft, Smooth, Problems, UsingDefault)`; `TemplateJson` |
| `Simulation/PetConfig.cs` | **All tuning** (`PetConfig`, nested `StatRules`, `BehaviorWeights`, `DurationRange`, `FurnitureRules`, `ChatRules`, `BreedingRules`, `DeathRules`), feature switches `EnableClimbing/Pushing/Hanging`, new-pet trait ranges |
| `Furniture/FurnitureSpec.cs` | `FurnitureSpec` (a kind: size, art, frames, `Animate`, slots and offsets, per-slot seats `SlotSeatsDip` and `SlotFacing`, bounciness, friction, uses, `Consumable`; `IsPen`, `SlotOffsetDip`, `SeatDip(slot, use)`, `FacingAt(slot)`), `FurnitureUse` (need, rate, animation, seconds, `MinSeconds` (a minimum stay, even once full), effect, seat), `Need`, `UseEffect`, `AnimateWhen`, `SlotFacing` |
| `Furniture/FurnitureSpecFile.cs` | `furniture\<kind>.json` (de)serialization and validation → `FurnitureParseResult`; `KindFromFileName` |
| `Furniture/DefaultFurniture.cs` | The nine default kinds (`All`, incl. M15's `Pill` and the `LivingTower`); `FirstKinds` and `ToWrite(written)`: which defaults to add to an existing folder |
| `Furniture/FurnitureItem.cs` | One piece in the world: body (a `PetBody`), scale, `PixelScale` (whole-number scale for its size and slots), slots (`Reserve`/`Release`), `Users` (pet → the use it's making, so others can see what's being played), `Current`/`Previous` `FurnitureFrame`; `Egg` (an `EggInfo` if it's an egg), `Grave` (a `GraveInfo` if it's a gravestone), `IsFurniture` (neither) |
| `Furniture/FurnitureWorld.cs` | **All furniture, shared by pets**: `SetSpecs` (the egg and gravestone specs always stay), `Add`, `AddEgg`, `EggItems`, `AddGrave`, `GraveItems`, `SetGrave`, `Remove`, `Tick` (physics via `PhysicsSystem` with the item's size, bounce, respawn), pointer drag/throw/poke, `Kick`, `PenAt`, `PenAhead`, `CreateSave`/`Load`, `Version` |
| `Furniture/RoutePlanner.cs` | Dijkstra over ground segments with walk, walk off / drop, climb up, climb down → `RouteStep`s; `SideAt` |
| `Furniture/RoomSave.cs` | `RoomSave`/`RoomItem` (an egg's `EggInfo` or a gravestone's `GraveInfo` included) and `RoomStore` (`room.json`, atomic write) |
| `Chat/ChatRules.cs` | Chat tuning (`PetConfig.Chat`): shared model settings (`Sampling` → `ChatSampling`, `ContextTokens`, `MaxSentences`/`MaxChars`), then **speech** (`ChatFrequency` scales all pets' speech rates) and **sticky notes**, two separate behaviors with their own switches, rates and gaps |
| `Breeding/BreedingRules.cs` | Breeding tuning (`PetConfig.Breeding`): switch, rate per hour, cooldown, min stat, reach, courtship and approach seconds, hatch minutes, variation; pill round minutes, visit and ask seconds, agree chance |
| `Breeding/BreedingDirector.cs` | **When pets mate**: `Tick(dt, pets, rules, roomForEgg)` rolls each close, ready pair (`Ready`: not fixed, free, stats, cooldown), runs one courtship at a time (`PetSimulation.Court` / `CourtshipArrived` / `EndCourtship`), calls it off if either stops; `TakeMatings` → `Mating(ParentA, ParentB, EggFeet)`; `Forget`. `BreedingPet(Id, Sim)`. M15 pill rounds (`TickRounds`: nearest unasked pet → `Approach` (`Seek`, then `AskToMate` + `Listen`) → `Asking` → `Refusal` or courtship), `PillRounds`, `TakeSayings` |
| `Breeding/PillLines.cs` | M15: `Saying(PetId, Text)`; the fixed ask / yes / no lines (`NoReason`: not interested, fixed, cooldown, not well) |
| `Breeding/Inheritance.cs` | `Egg(parentA, parentB, rules, random, now)` → `EggInfo`; `Cross` (each number from one parent or the average, times a variation), `Gene`. `Parent(Id, Traits, HasOwnSprites)` |
| `Death/DeathRules.cs` | Death tuning (`PetConfig.Death`): switch, `ZeroHours`, `AllCriticalHours`, `ReviveStat` |
| `Death/Mortality.cs` | M14: `Mortality.Update(stats, elapsed, counting, rules, critical, warnings)` → `DeathCause?` (per-need timers at 0, all-critical timer, pause when not counting, one warning per need until it recovers); `NeglectTimers` (saved), `DeathCause`, `PetDeath`, `PetWarning`, `WarningKind` |
| `Death/Grave.cs` | `GraveInfo` (pet id, name, born, died, cause; `From(id, name, save)`, `Story`, `Date`), `Graves` (`Kind = "grave"`, the built-in `Spec`) |
| `Breeding/Egg.cs` | `EggInfo` (laid/hatch times, parents, child traits without a name, `SpritesFrom`), `Eggs` (`Kind = "egg"`, the built-in `Spec`) |
| `Chat/IChatModel.cs` | `IChatModel.CompleteAsync(ChatPrompt, ChatSampling, ct)`, `ChatPrompt(System, User)`, `ChatSampling` (max tokens, temperature, top-p, top-k, min-p, repeat penalty) |
| `Chat/ChatDirector.cs` | **Runs speech and notes**: `Tick(dt, pets, rules, speech, notes, notesOnScreen)`, `Clicked(petId, click)` (a reaction that goes first and skips the gaps), one request at a time, timeouts, banter → answer (held until `AnswerDelaySeconds`), per-behavior gaps and recent lines (`Track`); `TakeLines` → `ChatLine`s, `TakeProblems`, `Forget`. `ChatPet` is a pet as chat sees it (incl. `Species`, `ReadWindowId`, `BehindWindowId`: a note goes on the window it stands on, else the one behind it, then with `ChatLine.NoteBehind`). `Reader` (null = reading off): remarks and reading notes read first, then ask (`ReadThenAskAsync`) |
| `Chat/ChatPrompts.cs` | `ChatKind` (Needy, Banter, Answer, Clicked, Joke, Remark; Note = a note's text), `NeedKind`; `System` (speech, mood only for needs), `NoteSystem` (writing), `Needy`/`Clicked`/`Banter`/`Answer`/`Joke`, `NoteTopics` + `Note(topic)`, `Remark`/`NoteAbout` (title + text read), `Mood` |
| `Chat/ScreenReading.cs` | M12b: `ScreenLine`, `ScreenText`, `IScreenReader` (the App's OCR reader, a fake in tests); `ScreenReading`: `Allows` (excluded apps and title words), `Area` (box around the pet, slid into the window), `Visible` (drop lines under covers), `Snippet` (nearest readable lines, reading order) |
| `Chat/TextList.cs` | A string list that compares by items (settings lists, so config records stay equal) |
| `Chat/ChatText.cs` | `ChatText.Clean(raw, name, maxSentences, maxChars)` (raw reply → bubble/note text or null); `ChatMl.Format` (Qwen ChatML, thinking off) |
| `Simulation/PetClick.cs` | `PetClick` (Petted, WokenUp, Poked): what a click did; `PetSimulation.TakeClick()` drains it |
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
| `DpiAwareness.cs` | `EnsurePerMonitorV2()`: sets PerMonitorV2 in code (called from `App`'s static constructor), for when the manifest doesn't apply (`dotnet DesktopPet.App.dll`) |
| `PetWindowInterop.cs` | Extended styles (no-activate, tool window), `SetBounds` / `SetPosition` in physical pixels, `BringToTopmost`, `PlaceBelow`, overlay styles |
| `WindowManipulator.cs` | `MoveBy(windowId, dx, dy)` with `WindowMoveGuard` against the live frame |
| `NativeIcon.cs` | `DestroyIcon` |
| `ScreenText.cs` | M12b: `WindowIdentity` (`Describe` → title + process name; `CoversOf` → other processes' windows in front), `ScreenCapture.Capture(area)` (GDI BitBlt → BGRA), `TextRecognizer` (`Windows.Media.Ocr`, 2× below 150% scale, lines with screen bounds) |
| `WindowTracker.cs` | For sticky notes: `Query(id)` → `TrackedWindowState` (Gone, Hidden, Shown) + frame; `PlaceAbove(ours, id)` keeps our window just above another in z-order |

## Chat (`src/DesktopPet.Chat/`)

| File | Purpose |
|---|---|
| `LlamaChatModel.cs` | `LoadAsync(path, useGpu, contextTokens)` (Vulkan or CPU backend chosen once per process, warm-up answer), `CompleteAsync` (off the UI thread, one at a time, `StatelessExecutor` with ChatML and stop strings), `OnGpu`, `Backend` |

---

## App (`src/DesktopPet.App/`)

| File | Purpose |
|---|---|
| `App.xaml.cs` | Composition root (also M13: `AddPet` via `PersonalityTraits.Give` (and `WriteCharacterSprites`), `LayEgg`, `Hatch`, `SetFixed`, the egg's right-click menu; M14: `OnWarned` (notifications), `OnDied` (close, gravestone), `PlaceGraves` at startup, the gravestone's menu, `Revive`, `DeleteGrave`, `RoomForPet`): single-instance mutex, data folder + migration, settings, furniture (`WriteDefaultFurniture`, `LoadFurniture` keeping the last good or default spec for a broken file, `FurnitureHost`, `AddFurniture`, furniture right-click menu, `OnFurnitureChanged`), one `PetHost` per pet folder (`StartPet`: traits rolled/written if missing, personality, layered sprites), shared `PetMenu` + `TrayIcon`, `CustomizationWatcher` callbacks (`OnSettingsChanged`, `OnSpritesChanged`, `OnPetChanged`), add/remove pets, push toggle, `ChatHost` (started last; talk and notes toggles), debug overlay timer, problem reporting (tray + `problems.txt`) |
| `PetHost.cs` | One pet: `PetSimulation` (with the shared `FurnitureWorld`) + `PetWindow` + `GameLoop` + save store, plus `Name`/`Traits`/`Personality`, `Character`/`Look` (its built-in art), `Feet`/`SizePx`, `BornUtc`, `Warned`/`Died` events (raised after `Step`; `Died` posted to the dispatcher), `BrokenSaveKept`, `SetTraits`, `WriteTraits` (menu changes, replacing the file's Traits), `TraitProblems`, `Config`, `UserAway`, `ChatInfo` (a `ChatPet`: stats, place, asleep or not, the window it stands on). `BeginFrame` (snapshot, cursor sampling while pressed) → `Step` (Tick + carry out `WindowCommand`s) → `Render` (interpolate, pick frame from `SpriteAtlas`, autosave each minute). Lock/unlock → `UserAway`. Implements `IPetControls` for the menu |
| `GameLoop.cs` | `CompositionTarget.Rendering` → `FixedStepper` → step/render; re-entrancy guarded |
| `PetWindow.xaml(.cs)` | Borderless transparent topmost window showing one `Image`; `Present(rect, bitmap, mirror, smooth)`; `Behind` (a window to stay just below: a penned pet's pen), `Handle`; raises `PointerPressed/Released/Canceled`, `MenuRequested`. Also used for furniture (`KeepOnTop = false`, so pets stay in front) |
| `Furniture/FurnitureHost.cs` | Owns the `FurnitureWorld`, its own `GameLoop`, a window per piece, `room.json` saving (once settled, and each minute); `SetSpecs`, `Add`, `AddEgg`, `AddGrave`, `Remove`, `RemoveAll` (not eggs or gravestones), `Count` (furniture only), `EggCount`, `DebugInfo`, `MaxItems`, `WindowOf(item)` (its HWND; PetHost's `FurnitureWindow`, so a penned pet draws behind the pen). `Render` is guarded against re-entry (a nested one made orphaned windows) |
| `Breeding/BreedingHost.cs` | M13: ticks `BreedingDirector` every 100 ms with each `PetHost.BreedingInfo`; `RoomForEgg` (pets + eggs below the cap); hands matings to `App.LayEgg`, due eggs to `App.Hatch` and pill-round sayings to `ChatHost.Say`; `Forget` |
| `Furniture/GraveArt.cs` | M14: `Render(grave, stone, size, scale)`: the stone art scaled to the exact pixel size with the name and dates engraved on its face (FurnitureHost caches one per gravestone and size) |
| `Furniture/FurnitureArt.cs` | A kind's frames from its PNG (cut into `Frames`), else the embedded default for that kind, else a plain box; `DefaultPng(kind)` |
| `Furniture/Art/*.png` | The default furniture art (EmbeddedResource `DesktopPet.App.Furniture.<kind>.png`). Generated; don't hand-edit |
| `SpriteSheet/DefaultArt.cs` | Built-in art: crops 64×64 frames (`FrameSize`) from a character's embedded sheet (`Frame(character, animation, frame, jacket)`), recolours the rabbit's jacket to the pet's `JacketShades`; `SheetPng(character)` (the PNG bytes, to write into a pet folder), `TrayIconPixels()` (`AppCharacter`) |
| `SpriteSheet/rabbit.png` | The rabbit's sheet (EmbeddedResource `DesktopPet.App.rabbit.png`; for pets set to `"rabbit"` and the exported template). Generated; don't hand-edit (see `docs/SPRITE_GENERATOR.md`) |
| `SpriteSheet/Fakemon/<character>.png` | The default characters' sheets (same layout as `rabbit.png`; EmbeddedResource `DesktopPet.App.Characters.<file name>`), generated from `tools/sprites/characters/<character>.py` (`Botrosa.png` from `ringbot.py`). Listed in Core's `Characters.Defaults`. Generated; don't hand-edit |
| `SpriteSheet/SpriteAtlas.cs` | (animation id, frame) → `AtlasFrame(Bitmap, FacesLeft, Smooth)`. `Load(folders, animations, look)` → `(atlas, SpriteLayer[])`: per animation the first folder whose `atlas.json` defines it (pet's own, then shared), the pet's built-in art elsewhere (`BuiltInLook(Character, Jacket)`); `CustomCount` |
| `SpriteSheet/SpriteTemplateExporter.cs` | Menu → Export sprite template: the built-in art as `template.png` + `atlas.json` + README |
| `Customization/DataFolder.cs` | `data\` paths (`settings.json`, `personality-traits.json` (`LoadPersonalityTraits`, written with the defaults if missing), `HasOwnSprites`, `CopySprites`, `WriteCharacterSprites` (a new pet's own copy of its character's sheet + atlas.json), `sprites\`, `sprite-template\`, `problems.txt`, `furniture\` (+ `defaults-written.txt`), `room.json`, `pets\<n>\` with save (and traits), personality, sprites), `ExistingPets`, `StartupPets` and `DeadPets` (saves marked dead), `MigrateLegacySaves`, `MigrateTraitsFiles` (old traits.json → save.json), `LoadTraits` (from the save), personality load and write, `RecyclePetFolder`, `FindModel` (chat model: next to the exe, then `models\` in the data folder), settings load, `WriteDefaultFurniture` (first run: all; later: defaults added since, per `defaults-written.txt`), `LoadFurniture`, `Open` |
| `Customization/CustomizationWatcher.cs` | `FileSystemWatcher` on settings, shared sprites, the furniture folder and pet folders (via `PetRoster.Classify`), 400 ms debounce per file kind and pet, callbacks on the UI thread |
| `UI/PetMenu.cs` | WinForms `ContextMenuStrip` shared by all pets (with no pets, only the app-wide items): name + mood header, stat bars, About submenu (personality, traits read-only, parents and fixed, Edit traits, Open its folder), Feed/Play/Sleep, "*name* is fixed" (writes `Fixed` into the Traits in save.json), Furniture submenu (`FurnitureActions`: add beside the pet, remove all, open folder), add/remove pet, push toggle, Customize submenu, debug overlay, Exit. Acts on the current `IPetControls` target |
| `UI/IPetControls.cs` | What the menu needs from a pet |
| `UI/TrayIcon.cs` | `NotifyIcon` with the shared menu; `Notify(title, text, warning)` |
| `UI/DebugOverlay.cs` | Click-through overlay per monitor drawing floors, window tops, walls, hang bars, pet and furniture boxes (`DebugView`, `DebugPet`) |
| `Chat/ChatHost.cs` | Loads the model in the background, ticks `ChatDirector` every display frame (`CompositionTarget.Rendering`) with `PetHost.ChatInfo`, shows lines as bubbles (stacked when they'd overlap) and notes, tracks notes against their windows every 100 ms; `Enabled` (speech), `NotesEnabled`, `ReadingEnabled` (sets `ChatDirector.Reader`), `Say(petId, text)` (a fixed line in a bubble, no model; used by pill rounds), `Forget`, `BringToFront` |
| `Chat/SpeechBubble.cs` | Click-through topmost bubble sized to its text; `Say`, `Follow` (fade, follow the head, stack above `taken`), `Silence` |
| `Chat/ScreenReader.cs` | `IScreenReader` for Core: query window, check `ScreenReading.Allows`, capture the area, OCR, keep `Visible` lines (covers: windows in front + our pets, bubbles, notes); notifies once if there's no OCR |
| `Chat/StickyNote.cs` | Note window anchored to another window by offset (of its top-left, or with `fromBottom` its bottom-left: above a pet's head); `Track(state, bounds)`; click → `Dismissed` |
| `app.manifest` | PerMonitorV2 DPI awareness (only applies when started from the .exe; see `DpiAwareness`) |

---

## Flows

**Startup:** `App.OnStartup` → mutex → `DataFolder.EnsureExists` → `MigrateLegacySaves` → `MigrateTraitsFiles` → `LoadSettings` (errors →
defaults) → `DesktopSource` → `WriteDefaultFurniture` (first run) → `FurnitureHost` → `LoadFurniture` (specs + art) →
`FurnitureHost.Start` (loads `room.json`, starts its loop) → read every startup pet's traits from its `save.json` (names first, so rolled ones avoid them) → for
each id `StartPet`: roll traits if missing (saved with the first `Save`; their character's sheet goes into the pet's `sprites\`), write personality if missing,
`SpriteAtlas.Load(SpriteFolders(id), Animations.All, look)` → `new PetHost(...)` (loads save, builds
`PetSimulation` with the traits, window, loop) → `Start` → menu, tray → one problems toast → watcher.

**Per display frame (furniture, then each pet):** `FurnitureHost`'s loop ticks `FurnitureWorld` (and feeds a drag);
then per pet: `GameLoop.OnRendering` → `PetHost.BeginFrame` (shared `DesktopSource.GetSnapshot`)
→ N × `PetHost.Step(dt)` = `PetSimulation.Tick` + `WindowManipulator.MoveBy` for each command → `PetHost.Render(alpha)`.

**Mouse:** `PetWindow` press/release/capture-lost → `PetHost` → `PetSimulation.Pointer*` with `CursorSource`
positions and `Stopwatch` timestamps; while pressed, `BeginFrame` feeds `PointerMove` every frame. Right-click →
`MenuRequested` → `App` shows `PetMenu` for that pet.

**Settings edit:** watcher → `App.OnSettingsChanged` → `PetConfigFile.Parse` + validate → on success each
`PetHost.Reconfigure` → `PetSimulation.Reconfigure`; on error keep the old config and toast.

**Sprites edit:** watcher → `App.OnSpritesChanged` → `SpriteAtlas.Load` per pet (its folders, its variant) →
`PetHost.SetAtlas`.

**Pet file edit:** watcher → `PetRoster.Classify` → `App.OnPetChanged(id, kind)`: save → `DataFolder.LoadTraits`
(`PetSaveFile.Parse`) → `PetHost.SetTraits` (unchanged, such as an autosave → nothing; errors → keep old + toast once, per `PetHost.TraitProblems`); personality → `PetHost.Personality`; sprites →
reload that pet's atlas.

**Pets and furniture:** a voluntary state ends → `PickNext` → `BehaviorPicker.Pick(context)` may roll furniture →
`FurnitureChooser.TryStart` (reserve a slot, `RoutePlanner.Plan`) → `GoToFurniture` (walk; `Climb` for climbs, which
returns to GoTo at the top; drops land and `LandState` → `PickNext` → GoTo again) → `UseFurniture` (stats gain in
`UpdateStats`) → `CancelErrand` → `PickNext`. Separately, each tick a pet idling, walking or sitting runs
`GroupPlay.TryJoin`: another pet's fun use of a nearby multi-slot piece (seen in `FurnitureItem.Users`) may draw it
over via `FurnitureChooser.TryGo` with a short `Errand.PlaySeconds`. Grabbing the pet, a menu action, or the furniture being removed or
picked up calls the trip off.

**Furniture edit:** watcher → `App.OnFurnitureChanged` → `LoadFurniture` → `FurnitureHost.SetSpecs` (kinds that are
gone are removed from the room).

**Speech and notes (each display frame):** `ChatHost.OnRendering` → `PetHost.ChatInfo` and `PetHost.TakeClick` → `ChatDirector.Clicked` for each pet → `ChatDirector.Tick` (collect a finished reply → `ChatText.Clean` → line; then maybe start one request: a click reaction, else a pending answer, else a speech roll, else a note roll → `IChatModel.CompleteAsync` on a worker thread) → `TakeLines`: speech → `SpeechBubble.Say`, notes → new `StickyNote` on the pet's window (below its title bar) or the window behind it (above its head) → bubbles `Follow` their pets; notes `Track` their windows every 100 ms.

**Death:** `PetSimulation.UpdateStats` → `Mortality.Update` (warnings → `TakeWarnings` → `PetHost.Warned` → `App.OnWarned` → tray) → once a timer runs out, `Death` is set → `PetHost.Died` (posted) → `App.OnDied` (forget in chat and breeding, `Close(save: true)` writes the dead save, `FurnitureHost.AddGrave` at its feet). Startup: `StartupPets` skips dead pets, then `PlaceGraves`. Gravestone right-click → `Revive` (`SaveData.Revived`, remove the gravestone, `StartPet` there) or `DeleteGrave` (Recycle Bin, then remove).

**Breeding (every 100 ms):** `BreedingHost.Tick` → `BreedingDirector.Tick` (a ready, close pair rolls → both `Court`; once both arrived and `CourtshipSeconds` pass → `Mating`, both `EndCourtship(mated)`) → `App.LayEgg` (`Inheritance.Egg` → `FurnitureHost.AddEgg`). Pill rounds (M15) run in the same `Tick`: a pet that finished a `Breed` use has `OnPillRound` → the director walks it to each other pet (`Seek` → `VisitState`), asks, and starts a courtship on yes; sayings → `ChatHost.Say` → eggs past `HatchAtUtc` → `App.Hatch` (remove egg, name, `PersonalityTraits.Give`, write personality and the save (traits, cooldown), copy a parent's sprites, `StartPet` at the egg).

**Saving:** `PetHost.Save` → `JsonFileSaveStore` (`pets\<n>\save.json`) each minute, after menu actions, on exit,
and on session end; `PetSaveFile.Merge` keeps the file's `Traits` as they are. The app's own trait changes
(`PetHost.WriteTraits`: "fixed", or traits rolled for a pet with none) replace them. Personality is only written when a pet is created. Removing a pet moves its folder to
the Recycle Bin.

---

## Recipes

| To... | Touch |
|---|---|
| Tune a number or add a setting | `PetConfig` (property with default + doc comment), `PetConfigValidator` (range). It serializes automatically. Mention it in PLAN.md if it's notable. Test in `PetConfigFileTests` / `ReconfigureTests` |
| Add a behavior state | `PetStateId` enum, a class in `States.cs` (`TimedState` for voluntary ones), register it in `StateMachine`'s array, pick it from `BehaviorPicker` or another state, decide its `StatActivity` in `PetSimulation.UpdateStats`, add tests under `tests/.../Behavior/` |
| Add an animation | `Animations.cs` (def + append to `All`; the order is the sheet row order), then the generator (`ORDER` + `frames()`), regenerate `rabbit.png`, play it from a state, add it to the table in `docs/SPRITES.md` |
| Change the built-in art | `tools/sprites/make_rabbit.py`, regenerate; see `docs/SPRITE_GENERATOR.md` |
| Sprites for a new character | `tools/sprites/characters/<name>.py` from `template.py`; see `docs/CHARACTER_SPRITES.md` (or the `character-sprites` skill) |
| Change per-pet colours | `JacketColors.Variants` (and the jacket colours in the generator's palette must stay unique) |
| Add a default character | Generate its sheet into `SpriteSheet/Fakemon/` (`docs/CHARACTER_SPRITES.md`), add a `CharacterInfo` (id = file name, any case) to `Characters.Defaults`; tests in `Traits/PetTraitsTests` |
| Add a per-pet trait | `Neediness`/`BehaviorTraits` (parameter default 1), `PetTraits.ApplyTo`, `PetTraitsFile.Validate`, `TraitRoller.Roll`, maybe `Personality.Describe` and the About lines in `PetMenu`; tests in `Traits/PetTraitsTests` |
| Add a menu item | `UI/PetMenu.cs`; pet-specific actions go through `IPetControls` (implemented by `PetHost`), app-wide ones are constructor callbacks wired in `App.OnStartup` |
| Use a new Win32 API | Add the name to `Interop/NativeMethods.txt`, wrap it in a small static class in Platform.Windows |
| Change what counts as a window/platform | `WindowSource` filter (Windows side) or `PlatformBuilder`/`WallBuilder` (geometry, tested) |
| Change the save format | `SaveData` (bump `CurrentVersion`; tolerate old files), `JsonFileSaveStoreTests` |
| Add a furniture kind (default) | `DefaultFurniture` (spec, appended to `All`; existing folders get it once via `ToWrite`), the generator (`FURNITURE` + a drawing function), regenerate, add it to `docs/FURNITURE.md` |
| Add a furniture use effect | `UseEffect`, `UseFurnitureState.Update`, `PetSimulation.UseActivity`, tests in `Behavior/FurnitureBehaviorTests` |
| Add a kind of spoken line | `ChatKind`, a prompt in `ChatPrompts`, a rate in `ChatRules` (+ validator), the roll in `ChatDirector.TrySpeak`, maybe a `ChatTraits` multiplier; tests in `Chat/ChatDirectorTests` |
| Change what notes are about | `ChatPrompts.NoteTopics` / `NoteSystem` |
| Change what pets may read | `ChatRules.ReadExcluded*` defaults, `ScreenReading` (area, visibility, snippet), `ScreenReader` (App glue); tests in `Chat/ScreenReadingTests`, `Chat/ChatReadingTests` |
| Change when pets die or how they're warned | `DeathRules` (+ validator), `Mortality`; the notification texts in `App.OnWarned`; tests in `Death/MortalityTests`, `Death/DeathTests` |
| Change the gravestone | Art: the generator's `grave()` (keep the plain face in step with `GraveArt`'s face constants); size: `Graves.Spec`; text: `GraveArt` |
| Change what children inherit | `Inheritance` (numbers, art), `JacketColors.Blend`; tests in `Breeding/InheritanceTests` |
| Change when pets mate | `BreedingRules` (+ validator), `BreedingDirector`; tests in `Breeding/BreedingDirectorTests` |
| Change the breeding pill | Spec: `DefaultFurniture.Pill`; who takes it: `FurnitureChooser` (`PillAppeal`, `MayTakePill`); the round and answers: `BreedingDirector` (pill rounds), `PillLines`; tests in `Breeding/BreedingPillTests` |
| Add a default personality trait | `PersonalityTraits.Defaults` (only written when the user has no `personality-traits.json`) |
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
- Chat (`Chat/ChatDirectorTests`) uses `FakeChatModel` (canned replies, or `Manual` with `Release`, or `Fail`) and a `Quiet` rules base with every rate 0 and no gaps; `FakeRandom(0.0)` makes every roll succeed.
- Furniture scenarios (`Behavior/FurnitureBehaviorTests`) use a `Room` helper that ticks the `FurnitureWorld`, advances
  the `FakeClock` and ticks the pet together; `FakeRandom(0.99)` makes the picker choose furniture when it's weighted.

## Gotchas

- A running pet locks `bin/` and the build fails: closing it to rebuild is fine (allowed for testing; see PLAN.md, Testing).
- Starting the app migrates old saves in the real data folder for good; back the folder up before live checks that
  could change it.
- `DesktopSource` ignores its own process's windows, so live checks need test windows from another process.
- Don't drive the real mouse in checks without asking.
- `DesktopSnapshot` reference equality matters: Core rebuilds platforms/walls only when the snapshot object changes.
- WinForms implicit usings are removed in the App csproj (they clash with WPF types); import WinForms types explicitly.
- Resizing a WPF window renders synchronously and can re-enter the loop; `GameLoop` guards against it.
- The chat model loads in the background: chat is quiet for the first few seconds (and for good if `models\` has no model).
- Windows PowerShell mangles double quotes in `git commit -m`; write the message to a file and use `-F`.
