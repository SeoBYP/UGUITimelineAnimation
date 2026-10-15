"""Art for the RPG sample (Assets/UIAnimation/Samples/RPG/Art).

- Frames, backgrounds and effects are drawn here from scratch.
- Icons and hero faces are game-icons.net icons (CC BY 3.0, see Art/Icons/CREDITS.md), colored and shaded here.

Run: python Tools/rpg_art.py <game-icons folder: .../icons/ffffff/transparent/1x1>   (needs Pillow)
Import settings (sprite borders) are set by the builder."""
import math, os, random, sys
from PIL import Image, ImageChops, ImageDraw, ImageFilter

ICONS = sys.argv[1] if len(sys.argv) > 1 else None
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), '..', 'Assets', 'UIAnimation', 'Samples', 'RPG', 'Art')
S = 4   # supersampling
GOLD, GOLD_DARK, INK = (226, 180, 92), (150, 104, 40), (32, 20, 14)


def big(w, h): return Image.new('RGBA', (w * S, h * S), (0, 0, 0, 0))
def small(img, w, h): return img.resize((w, h), Image.LANCZOS)


def save(img, *path):
    p = os.path.join(OUT, *path)
    os.makedirs(os.path.dirname(p), exist_ok=True)
    img.save(p)


def vgradient(w, h, top, bottom):
    t = Image.linear_gradient('L').resize((w, h))
    return Image.composite(Image.new('RGBA', (w, h), bottom + (255,)), Image.new('RGBA', (w, h), top + (255,)), t)


# ---------- backgrounds (1920x1080) ----------
# Translucent shapes go on their own layer and are composited: ImageDraw would write their alpha into the
# background itself, leaving see-through pixels in what must be an opaque page background.
def stars(img, count, seed, max_y=1.0):
    rnd = random.Random(seed)
    layer = Image.new('RGBA', img.size)
    d = ImageDraw.Draw(layer)
    for _ in range(count):
        x, y, r = rnd.randrange(1920), rnd.randrange(int(1080 * max_y)), rnd.choice([1, 1, 2, 2, 3])
        d.ellipse([x - r, y - r, x + r, y + r], fill=(255, 240, 200, rnd.randrange(90, 230)))
    img.alpha_composite(layer)


def vignette(img, strength=150):
    v = Image.new('RGBA', img.size)
    d = ImageDraw.Draw(v)
    d.rectangle([0, 0, *img.size], fill=(0, 0, 0, strength))
    d.ellipse([-260, -240, img.size[0] + 260, img.size[1] + 240], fill=(0, 0, 0, 0))
    return Image.alpha_composite(img, v.filter(ImageFilter.GaussianBlur(140)))


def bg_lobby():
    img = vgradient(1920, 1080, (28, 22, 64), (74, 46, 70))
    stars(img, 160, 1, 0.6)
    glow = Image.new('RGBA', img.size)
    ImageDraw.Draw(glow).ellipse([1330, 90, 1530, 290], fill=(255, 236, 190, 255))   # moon
    img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(40)))
    d = ImageDraw.Draw(img)
    d.ellipse([1360, 120, 1500, 260], fill=(255, 244, 214, 255))
    # hills and a castle silhouette
    d.polygon([(0, 820), (300, 700), (620, 790), (980, 690), (1360, 780), (1920, 680), (1920, 1080), (0, 1080)], fill=(36, 26, 48, 255))
    castle = (22, 16, 30, 255)
    for x, w, h in [(1180, 90, 300), (1290, 140, 220), (1450, 80, 340), (1550, 120, 240), (1690, 70, 280)]:
        d.rectangle([x, 760 - h, x + w, 900], fill=castle)
        for i in range(0, w, 24): d.rectangle([x + i, 760 - h - 18, x + i + 12, 760 - h], fill=castle)
    for x in (1210, 1480, 1710): d.polygon([(x - 10, 460 if x == 1480 else 470), (x + 30, 380 if x == 1480 else 430), (x + 70, 460 if x == 1480 else 470)], fill=castle)
    for x, y in [(1320, 600), (1480, 520), (1580, 640), (1220, 560)]: d.rectangle([x, y, x + 14, y + 22], fill=(255, 200, 110, 255))
    d.polygon([(0, 900), (500, 840), (1000, 920), (1500, 860), (1920, 900), (1920, 1080), (0, 1080)], fill=(18, 12, 22, 255))
    return vignette(img, 120)


