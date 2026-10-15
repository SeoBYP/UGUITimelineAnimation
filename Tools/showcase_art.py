"""Art for the Showcase sample (Assets/UIAnimation/Samples/Showcase/Art), drawn from scratch so it can ship with the
package: per theme a background, a panel, a button and a bar (shapes meant for 9-slicing), plus shared icons.
Run: python Tools/showcase_art.py  (needs Pillow). Import settings (sprite borders) are set by the builder."""
import math, os
from PIL import Image, ImageDraw, ImageFilter

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'UIAnimation', 'Samples', 'Showcase', 'Art')
S = 4   # supersampling


def big(w, h): return Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
def small(img, w, h): return img.resize((w, h), Image.LANCZOS)


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)


def gradient(w, h, top, bottom):
    img = Image.new('RGBA', (w, h))
    px = img.load()
    for y in range(h):
        t = y / (h - 1)
        c = tuple(int(top[i] + (bottom[i] - top[i]) * t) for i in range(3)) + (255,)
        for x in range(w): px[x, y] = c
    return img


def chamfer(x0, y0, x1, y1, c):   # rectangle with cut corners
    return [(x0 + c, y0), (x1 - c, y0), (x1, y0 + c), (x1, y1 - c), (x1 - c, y1), (x0 + c, y1), (x0, y1 - c), (x0, y0 + c)]


# ---------- backgrounds (1920x1080) ----------
def bg_casual():
    bubbles = Image.new('RGBA', (1920, 1080))
    d = ImageDraw.Draw(bubbles)
    for x, y, r in [(220, 860, 260), (1650, 180, 200), (1500, 900, 140), (500, 160, 90), (980, 980, 120)]:
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 255, 255, 60))
    return Image.alpha_composite(gradient(1920, 1080, (120, 200, 255), (255, 214, 240)), bubbles)


def bg_scifi():
    img = gradient(1920, 1080, (8, 14, 32), (4, 30, 52))
    d = ImageDraw.Draw(img, 'RGBA')
    for x in range(0, 1920, 64): d.line([(x, 0), (x, 1080)], fill=(60, 220, 255, 26), width=1)
    for y in range(0, 1080, 64): d.line([(0, y), (1920, y)], fill=(60, 220, 255, 26), width=1)
    glow = Image.new('RGBA', (1920, 1080))
    ImageDraw.Draw(glow).ellipse([560, 140, 1360, 940], fill=(40, 200, 255, 60))
    return Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(160)))


def bg_fantasy():
    img = gradient(1920, 1080, (36, 22, 58), (18, 40, 34))
    d = ImageDraw.Draw(img, 'RGBA')
    for i in range(70):
        x, y, r = (i * 733) % 1920, (i * 397) % 760, 2 + i % 3
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 236, 170, 120 + (i * 13) % 120))
    vign = Image.new('RGBA', (1920, 1080))
    v = ImageDraw.Draw(vign)
    v.rectangle([0, 0, 1920, 1080], fill=(0, 0, 0, 140))
    v.ellipse([-200, -200, 2120, 1280], fill=(0, 0, 0, 0))
    return Image.alpha_composite(img, vign.filter(ImageFilter.GaussianBlur(120)))


# ---------- 9-slice shapes (128x128, border 40) ----------
def shape(theme, kind):
    img = big(128, 128)
    d = ImageDraw.Draw(img)
    w = h = 128 * S - 1
    if theme == 'Casual':
        fill = {'panel': (255, 255, 255, 255), 'button': (255, 200, 60, 255), 'bar': (255, 255, 255, 235)}[kind]
        if kind == 'button':
            d.rounded_rectangle([0, 0, w, h], radius=36 * S, fill=(230, 150, 30, 255))
            d.rounded_rectangle([0, 0, w, h - 10 * S], radius=36 * S, fill=fill)
        else:
            d.rounded_rectangle([0, 0, w, h], radius=36 * S, fill=fill)
    elif theme == 'SciFi':
        fill = {'panel': (10, 26, 46, 252), 'button': (12, 60, 80, 252), 'bar': (6, 18, 34, 252)}[kind]
        d.polygon(chamfer(0, 0, w, h, 26 * S), fill=(80, 230, 255, 255))
        d.polygon(chamfer(5 * S, 5 * S, w - 5 * S, h - 5 * S, 24 * S), fill=fill)
    else:
        fill = {'panel': (240, 224, 186, 255), 'button': (120, 70, 30, 255), 'bar': (60, 36, 24, 240)}[kind]
        edge = (214, 170, 80, 255)
        d.rounded_rectangle([0, 0, w, h], radius=18 * S, fill=edge)
        d.rounded_rectangle([7 * S, 7 * S, w - 7 * S, h - 7 * S], radius=14 * S, fill=fill)
        d.rounded_rectangle([13 * S, 13 * S, w - 13 * S, h - 13 * S], radius=10 * S, outline=edge, width=2 * S)
    return small(img, 128, 128)


