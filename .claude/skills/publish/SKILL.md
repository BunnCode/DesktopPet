---
name: publish
description: Build, test and publish DesktopPet as a self-contained single-file exe in publish\, then copy the repo's data\ folder (settings, pets, furniture, sprites) next to it in publish\data\. Use when the user asks to build/publish/package the app, make a release build, or refresh the data that ships with the exe.
---

# Publish with data

Run (PowerShell tool, from the repo root):

```
powershell -ExecutionPolicy Bypass -File tools\publish\publish.ps1
```

Options: `-SkipTests` (skip `dotnet test`), `-Mirror` (make `publish\data` an exact copy of `data\`, deleting files
only the publish copy has; only when the user asks for an exact copy).

What it does:
1. Stops any running `DesktopPet.App` (allowed without asking; it locks `bin\` / `publish\`).
2. `dotnet build` and `dotnet test` on `DesktopPet.slnx` (warnings are errors).
3. `dotnet publish` with the same arguments as the VS Code `publish` task (Release, win-x64, self-contained,
   single file). The chat model goes to `publish\models\`, LLamaSharp natives to `publish\runtimes\`.
4. Backs up the old `publish\data` to `%TEMP%\DesktopPet-backups\publish-data-<time>\`, then copies `data\` into
   `publish\data\` (newer files win; nothing is deleted without `-Mirror`).

Afterwards tell the user:
- whether build/tests/publish passed (with the failing output if not), and the backup path;
- the "In publish\data but not in data\" list, if any: those are files the repo copy has lost (or the publish copy
  has extra), worth a look;
- which pet instances were stopped, and offer to restart them.

Note: an exe run from `publish\` inside the repo still uses the repo's `data\` (`DataFolder` walks up to
`DesktopPet.slnx`). `publish\data` is what's used once the `publish` folder is copied or zipped somewhere else.