def bg_hall():   # sub pages: dim stone hall
    img = vgradient(1920, 1080, (44, 32, 30), (20, 14, 14))
    lines = Image.new('RGBA', img.size)
    d = ImageDraw.Draw(lines)
    for x in range(0, 1920, 240):
        d.rectangle([x, 0, x + 6, 1080], fill=(0, 0, 0, 50))
        for y in range(0, 1080, 120): d.rectangle([x, y + (60 if (x // 240) % 2 else 0), x + 240, y + (60 if (x // 240) % 2 else 0) + 4], fill=(0, 0, 0, 40))
    img.alpha_composite(lines)
    glow = Image.new('RGBA', img.size)
    ImageDraw.Draw(glow).ellipse([360, 80, 1560, 900], fill=(255, 170, 80, 60))
    return vignette(Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(180))), 170)


def bg_summon():
    img = vgradient(1920, 1080, (16, 8, 34), (40, 14, 52))
    stars(img, 220, 7)
    glow = Image.new('RGBA', img.size)
    ImageDraw.Draw(glow).ellipse([560, 300, 1360, 1000], fill=(170, 90, 255, 90))
    return vignette(Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(160))), 150)


# ---------- 9-slice frames (128x128, border 44) ----------
def frame(kind):
    img = big(128, 128)
    d = ImageDraw.Draw(img)
    w = h = 128 * S - 1
    def rr(inset, r, **k): d.rounded_rectangle([inset * S, inset * S, w - inset * S, h - inset * S], radius=r * S, **k)
    if kind == 'panel':          # parchment with a gold edge
        rr(0, 18, fill=GOLD); rr(5, 14, fill=GOLD_DARK); rr(8, 12, fill=(242, 226, 190)); rr(14, 8, outline=(230, 190, 112), width=2 * S)   # opaque: gold 80 % over parchment
    elif kind == 'dark':         # dark wood/leather panel
        rr(0, 18, fill=GOLD); rr(5, 14, fill=(70, 44, 26)); rr(8, 12, fill=(46, 30, 22)); rr(14, 8, outline=(128, 98, 55), width=2 * S)   # opaque: gold 47 % over the dark fill
    elif kind == 'button':       # gold button
        rr(0, 26, fill=GOLD_DARK)
        top = vgradient(128 * S, 128 * S, (255, 222, 140), (214, 150, 50)); m = big(128, 128); ImageDraw.Draw(m).rounded_rectangle([4 * S, 4 * S, w - 4 * S, h - 10 * S], radius=22 * S, fill=(255, 255, 255, 255))
        img = Image.composite(top, img, m.split()[3]); d = ImageDraw.Draw(img)
        hl = big(128, 128)   # a translucent highlight on its own layer, so the button stays opaque
        ImageDraw.Draw(hl).rounded_rectangle([12 * S, 8 * S, w - 12 * S, 40 * S], radius=14 * S, fill=(255, 255, 255, 60))
        img.alpha_composite(hl)
    elif kind == 'button_dark':
        rr(0, 26, fill=GOLD); rr(4, 22, fill=(92, 56, 30)); rr(8, 18, fill=(120, 74, 40))
    elif kind == 'pill':         # resource counters
        rr(0, 30, fill=GOLD_DARK); rr(4, 26, fill=(30, 20, 18, 235))
    elif kind == 'slot':         # white, tinted by rarity
        rr(0, 16, fill=(255, 255, 255)); rr(6, 12, fill=(60, 60, 60)); rr(9, 10, fill=(40, 34, 30))
    elif kind == 'card':         # hero card edge, white, tinted by rarity
        rr(0, 14, outline=(255, 255, 255), width=6 * S)
    elif kind == 'bar':          # gauge background and fill (white, tinted)
        rr(0, 40, fill=(255, 255, 255))
    elif kind == 'header':       # top bar of sub pages
        d.rectangle([0, 0, w, h], fill=(30, 20, 16, 240)); d.rectangle([0, h - 6 * S, w, h], fill=GOLD)
    return small(img, 128, 128)