# ---------- icons (256x256, colored, shared by the themes) ----------
def icon(name):
    img = big(256, 256)
    d = ImageDraw.Draw(img)
    s = 256 * S
    c = s / 2

    def circle(cx, cy, r, **k): d.ellipse([cx - r, cy - r, cx + r, cy + r], **k)

    if name == 'coin':
        circle(c, c, s * .44, fill=(230, 160, 20)); circle(c, c, s * .36, fill=(255, 210, 60))
        d.rectangle([c - s * .05, c - s * .2, c + s * .05, c + s * .2], fill=(230, 160, 20))
    elif name == 'gem':
        d.polygon([(c, s * .08), (s * .9, s * .38), (c, s * .94), (s * .1, s * .38)], fill=(80, 200, 255))
        d.polygon([(c, s * .08), (s * .9, s * .38), (c, s * .5)], fill=(160, 235, 255))
        d.polygon([(s * .1, s * .38), (c, s * .5), (c, s * .94)], fill=(40, 140, 220))
    elif name == 'potion':
        d.rectangle([s * .4, s * .08, s * .6, s * .3], fill=(200, 200, 220))
        circle(c, s * .62, s * .3, fill=(230, 60, 120)); circle(s * .42, s * .55, s * .07, fill=(255, 200, 220))
    elif name == 'sword':
        d.polygon([(c, s * .04), (s * .58, s * .62), (s * .42, s * .62)], fill=(220, 230, 240))
        d.rectangle([s * .26, s * .62, s * .74, s * .68], fill=(200, 150, 40))
        d.rectangle([s * .46, s * .68, s * .54, s * .9], fill=(120, 70, 30)); circle(c, s * .93, s * .05, fill=(200, 150, 40))
    elif name == 'shield':
        d.polygon([(s * .14, s * .1), (s * .86, s * .1), (s * .86, s * .5), (c, s * .95), (s * .14, s * .5)], fill=(70, 110, 200))
        d.polygon([(c, s * .1), (s * .86, s * .1), (s * .86, s * .5), (c, s * .95)], fill=(50, 80, 160))
        circle(c, s * .42, s * .12, fill=(255, 210, 60))
    elif name == 'scroll':
        d.rounded_rectangle([s * .2, s * .15, s * .8, s * .85], radius=s * .06, fill=(240, 220, 170))
        for i in range(4): d.line([(s * .3, s * (.32 + i * .12)), (s * .7, s * (.32 + i * .12))], fill=(150, 110, 60), width=S * 6)
    elif name == 'chest':
        d.rounded_rectangle([s * .1, s * .3, s * .9, s * .5], radius=s * .08, fill=(170, 100, 40))
        d.rectangle([s * .1, s * .48, s * .9, s * .88], fill=(140, 80, 30))
        d.rectangle([s * .1, s * .46, s * .9, s * .52], fill=(240, 190, 60)); d.rectangle([s * .44, s * .4, s * .56, s * .62], fill=(240, 190, 60))
    elif name == 'star':
        d.polygon([(c + math.cos(math.radians(-90 + i * 36)) * s * (.46 if i % 2 == 0 else .2),
                    c + math.sin(math.radians(-90 + i * 36)) * s * (.46 if i % 2 == 0 else .2)) for i in range(10)], fill=(255, 220, 70))
    elif name == 'rays':   # white, tinted per theme
        for i in range(12):
            a0, a1 = math.radians(i * 30 - 6), math.radians(i * 30 + 6)
            d.polygon([(c, c), (c + math.cos(a0) * s, c + math.sin(a0) * s), (c + math.cos(a1) * s, c + math.sin(a1) * s)], fill=(255, 255, 255, 150))
        img = img.filter(ImageFilter.GaussianBlur(S * 3))
    elif name == 'glow':   # soft white disc
        circle(c, c, s * .3, fill=(255, 255, 255, 255))
        img = img.filter(ImageFilter.GaussianBlur(S * 24))
    return small(img, 256, 256)


for theme, bg in [('Casual', bg_casual), ('SciFi', bg_scifi), ('Fantasy', bg_fantasy)]:
    save(bg(), theme, 'bg.png')
    for kind in ('panel', 'button', 'bar'): save(shape(theme, kind), theme, kind + '.png')
for n in ('coin', 'gem', 'potion', 'sword', 'shield', 'scroll', 'chest', 'star', 'rays', 'glow'):
    save(icon(n), 'Icons', n + '.png')
print('done')
