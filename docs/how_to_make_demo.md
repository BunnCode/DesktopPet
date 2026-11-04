# How to make demo screenshots

The README screenshots (`docs/screenshots/`) are staged with demo data on a plain backdrop. The scripts are in
`tools/demo/`; they assume a 2560×1440 primary monitor at 125% (work area bottom at y = 1380), so adjust `W`, `H` and
`FLOOR` for another setup.

1. **Back up the real data.** Quit the pet, copy `data` aside, then empty it. Renaming it may
   fail if something holds the folder open.
2. **Write demo data:** `python tools/demo/make_demo.py data <base|needy|court>`. This copies the pets from `publish/data/pets` (stats reset) and writes
   four pets, a dead pet (Clover), furniture, an egg, a gravestone and chat-heavy settings. `needy` lowers the stats
   so the pets use the furniture; `court` turns breeding up. Also copy the default `furniture\*.json/png` in. If the
   `furniture` folder already exists, the app only writes the newest kinds.
3. **Start the stage, then the app:** `pythonw tools/demo/stage.py <hero|windows|chat|plain>`. The backdrop is a
   *maximized, titled* window: it hides the real desktop from the pets but gives them no platform. Use `pythonw` so
   no console pops up.
4. **Capture bursts:** `python tools/demo/grab.py <outdir> <prefix> 60 3 0 32 2560 1380` (DPI-aware; this region
   skips the backdrop's title bar and the taskbar). Then `sheet.py` makes a contact sheet to pick frames from.
5. **Menu and debug overlay** need the mouse, so ask the user first: `python tools/demo/click.py right <x> <y> out.png`
   right-clicks a pet. Click "Debug overlay" the same way with `left`.
6. **Crop** the chosen frames with Pillow into `docs/screenshots/`, cropping out the taskbar and the backdrop's title
   bar.
7. **Restore** the real data folder and check it against the backup (file count and hashes).

Tips:
- For the most climbing, set Walk weight high and `ClimbChance` up. `ClimbChance + PushChance` must stay ≤ 1, or the
  whole settings file is rejected.
- Settings reload live, but restart the app between scenes so the pets start fresh.