# ---------- effects ----------
def circle_sprite(size, color):
    img = big(size, size)
    ImageDraw.Draw(img).ellipse([0, 0, size * S - 1, size * S - 1], fill=color)
    return small(img, size, size)


def rays():
    img = big(512, 512); d = ImageDraw.Draw(img); s = 512 * S; c = s / 2
    for i in range(16):
        a0, a1 = math.radians(i * 22.5 - 4), math.radians(i * 22.5 + 4)
        d.polygon([(c, c), (c + math.cos(a0) * s, c + math.sin(a0) * s), (c + math.cos(a1) * s, c + math.sin(a1) * s)], fill=(255, 255, 255, 140))
    mask = Image.radial_gradient('L').resize(img.size)
    img.putalpha(ImageChops.multiply(img.split()[3], ImageChops.invert(mask)))
    return small(img.filter(ImageFilter.GaussianBlur(S * 4)), 512, 512)


def glow():
    img = big(256, 256)
    ImageDraw.Draw(img).ellipse([64 * S, 64 * S, 192 * S, 192 * S], fill=(255, 255, 255, 255))
    return small(img.filter(ImageFilter.GaussianBlur(S * 30)), 256, 256)


def summon_circle():   # rings, runes and a star, white (tinted)
    img = big(512, 512); d = ImageDraw.Draw(img); s = 512 * S; c = s / 2; white = (255, 255, 255, 255)
    for r, wdt in [(0.48, 6), (0.44, 3), (0.30, 4)]: d.ellipse([c - s * r, c - s * r, c + s * r, c + s * r], outline=white, width=wdt * S)
    pts = [(c + math.cos(math.radians(-90 + i * 144)) * s * 0.44, c + math.sin(math.radians(-90 + i * 144)) * s * 0.44) for i in range(5)]
    d.line(pts + [pts[0]], fill=white, width=4 * S)
    for i in range(24):
        a = math.radians(i * 15); x, y = c + math.cos(a) * s * 0.46, c + math.sin(a) * s * 0.46
        d.rectangle([x - 5 * S, y - 5 * S, x + 5 * S, y + 5 * S], fill=white)
    return small(img, 512, 512)


def card_back():
    img = vgradient(320, 440, (70, 40, 110), (30, 16, 50)).resize((320 * S, 440 * S))
    d = ImageDraw.Draw(img); w, h = 320 * S, 440 * S
    d.rounded_rectangle([0, 0, w - 1, h - 1], radius=24 * S, outline=GOLD, width=10 * S)
    d.rounded_rectangle([22 * S, 22 * S, w - 22 * S, h - 22 * S], radius=14 * S, outline=(147, 111, 87), width=3 * S)   # opaque: gold 55 % over the purple
    c = (w / 2, h / 2)
    for r in (110, 80): d.ellipse([c[0] - r * S, c[1] - r * S, c[0] + r * S, c[1] + r * S], outline=GOLD, width=4 * S)
    pts = [(c[0] + math.cos(math.radians(-90 + i * 144)) * 80 * S, c[1] + math.sin(math.radians(-90 + i * 144)) * 80 * S) for i in range(5)]
    d.line(pts + [pts[0]], fill=GOLD, width=4 * S)
    mask = big(320, 440); ImageDraw.Draw(mask).rounded_rectangle([0, 0, w - 1, h - 1], radius=24 * S, fill=(255, 255, 255, 255))
    img.putalpha(mask.split()[3])
    return small(img, 320, 440)


# ---------- game-icons: colored with a gradient, dark outline, soft shadow ----------
def load_icon(path):
    return Image.open(os.path.join(ICONS, path)).convert('RGBA')


def colored(path, top, bottom, size=256, outline=INK):
    src = load_icon(path).resize((size - 24, size - 24), Image.LANCZOS)
    alpha = Image.new('L', (size, size)); alpha.paste(src.split()[3], (12, 12))
    fill = vgradient(size, size, top, bottom); fill.putalpha(alpha)
    edge = alpha.filter(ImageFilter.MaxFilter(9))
    out = Image.new('RGBA', (size, size), outline + (0,)); out.putalpha(edge)
    shadow = Image.new('RGBA', (size, size), (0, 0, 0, 0)); shadow.putalpha(edge.filter(ImageFilter.GaussianBlur(6)).point(lambda v: v * 0.5))
    img = Image.new('RGBA', (size, size)); img.alpha_composite(shadow, (0, 6)); img.alpha_composite(out); img.alpha_composite(fill)
    # highlight on the upper half
    hl = Image.new('RGBA', (size, size), (255, 255, 255, 0)); hm = Image.linear_gradient('L').resize((size, size)).point(lambda v: max(0, 70 - v))
    hl.putalpha(ImageChops.multiply(hm, alpha)); img.alpha_composite(hl)
    return img


