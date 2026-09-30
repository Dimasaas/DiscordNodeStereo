"""Gera o ícone do DiscordNodeStereo e as imagens do README (assets/ e docs/).

Só precisa rodar quando mudar a arte:  python tools/make_assets.py   (requer Pillow)
"""
import math
import os

from PIL import Image, ImageChops, ImageDraw, ImageFilter, ImageFont

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ASSETS = os.path.join(ROOT, "assets")
DOCS = os.path.join(ROOT, "docs")
FONTS = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts")

VIOLET = (124, 58, 237)
CYAN = (6, 182, 212)
BAR = (88, 60, 220)


def gradient(size, c1, c2):
    """Degradê diagonal de c1 (canto superior esquerdo) para c2 (inferior direito)."""
    w, h = size
    small = Image.new("RGB", (256, 256))
    px = small.load()
    for y in range(256):
        for x in range(256):
            t = (x + y) / 510
            px[x, y] = tuple(round(a + (b - a) * t) for a, b in zip(c1, c2))
    return small.resize((w, h), Image.BICUBIC)


def arc_round(d, cx, cy, r, a0, a1, width, fill):
    """Arco com pontas arredondadas (ângulos no sentido horário a partir das 3h, como no Pillow)."""
    d.arc([cx - r, cy - r, cx + r, cy + r], a0, a1, fill=fill, width=int(width))
    mid = r - width / 2   # o Pillow desenha a espessura para dentro
    for a in (a0, a1):
        t = math.radians(a)
        x, y = cx + mid * math.cos(t), cy + mid * math.sin(t)
        d.ellipse([x - width / 2, y - width / 2, x + width / 2, y + width / 2], fill=fill)


def mic_mask(s):
    """Microfone de mesa com ondas de som dos dois lados (estéreo), em branco numa máscara s x s."""
    m = Image.new("L", (s, s), 0)
    d = ImageDraw.Draw(m)
    cx = s * 0.5
    w = s * 0.05
    # cápsula
    d.rounded_rectangle([s * 0.395, s * 0.15, s * 0.605, s * 0.54], radius=s * 0.105, fill=255)
    # suporte em U, haste e base
    holder_cy, holder_r = s * 0.44, s * 0.19
    arc_round(d, cx, holder_cy, holder_r, 0, 180, w, 255)
    d.rectangle([cx - w / 2, holder_cy + holder_r - w, cx + w / 2, s * 0.75], fill=255)
    d.rounded_rectangle([s * 0.38, s * 0.735, s * 0.62, s * 0.785], radius=w / 2, fill=255)
    # ondas: esquerda e direita (L/R)
    wave_cy = s * 0.345
    for r in (s * 0.295, s * 0.38):
        arc_round(d, cx, wave_cy, r, 150, 210, s * 0.045, 255)
        arc_round(d, cx, wave_cy, r, -30, 30, s * 0.045, 255)
    return m


def icon_image(s=1024):
    img = Image.new("RGBA", (s, s), (0, 0, 0, 0))
    mask = Image.new("L", (s, s), 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, s - 1, s - 1], radius=int(s * 0.23), fill=255)
    bg = gradient((s, s), VIOLET, CYAN).convert("RGBA")
    img.paste(bg, (0, 0), mask)

    # brilho suave no topo
    gloss = Image.new("L", (s, s), 0)
    ImageDraw.Draw(gloss).ellipse([-s * 0.3, -s * 0.75, s * 1.3, s * 0.42], fill=38)
    gloss = ImageChops.multiply(gloss, mask)
    img = Image.composite(Image.new("RGBA", (s, s), (255, 255, 255, 255)), img, gloss)

    # sombra + microfone branco
    mic = mic_mask(s)
    shadow = ImageChops.offset(mic, 0, int(s * 0.022)).point(lambda v: v * 90 // 255)
    shadow = shadow.filter(ImageFilter.GaussianBlur(s * 0.02))
    img = Image.composite(Image.new("RGBA", (s, s), (40, 20, 90, 255)), img, ImageChops.multiply(shadow, mask))
    img = Image.composite(Image.new("RGBA", (s, s), (255, 255, 255, 255)), img, mic)

    # grade da cápsula
    d = ImageDraw.Draw(img)
    lw = s * 0.024
    for y in (0.26, 0.32, 0.38):
        d.rounded_rectangle([s * 0.45, s * y - lw / 2, s * 0.55, s * y + lw / 2], radius=lw / 2, fill=BAR)
    return img


def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def banner(logo):
    w, h = 1280, 400
    img = gradient((w, h), (17, 12, 40), (12, 38, 58)).convert("RGBA")
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse([20, -60, 420, 460], fill=VIOLET + (110,))
    gd.ellipse([900, 160, 1400, 600], fill=CYAN + (60,))
    img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(110)))

    img.alpha_composite(logo.resize((220, 220), Image.LANCZOS), (110, 90))
    d = ImageDraw.Draw(img)

    # título em duas cores: "DiscordNode" branco + "Stereo" em degradê, ajustado à largura
    size = 104
    while True:
        f_title = font("segoeuib.ttf", size)
        first = d.textlength("DiscordNode", font=f_title)
        if first + d.textlength("Stereo", font=f_title) <= w - 372 - 60:
            break
        size -= 2
    top = 150 - size // 2 - 10
    d.text((372, top), "DiscordNode", font=f_title, fill=(255, 255, 255))
    text_mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(text_mask).text((372 + first, top), "Stereo", font=f_title, fill=255)
    # degradê só na área da palavra, para ir de lilás a ciano dentro do "Stereo"
    x0, y0, x1, y1 = text_mask.getbbox()
    word = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    word.paste(gradient((x1 - x0, y1 - y0), (196, 140, 255), (34, 211, 238)).convert("RGBA"), (x0, y0))
    img = Image.composite(word, img, text_mask)
    d = ImageDraw.Draw(img)
    d.text((378, 222), "Mic estéreo a 512 kbps no Discord, sempre.", font=font("segoeui.ttf", 38), fill=(200, 205, 230))

    # chips translúcidos: desenha numa camada à parte e compõe
    chips = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cd = ImageDraw.Draw(chips)
    f = font("seguisb.ttf", 24)
    x = 378
    for chip in ("512 kbps", "Estéreo", "Portátil", "Automático"):
        tw = cd.textlength(chip, font=f)
        cd.rounded_rectangle([x, 290, x + tw + 36, 336], radius=23, fill=(255, 255, 255, 28),
                             outline=(255, 255, 255, 80), width=2)
        cd.text((x + 18, 295), chip, font=f, fill=(235, 238, 255, 255))
        x += tw + 52
    return Image.alpha_composite(img, chips)


