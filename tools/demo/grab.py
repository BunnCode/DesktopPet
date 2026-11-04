"""DPI-aware burst capture of a screen region.

Usage: python grab.py <outdir> <prefix> <count> <interval_s> [left top right bottom]
"""
import ctypes
import os
import sys
import time

ctypes.windll.shcore.SetProcessDpiAwareness(2)
from PIL import ImageGrab  # noqa: E402

out, prefix, count, interval = sys.argv[1], sys.argv[2], int(sys.argv[3]), float(sys.argv[4])
box = tuple(int(v) for v in sys.argv[5:9]) if len(sys.argv) >= 9 else (0, 0, 2560, 1440)
os.makedirs(out, exist_ok=True)
for i in range(count):
    ImageGrab.grab(bbox=box, all_screens=True).save(os.path.join(out, f"{prefix}-{i:03d}.png"))
    if i + 1 < count:
        time.sleep(interval)
print("done", count)