SILVER, BROWN, GOLDC, RED, BLUE, GREEN, PURPLE, CYAN = ((235, 240, 248), (150, 160, 175)), ((196, 140, 90), (120, 76, 40)), ((255, 226, 120), (210, 140, 30)), ((255, 120, 110), (190, 30, 40)), ((140, 190, 255), (40, 90, 200)), ((160, 240, 140), (40, 150, 70)), ((220, 160, 255), (120, 60, 200)), ((180, 250, 255), (40, 170, 220))
ICON_SET = {
    # equipment
    'sword': ('lorc/broadsword.png', SILVER), 'axe': ('lorc/battle-axe.png', SILVER), 'bow': ('delapouite/bow-arrow.png', BROWN),
    'staff': ('lorc/wizard-staff.png', PURPLE), 'shield': ('lorc/checked-shield.png', BLUE), 'helm': ('lorc/visored-helm.png', SILVER),
    'armor': ('lorc/breastplate.png', SILVER), 'boots': ('lorc/leather-boot.png', BROWN), 'ring': ('delapouite/ring.png', GOLDC),
    'necklace': ('delapouite/emerald-necklace.png', GREEN), 'dagger': ('lorc/plain-dagger.png', SILVER),
    # consumables
    'potion_red': ('delapouite/health-potion.png', RED), 'potion_blue': ('delapouite/magic-potion.png', BLUE), 'potion_green': ('lorc/standing-potion.png', GREEN),
    'scroll': ('lorc/scroll-unfurled.png', ((250, 236, 200), (200, 170, 120))),
    # materials
    'crystal': ('lorc/crystal-growth.png', CYAN), 'gold_bar': ('willdabeast/gold-bar.png', GOLDC), 'gems': ('lorc/gems.png', PURPLE),
    'rune': ('lorc/rune-stone.png', ((190, 200, 210), (100, 110, 130))), 'book': ('delapouite/spell-book.png', PURPLE), 'seal': ('lorc/wax-seal.png', RED),
    # currencies and UI
    'gold': ('delapouite/two-coins.png', GOLDC), 'gem': ('lorc/cut-diamond.png', CYAN), 'stamina': ('lorc/lightning-helix.png', ((255, 250, 160), (240, 180, 30))),
    'chest': ('skoll/open-treasure-chest.png', GOLDC), 'star': ('delapouite/round-star.png', GOLDC), 'crown': ('lorc/crown.png', GOLDC),
    'trophy': ('delapouite/laurels-trophy.png', GOLDC), 'skull': ('lorc/skull-crossed-bones.png', SILVER), 'hourglass': ('lorc/hourglass.png', GOLDC),
    'menu_heroes': ('lorc/visored-helm.png', GOLDC), 'menu_bag': ('lorc/knapsack.png', GOLDC), 'menu_summon': ('skoll/pentacle.png', GOLDC),
    'menu_adventure': ('lorc/treasure-map.png', GOLDC), 'menu_quests': ('delapouite/scroll-quill.png', GOLDC), 'upgrade': ('delapouite/upgrade.png', GOLDC),
    # stats and skills
    'atk': ('lorc/crossed-swords.png', RED), 'def': ('lorc/checked-shield.png', BLUE), 'hp': ('zeromancer/heart-plus.png', RED), 'spd': ('lorc/lightning-helix.png', GOLDC),
    'skill_fire': ('lorc/fire-ring.png', ((255, 200, 90), (220, 60, 20))), 'skill_ice': ('lorc/frozen-orb.png', CYAN), 'skill_arcane': ('lorc/magic-swirl.png', PURPLE), 'skill_arrow': ('lorc/energy-arrow.png', GREEN),
    # chapters
    'ch_forest': ('delapouite/forest.png', GREEN), 'ch_mountain': ('lorc/mountains.png', SILVER), 'ch_volcano': ('lorc/volcano.png', RED),
    'ch_castle': ('delapouite/castle.png', PURPLE), 'ch_dragon': ('faithtoken/dragon-head.png', ((255, 150, 80), (160, 30, 30))),
}
HEROES = {   # face icon, card colors (top, bottom), face colors
    'aria': ('delapouite/woman-elf-face.png', ((70, 130, 90), (20, 40, 30)), ((250, 230, 200), (200, 160, 120))),
    'merlin': ('delapouite/wizard-face.png', ((90, 70, 160), (24, 16, 50)), ((235, 225, 255), (160, 140, 210))),
    'borg': ('delapouite/barbarian.png', ((160, 70, 40), (50, 20, 12)), ((255, 220, 180), (200, 140, 90))),
    'durin': ('delapouite/dwarf-face.png', ((150, 110, 50), (50, 34, 14)), ((255, 230, 190), (210, 160, 100))),
    'leon': ('delapouite/spartan-helmet.png', ((60, 90, 160), (16, 24, 50)), ((235, 240, 250), (150, 160, 180))),
    'vex': ('lorc/hood.png', ((60, 60, 70), (16, 16, 22)), ((200, 200, 220), (110, 110, 130))),
}


