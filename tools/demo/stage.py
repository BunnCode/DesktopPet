"""Demo stage for Deskling screenshots: a backdrop (maximized, so the pets ignore it) plus titled demo windows.

Usage: python stage.py <scene> [backdrop.png]   scenes: hero, windows, chat, plain
The backdrop image (make one with forest.py) is pinned to the bottom of the work area, where the pets stand; without
one it's a plain gradient.
"""
import ctypes
import sys
import tkinter as tk

ctypes.windll.shcore.SetProcessDpiAwareness(2)

W, H = 2560, 1380  # primary monitor work area (physical px)
scene = sys.argv[1] if len(sys.argv) > 1 else "hero"

root = tk.Tk()
root.withdraw()

# Backdrop: a maximized, titled window. It hides the real desktop from the pets (it occludes windows behind it) but
# a maximized window has no top platform or walls.
back = tk.Toplevel(root)
back.title("Backdrop")
back.geometry(f"{W}x{H}+0+0")
back.state("zoomed")
canvas = tk.Canvas(back, width=W, height=H, highlightthickness=0)
canvas.pack()
image = sys.argv[2] if len(sys.argv) > 2 else None
top, bottom = (0xC9, 0xE4, 0xF2), (0xF4, 0xE9, 0xF5)
steps = 120
for i in range(steps):
    t = i / (steps - 1)
    c = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom))
    y0 = H * i // steps
    y1 = H * (i + 1) // steps + 1
    canvas.create_rectangle(0, y0, W, y1, fill="#%02x%02x%02x" % c, outline="")
if image:
    photo = tk.PhotoImage(file=image)
    back.update()
    # The canvas starts below the title bar: put the image's bottom on the work area's bottom edge.
    canvas.create_image(0, H - back.winfo_rooty(), image=photo, anchor="sw")


def window(title, x, y, w, h, body, size=15):
    win = tk.Toplevel(root)
    win.title(title)
    win.geometry(f"{w}x{h}+{x}+{y}")
    win.configure(bg="white")
    text = tk.Text(win, font=("Segoe UI", size), wrap="word", bd=0, padx=28, pady=22,
                   bg="white", fg="#222", highlightthickness=0)
    text.insert("1.0", body)
    text.configure(state="disabled")
    text.pack(fill="both", expand=True)
    return win


PLANS = (
    "Weekend plans\n\n"
    "- Farmers market on Saturday morning\n"
    "- Buy carrots (lots of carrots)\n"
    "- Finish the birdhouse\n"
    "- Call Grandma\n"
    "- Picnic in the park if it's sunny\n"
    "- Water the tomatoes\n"
)
RECIPE = (
    "Forest berry pie\n\n"
    "2 cups wild blueberries\n"
    "1 cup raspberries\n"
    "3/4 cup sugar\n"
    "2 tbsp cornflour\n"
    "1 pie crust\n\n"
    "Bake at 200 C for 40 minutes."
)

# tk geometry is the client area; the frame adds a title bar above and a thin border. Windows reach down to the
# floor (y = H) so the pets can climb their sides.
if scene == "hero":
    window("Weekend plans", 1040, 950, 600, 410, PLANS)
elif scene == "windows":
    window("Weekend plans", 640, 760, 600, 600, PLANS)
    window("Forest berry pie", 1480, 1010, 520, 350, RECIPE)
elif scene == "chat":
    window("Weekend plans", 900, 860, 760, 500, PLANS, size=17)

root.update()
for w in root.winfo_children():
    if w is not back:
        w.lift()
        w.focus_force()
root.mainloop()
