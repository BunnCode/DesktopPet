# DesktopPet

Shimeji-style desktop pet for Windows (C# / .NET 10 / WPF).

**`docs/PLAN.md` is the source of truth for architecture, status and plans.** Read it before planning, starting a
milestone or making design decisions; the user edits it directly, and their edits override earlier conversations.
Update it (status table, changes from plan, limitations, next steps) when a milestone lands or a design changes.

**`docs/CODEMAP.md` maps the code** (every file's purpose, key types, frame flow, recipes for common changes). Read it
before exploring the source, and open only the files it points to. Keep it current when files, public types or flows
change. The built-in sprite generator (`tools/sprites/make_rabbit.py`) is documented in `docs/SPRITE_GENERATOR.md`.

## Rules
- `DesktopPet.Core` stays free of WPF/Win32 references. Game logic goes there, with unit tests against `FakeDesktop`.
  The Windows layer and App stay thin glue.
- Build and test: `dotnet build DesktopPet.slnx` and `dotnet test DesktopPet.slnx` (warnings are errors).
- A running pet locks `src/DesktopPet.App/bin`; ask before closing the user's instance.
- Don't drive the user's real mouse (synthetic input) without asking first.
- Live checks against the real desktop need test windows from a separate process: `DesktopSource` ignores its own
  process's windows.
- Commit only when asked. With Windows PowerShell, write commit messages to a file and use `git commit -F`, because
  double quotes inside `-m` arguments get mangled.