def hero_portrait(face, card, skin):
    w, h = 400, 520
    img = vgradient(w, h, *card)
    g = Image.new('RGBA', (w, h)); ImageDraw.Draw(g).ellipse([60, 80, 340, 360], fill=(255, 255, 255, 60))
    img = Image.alpha_composite(img, g.filter(ImageFilter.GaussianBlur(50)))
    f = colored(face, *skin, size=360, outline=(20, 12, 10))
    img.alpha_composite(f, (20, 120))
    return img


if __name__ == '__main__':
    if ICONS is None: sys.exit('usage: python Tools/rpg_art.py <game-icons .../1x1 folder>')
    for name, fn in [('lobby', bg_lobby), ('hall', bg_hall), ('summon', bg_summon)]:
        bg = fn()
        assert bg.getextrema()[3][0] == 255, name + ' must be opaque'
        save(bg, 'Backgrounds', name + '.png')
    for kind in ('panel', 'dark', 'button', 'button_dark', 'pill', 'slot', 'card', 'bar', 'header'): save(frame(kind), 'Frames', kind + '.png')
    save(circle_sprite(64, (255, 255, 255, 255)), 'Effects', 'dot.png')
    save(rays(), 'Effects', 'rays.png'); save(glow(), 'Effects', 'glow.png'); save(summon_circle(), 'Effects', 'summon_circle.png'); save(card_back(), 'Effects', 'card_back.png')
    credits = {}
    for name, (path, colors) in ICON_SET.items():
        save(colored(path, *colors), 'Icons', name + '.png')
        credits.setdefault(path.split('/')[0], []).append(name)
    for name, (face, card, skin) in HEROES.items():
        save(hero_portrait(face, card, skin), 'Heroes', name + '.png')
        credits.setdefault(face.split('/')[0], []).append('hero ' + name)
    authors = {'lorc': 'Lorc, http://lorcblog.blogspot.com', 'delapouite': 'Delapouite, https://delapouite.com', 'willdabeast': 'Willdabeast, http://wjbstories.blogspot.com',
               'skoll': 'Skoll', 'zeromancer': 'Zeromancer (CC0)', 'faithtoken': 'Faithtoken, http://fungustoken.deviantart.com'}
    lines = ['# Icon credits', '', 'Icons and hero faces in `Art/Icons` and `Art/Heroes` are from [game-icons.net](https://game-icons.net),',
             'licensed under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/) (Zeromancer: CC0).',
             'They were recolored, outlined and shaded by `Tools/rpg_art.py`; hero portraits put the face icon on a drawn card.', '']
    for author, names in sorted(credits.items()):
        lines.append(f'- **{authors.get(author, author)}**: ' + ', '.join(sorted(names)))
    p = os.path.join(OUT, 'CREDITS.md'); open(p, 'w', encoding='utf-8').write(chr(10).join(lines) + chr(10))
    print('credits', {k: len(v) for k, v in credits.items()})
