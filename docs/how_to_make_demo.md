# How to make demo screenshots

The README screenshots (`docs/screenshots/`) are staged with demo data on a pixel-art forest backdrop. The scripts are
in `tools/demo/`; they assume a 2560×1440 primary monitor at 125% (work area bottom at y = 1380), so adjust `W`, `H`
and `FLOOR` for another setup.

1. **Run a separate copy of the app,** so the real data folder is never touched: quit the pet, build, and copy
   `src/Deskling.App/bin/Debug/net10.0-windows10.0.19041.0` to a folder **outside the repo** (for example in your
   temp folder). Outside the repo the app uses the `data` folder next to its exe. Only one copy runs at a time.
2. **Make the backdrop:** `python tools/demo/forest.py forest.png`. A pastel forest drawn in the pets' style (flat
   colours, light from the upper left, the sprites' outline colour on the nearest things), upscaled 2× with nearest
   neighbour. It takes about 20 s.
3. **Write demo data** into the copy's `data` folder (delete the old one first):
   `python tools/demo/make_demo.py <copy>\data <variant>`. Six pets, one per default character, plus a dead pet
   (Clover) for the gravestone. Variants: `base` (furniture, an egg and a gravestone; chat heavy), `climb` (walking and
   climbing turned up), `needy` (low stats, so the pets use the bed, trough and couch), `court` (breeding turned up),
   `lounge` (only the Living Tower, happiness low) and `games` (only the TV, happiness low). The app writes the
   default furniture files itself.
4. **Start the stage, then the app:** `python tools/demo/stage.py <hero|windows|chat|plain> forest.png`. The backdrop
   is a *maximized, titled* window: it hides the real desktop from the pets but gives them no platform. Start it with
   `Start-Process python -NoNewWindow` (with `-WindowStyle Hidden`, tk's windows stay hidden too). Pets leave sticky
   notes on the backdrop when there's no other window behind them.
5. **Capture bursts:** `python tools/demo/grab.py <outdir> <prefix> 30 3 0 32 2560 1380` (DPI-aware; this region
   skips the backdrop's title bar and the taskbar). Then `sheet.py` makes a contact sheet to pick frames from. Give
   the chat model 20–30 s to load before capturing. Watch for system pop-ups (sync clients and the like) in frames.
6. **Menu and debug overlay** need the mouse, so ask the user first. Set `Walk` to 0 in the copy's `settings.json` so
   the pets stand still, then `python tools/demo/click.py right <x> <y> out.png` right-clicks a pet. Click "Debug
   overlay" right away (the menu closes after a short while, and a click that misses raises the backdrop over the demo
   windows).
7. **Crop** the chosen frames with Pillow into `docs/screenshots/`, cropping out the taskbar and the backdrop's title
   bar. Small crops (the tower, the TV, eggs, the gravestone, furniture) read better upscaled 2–3× with nearest
   neighbour.

Tips:
- For the most climbing, set Walk weight high and `ClimbChance` up. `ClimbChance + PushChance` must stay ≤ 1, or the
  whole settings file is rejected.
- Settings reload live, but restart the app between scenes so the pets start fresh.
- The `lounge` and `games` variants leave out the dead pet: the app drops a missing gravestone in wherever it likes.
