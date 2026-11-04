"""Click at physical x,y then capture. Usage: python click.py <left|right> x y <out.png> [park_x park_y]"""
import ctypes
import sys
import time

ctypes.windll.shcore.SetProcessDpiAwareness(2)
from PIL import ImageGrab  # noqa: E402

u = ctypes.windll.user32
button, x, y, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
down, up = (0x0008, 0x0010) if button == "right" else (0x0002, 0x0004)
u.SetCursorPos(x, y)
time.sleep(0.15)
u.mouse_event(down, 0, 0, 0, 0)
time.sleep(0.05)
u.mouse_event(up, 0, 0, 0, 0)
if len(sys.argv) > 6:
    time.sleep(0.3)
    u.SetCursorPos(int(sys.argv[5]), int(sys.argv[6]))
time.sleep(1.2)
ImageGrab.grab(bbox=(0, 0, 2560, 1440), all_screens=True).save(out)
print("saved", out)
