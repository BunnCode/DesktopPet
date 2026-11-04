"""Contact sheet of captured frames, labelled by index. Usage: python sheet.py <dir> <prefix> <out.png> [cols] [thumb_w]"""
import glob
import os
import sys

from PIL import Image, ImageDraw

d, prefix, out = sys.argv[1], sys.argv[2], sys.argv[3]
cols = int(sys.argv[4]) if len(sys.argv) > 4 else 4
tw = int(sys.argv[5]) if len(sys.argv) > 5 else 480
files = sorted(glob.glob(os.path.join(d, f"{prefix}-*.png")))
ims = [Image.open(f) for f in files]
th = round(ims[0].height * tw / ims[0].width)
rows = (len(ims) + cols - 1) // cols
sheet = Image.new("RGB", (cols * tw, rows * (th + 18)), "white")
draw = ImageDraw.Draw(sheet)
for i, (f, im) in enumerate(zip(files, ims)):
    x, y = (i % cols) * tw, (i // cols) * (th + 18)
    sheet.paste(im.convert("RGB").resize((tw, th), Image.LANCZOS), (x, y + 18))
    draw.text((x + 4, y + 2), os.path.basename(f), fill="black")
sheet.save(out)
print(out, len(ims))