def with_shadow(shot, radius=10):
    """Arredonda os cantos do print e devolve (imagem, sombra) prontas para colar."""
    mask = Image.new("L", shot.size, 0)
    ImageDraw.Draw(mask).rounded_rectangle([0, 0, shot.width - 1, shot.height - 1], radius=radius, fill=255)
    rounded = Image.new("RGBA", shot.size, (0, 0, 0, 0))
    rounded.paste(shot.convert("RGBA"), (0, 0), mask)
    pad = 60
    shadow = Image.new("RGBA", (shot.width + pad * 2, shot.height + pad * 2), (0, 0, 0, 0))
    shadow.paste(Image.new("RGBA", shot.size, (0, 0, 0, 150)), (pad, pad + 14), mask)
    return rounded, shadow.filter(ImageFilter.GaussianBlur(24)), pad


def mockup():
    """Monta docs/mockup.png com os prints reais (docs/screenshot-*.png) sobre o fundo do banner."""
    main_path = os.path.join(DOCS, "screenshot-principal.png")
    dialog_path = os.path.join(DOCS, "screenshot-aviso.png")
    if not (os.path.exists(main_path) and os.path.exists(dialog_path)):
        return
    w, h = 1400, 900
    img = gradient((w, h), (17, 12, 40), (12, 38, 58)).convert("RGBA")
    glow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse([180, 40, 760, 700], fill=VIOLET + (120,))
    gd.ellipse([760, 380, 1360, 980], fill=CYAN + (80,))
    img = Image.alpha_composite(img, glow.filter(ImageFilter.GaussianBlur(140)))

    main = Image.open(main_path)
    main = main.resize((int(main.width * 1.18), int(main.height * 1.18)), Image.LANCZOS)
    dialog = Image.open(dialog_path)
    dialog = dialog.resize((int(dialog.width * 1.18), int(dialog.height * 1.18)), Image.LANCZOS)

    for shot, (x, y) in ((main, (250, 40)), (dialog, (720, 560))):
        rounded, shadow, pad = with_shadow(shot)
        img.alpha_composite(shadow, (x - pad, y - pad))
        img.alpha_composite(rounded, (x, y))
    img.convert("RGB").save(os.path.join(DOCS, "mockup.png"), optimize=True)


def main():
    os.makedirs(ASSETS, exist_ok=True)
    os.makedirs(DOCS, exist_ok=True)
    big = icon_image()
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    big.resize((256, 256), Image.LANCZOS).save(
        os.path.join(ASSETS, "icon.ico"), format="ICO", sizes=[(s, s) for s in sizes], bitmap_format="bmp")
    big.resize((256, 256), Image.LANCZOS).save(os.path.join(DOCS, "logo.png"))
    banner(big).convert("RGB").save(os.path.join(DOCS, "banner.png"), optimize=True)
    mockup()
    print("ok:", ASSETS, DOCS)


if __name__ == "__main__":
    main()
