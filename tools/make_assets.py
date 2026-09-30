"""Gera o ícone do VoxGuard e as imagens do README (assets/ e docs/).

Só precisa rodar quando mudar a arte:  python tools/make_assets.py   (requer Pillow)
"""
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


def bezier(p0, p1, p2, steps=40):
    return [((1 - t) ** 2 * p0[0] + 2 * (1 - t) * t * p1[0] + t * t * p2[0],
             (1 - t) ** 2 * p0[1] + 2 * (1 - t) * t * p1[1] + t * t * p2[1])
            for t in (i / steps for i in range(steps + 1))]


def shield(s):
    """Contorno de um escudo em coordenadas de pixel para uma tela s x s."""
    def p(x, y):
        return (x * s, y * s)
    left = bezier(p(0.5, 0.86), p(0.25, 0.74), p(0.25, 0.50))
    top = bezier(p(0.25, 0.27), p(0.40, 0.25), p(0.5, 0.17)) + bezier(p(0.5, 0.17), p(0.60, 0.25), p(0.75, 0.27))
    right = bezier(p(0.75, 0.50), p(0.75, 0.74), p(0.5, 0.86))
    return left + top + right


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

    # sombra + escudo branco
    shadow = Image.new("L", (s, s), 0)
    ImageDraw.Draw(shadow).polygon([(x, y + s * 0.025) for x, y in shield(s)], fill=90)
    shadow = shadow.filter(ImageFilter.GaussianBlur(s * 0.02))
    img = Image.composite(Image.new("RGBA", (s, s), (40, 20, 90, 255)), img, ImageChops.multiply(shadow, mask))
    d = ImageDraw.Draw(img)
    d.polygon(shield(s), fill=(255, 255, 255, 255))

    # ondas de voz dentro do escudo
    heights = [0.10, 0.20, 0.28, 0.20, 0.10]
    bw = s * 0.05
    for i, hgt in enumerate(heights):
        cx = s * (0.5 + (i - 2) * 0.078)
        cy = s * 0.50
        d.rounded_rectangle([cx - bw / 2, cy - s * hgt / 2, cx + bw / 2, cy + s * hgt / 2], radius=bw / 2, fill=BAR)
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
    d.text((372, 92), "VoxGuard", font=font("segoeuib.ttf", 104), fill=(255, 255, 255))
    d.text((378, 222), "Atualizador de módulos para Discord", font=font("segoeui.ttf", 38), fill=(200, 205, 230))

    # chips translúcidos: desenha numa camada à parte e compõe
    chips = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    cd = ImageDraw.Draw(chips)
    f = font("seguisb.ttf", 24)
    x = 378
    for chip in ("Portátil", "Leve", "Automático", "Sem instalar nada"):
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
