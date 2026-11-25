# Comic floor textures (multiplied by each floor's colour in the toon shader): inked plank seams with staggered
# end joints, a few grain strokes and knots; inked grout for tiles. Grey-scale, tileable, 1024 px.
#   python3 Tools/comic-textures/make_floors.py
import random, math
from PIL import Image, ImageDraw, ImageFilter
OUT = "Assets/MicroduckVR/Textures/"
S = 1024
INK = (70, 58, 92)        # seams: dark ink-violet (multiplied with the floor colour)

def planks(name, rows=4, seed=1):
    r = random.Random(seed)
    im = Image.new("RGB", (S, S), (255, 255, 255)); d = ImageDraw.Draw(im)
    h = S // rows
    for i in range(rows):
        y0 = i * h
        # a slightly different tone per plank
        tone = 255 - r.randint(0, 14)
        d.rectangle([0, y0, S, y0 + h], fill=(tone, tone, tone))
        # grain: long gentle strokes
        for k in range(r.randint(3, 5)):
            yy = y0 + r.randint(12, h - 12); amp = r.uniform(1.5, 4); ph = r.uniform(0, 6.28)
            x0 = r.randint(-200, S // 2); L = r.randint(S // 3, S)
            pts = [(x, yy + amp * math.sin(ph + x / r.uniform(160, 300))) for x in range(x0, x0 + L, 8)]
            d.line(pts, fill=(tone - 75, tone - 78, tone - 62), width=4)
        # a knot now and then
        if r.random() < 0.6:
            kx, ky = r.randint(60, S - 60), y0 + r.randint(24, h - 24)
            d.ellipse([kx - 16, ky - 8, kx + 16, ky + 8], outline=(tone - 60, tone - 60, tone - 50), width=4)
        # end joints, staggered (they wrap so the texture tiles)
        off = (i * 397) % S
        for j in range(2):
            x = (off + j * S // 2) % S
            d.line([(x, y0), (x, y0 + h)], fill=INK, width=7)
        d.line([(0, y0), (S, y0)], fill=INK, width=8)
    im = im.filter(ImageFilter.GaussianBlur(0.6))
    im.save(OUT + name)

def tiles(name, n=4, seed=2):
    r = random.Random(seed)
    im = Image.new("RGB", (S, S), (255, 255, 255)); d = ImageDraw.Draw(im)
    c = S // n
    for i in range(n):
        for j in range(n):
            tone = 255 - r.randint(0, 10)
            d.rectangle([i * c, j * c, i * c + c, j * c + c], fill=(tone, tone, tone))
            # a comic shine stroke in one corner of some tiles
            if r.random() < 0.5:
                x, y = i * c + 26, j * c + 26
                d.line([(x, y + 40), (x + 40, y)], fill=(255, 255, 255), width=8)
    for k in range(n + 1):
        d.line([(k * c, 0), (k * c, S)], fill=(150, 140, 168), width=12)
        d.line([(0, k * c), (S, k * c)], fill=(150, 140, 168), width=12)
        d.line([(k * c, 0), (k * c, S)], fill=INK, width=4)
        d.line([(0, k * c), (S, k * c)], fill=INK, width=4)
    im.save(OUT + name)

planks("floor_planks.png", 4, 1)
planks("floor_planks_narrow.png", 6, 3)
tiles("floor_tiles.png", 4, 2)
print("ok")
