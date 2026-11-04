"""python3 viewer/sheet.py out.png shotdir name1 name2 ... → Raster der Einzelbilder"""
import sys
from PIL import Image
out, d, *names = sys.argv[1:]
ims = [Image.open(f'{d}/{n}.png') for n in names]
w, h = ims[0].size
cols = min(6, len(ims)) if w < 500 else 2
rows = (len(ims) + cols - 1) // cols
S = Image.new('RGB', (w * cols, h * rows), (20, 16, 24))
for i, im in enumerate(ims):
    S.paste(im, ((i % cols) * w, (i // cols) * h))
S.save(out)
