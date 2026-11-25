"""Comic sky backdrop: a blue gradient with a halftone dot fade towards the horizon. Tiles horizontally.
Output: Assets/MicroduckVR/Textures/sky_halftone.png (mapped on the sky drum, v = 0.1 is the horizon)."""
from PIL import Image, ImageDraw
import os
W = H = 1024; S = 4                      # supersample
top, mid, low = (86, 178, 240), (154, 216, 242), (222, 244, 252)
im = Image.new("RGB", (W * S, H * S)); d = ImageDraw.Draw(im)
def lerp(a, b, t): return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))
for y in range(H * S):
    v = 1 - y / (H * S)                 # 0 bottom .. 1 top
    c = low if v < 0.1 else lerp(low, mid, (v - 0.1) / 0.3) if v < 0.4 else lerp(mid, top, min(1, (v - 0.4) / 0.5))
    d.line([(0, y), (W * S, y)], fill=c)
step = 32 * S                           # 32 columns across: tiles cleanly
row = 0
y = int((1 - 0.1) * H * S)
while y > (1 - 0.62) * H * S:
    v = 1 - y / (H * S); k = max(0.0, 1 - (v - 0.1) / 0.5)
    r = step * 0.42 * k ** 0.8
    if r > S * 0.6:
        off = step / 2 if row % 2 else 0
        for i in range(-1, W * S // step + 2):
            x = i * step + off
            d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255))
    y -= int(step * 0.5); row += 1
im = im.resize((W, H), Image.LANCZOS)
out = os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "MicroduckVR", "Textures", "sky_halftone.png")
im.save(out); print(out)
