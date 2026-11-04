"""Demo stage for DesktopPet screenshots: a plain backdrop (untitled, so the pets ignore it) plus titled demo windows.

Usage: python stage.py <scene>   scenes: hero, windows, chat, plain
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
top, bottom = (0xC9, 0xE4, 0xF2), (0xF4, 0xE9, 0xF5)
steps = 120
for i in range(steps):
    t = i / (steps - 1)
    c = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom))
    y0 = H * i // steps
    y1 = H * (i + 1) // steps + 1
    canvas.create_rectangle(0, y0, W, y1, fill="#%02x%02x%02x" % c, outline="")


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
    "Carrot cake\n\n"
    "2 cups grated carrots\n"
    "1 1/2 cups flour\n"
    "1 cup brown sugar\n"
    "3 eggs\n"
    "1 tsp cinnamon\n\n"
    "Bake at 175 C for 35 minutes."
)

# Window outer frame positions are approximate; tk geometry is the client area.
if scene == "hero":
    window("Weekend plans", 880, 560, 760, 780, PLANS)
elif scene == "windows":
    window("Weekend plans", 520, 520, 720, 820, PLANS)
    window("Carrot cake", 1480, 860, 620, 480, RECIPE)
elif scene == "chat":
    window("Weekend plans", 820, 640, 900, 700, PLANS, size=17)

root.update()
for w in root.winfo_children():
    if w is not back:
        w.lift()
        w.focus_force()
root.mainloop()
