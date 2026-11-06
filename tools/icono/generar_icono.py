#!/usr/bin/env python3
"""
Genera el icono de AMS2 Asistente de Frenado a partir de cero (diseno ORIGINAL, no copia ningun logo de terceros).

Concepto: ficha/carpeta roja con degrade, pestana oscura con la esquina izquierda en diagonal, un "2" blanco con contorno oscuro y una
etiqueta "AF" (negro sobre pastilla roja clara) abajo a la derecha. Formas propias; homenaje a la composicion, no copia del artwork. Las letras salen de Barlow Condensed Bold (SIL OFL,
ya embebida en la app) convertidas a trazos, asi el SVG no depende de ninguna fuente instalada.

Uso (desde cualquier carpeta):  python3 tools/icono/generar_icono.py
Necesita: fonttools (pip install fonttools), Pillow y Node con 'playwright' (Chromium) para rasterizar el SVG.
Salida:
  src/AMS2Frenado.App/Recursos/icono.svg        fuente vectorial completa
  src/AMS2Frenado.App/Recursos/icono.ico        16, 24, 32, 48, 64, 128 y 256 px (PNG dentro del ICO)
  docs/ui/icono-vista-previa.png                hoja con todos los tamanos (reales y ampliados)
Tamanos: 16, 24 y 32 usan la variante "simple" (silueta ficha + 2 grande; sin textos); 48 y 64 la "medio" (pestana lisa con detalle
metalico, 2 y etiqueta AF grande); 128 y 256 el icono completo (con FRENADO en la pestana).
"""
import json, struct, subprocess, sys, io
from pathlib import Path
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen
from fontTools.pens.transformPen import TransformPen
from fontTools.pens.boundsPen import BoundsPen
from PIL import Image

RAIZ = Path(__file__).resolve().parents[2]
FUENTE = RAIZ / "src/AMS2Frenado.App/Fuentes/BarlowCondensed-Bold.ttf"
SALIDA = RAIZ / "src/AMS2Frenado.App/Recursos"
PREVIA = RAIZ / "docs/ui/icono-vista-previa.png"
ROJO, OSCURO, BLANCO, ROJO_OSCURO = "#C62F26", "#0C0E11", "#FFFFFF", "#8F1F19"
TAMANOS = [16, 24, 32, 48, 64, 128, 256]

fuente = TTFont(FUENTE)
glifos = fuente.getGlyphSet()
cmap = fuente.getBestCmap()


def trazo_texto(texto, alto, centro_x, base_y, separacion=0.0):
    """Path SVG de 'texto' con altura de mayuscula 'alto' (unidades del viewBox), centrado en centro_x y apoyado en base_y."""
    nombres = [cmap[ord(c)] for c in texto]
    # altura de mayuscula = alto del glifo 'H'
    bp = BoundsPen(glifos); glifos[cmap[ord("H")]].draw(bp)
    escala = alto / (bp.bounds[3] - bp.bounds[1])
    ancho_total, x = 0.0, 0.0
    piezas = []
    for n in nombres:
        g = glifos[n]
        bp = BoundsPen(glifos); g.draw(bp)
        piezas.append((g, x, bp.bounds))
        x += g.width + separacion / escala
    ancho_total = (x - separacion / escala) * escala
    x0 = centro_x - ancho_total / 2
    partes = []
    for g, ox, _ in piezas:
        pen = SVGPathPen(glifos)
        # fuente: y hacia arriba, origen en la linea de base -> SVG: y hacia abajo
        g.draw(TransformPen(pen, (escala, 0, 0, -escala, x0 + ox * escala, base_y)))
        partes.append(pen.getCommands())
    return " ".join(partes)


def pastilla_af(x, y, w, h, alto_letra, cy_base):
    r = h / 2
    return (f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="url(#pas)" stroke="#7A1410" stroke-width="2.5"/>'
            f'<path d="{trazo_texto("AF", alto_letra, x + w / 2, cy_base, separacion=3)}" fill="#050505"/>')


def svg(variante):
    """variante: 'completo' (ficha + FRENADO + 2 + AF, 128/256 px), 'medio' (pestana lisa con detalle metalico, 2 y AF grande, 48/64 px)
    o 'simple' (solo silueta ficha y 2, 16-32 px)."""
    c = []
    c.append('<defs>'
             '<linearGradient id="cue" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FF5A4D"/><stop offset="0.55" stop-color="#D3261D"/><stop offset="1" stop-color="#A31510"/></linearGradient>'
             '<linearGradient id="pes" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#3A3E45"/><stop offset="1" stop-color="#15171A"/></linearGradient>'
             '<linearGradient id="met" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#E4E7EA"/><stop offset="1" stop-color="#7C838B"/></linearGradient>'
             '<linearGradient id="pas" x1="0" y1="0" x2="0" y2="1"><stop offset="0" stop-color="#FF8A7E"/><stop offset="1" stop-color="#F0483C"/></linearGradient>'
             '</defs>')
    # cuerpo rojo (ficha) y pestana oscura con la esquina izquierda cortada en diagonal
    c.append('<rect x="14" y="54" width="228" height="196" rx="18" fill="url(#cue)" stroke="#5A0F0C" stroke-width="5"/>')
    c.append('<path d="M16 58V50L50 12H228Q242 12 242 26V58Z" fill="url(#pes)" stroke="#0A0B0D" stroke-width="5" stroke-linejoin="round"/>')
    c.append('<path d="M20 49L52 17H226" fill="none" stroke="#6B7077" stroke-width="2" stroke-linecap="round"/>')
    c.append('<rect x="20" y="52" width="216" height="5" fill="#FF8A7E" opacity="0.55"/>')   # brillo del borde superior del cuerpo
    if variante == "completo":
        c.append(f'<path d="{trazo_texto("FRENADO", 24, 140, 47, separacion=3)}" fill="url(#met)"/>')
        base, alto = 196, 128
    elif variante == "medio":
        c.append('<rect x="70" y="26" width="150" height="14" rx="7" fill="url(#met)"/>')
        base, alto = 190, 124
    else:
        c.append('<rect x="70" y="24" width="150" height="20" rx="10" fill="url(#met)"/>')
        base, alto = 226, 160
    d = trazo_texto("2", alto, 124, base)
    c.append(f'<path d="{d}" transform="translate(5 7)" fill="#000" opacity="0.35" stroke="#000" stroke-width="16" stroke-linejoin="round"/>')
    c.append(f'<path d="{d}" fill="#1A1112" stroke="#1A1112" stroke-width="18" stroke-linejoin="round"/>')
    c.append(f'<path d="{d}" fill="{BLANCO}" stroke="{BLANCO}" stroke-width="5" stroke-linejoin="round"/>')
    if variante == "completo":
        c.append(pastilla_af(138, 198, 98, 44, 26, 229))
    elif variante == "medio":
        c.append(pastilla_af(124, 188, 112, 54, 36, 224))
    return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">'
            '<title>AMS2 Asistente de Frenado</title>' + "".join(c) + "</svg>")


def variante_de(px):
    return "simple" if px <= 32 else "medio" if px <= 64 else "completo"


def main():
    SALIDA.mkdir(parents=True, exist_ok=True)
    (SALIDA / "icono.svg").write_text(svg("completo") + "\n", encoding="utf-8")
    trabajo = Path(__import__("tempfile").mkdtemp())
    pedidos = []
    for px in TAMANOS:
        (trabajo / f"v{px}.svg").write_text(svg(variante_de(px)), encoding="utf-8")
        pedidos.append({"svg": str(trabajo / f"v{px}.svg"), "png": str(trabajo / f"{px}.png"), "px": px})
    # la hoja de vista previa usa el icono completo ampliado
    (trabajo / "completo.svg").write_text(svg("completo"), encoding="utf-8")
    pedidos.append({"svg": str(trabajo / "completo.svg"), "png": str(trabajo / "grande.png"), "px": 512})
    (trabajo / "pedidos.json").write_text(json.dumps(pedidos), encoding="utf-8")
    subprocess.run(["node", str(Path(__file__).with_name("rasterizar.js")), str(trabajo / "pedidos.json")], check=True)

    # ICO con PNG dentro de cada entrada (Vista+ y WPF lo leen sin problema)
    pngs = [(px, (trabajo / f"{px}.png").read_bytes()) for px in TAMANOS]
    cab = struct.pack("<HHH", 0, 1, len(pngs))
    offset = 6 + 16 * len(pngs)
    entradas, datos = b"", b""
    for px, png in pngs:
        entradas += struct.pack("<BBBBHHII", px % 256, px % 256, 0, 0, 1, 32, len(png), offset + len(datos))
        datos += png
    (SALIDA / "icono.ico").write_bytes(cab + entradas + datos)

    # hoja de vista previa (1200 x 760): icono grande, tamanos reales sobre oscuro y claro, ampliados y simulacion de acceso directo
    from PIL import ImageDraw
    hoja = Image.new("RGB", (1200, 760), (24, 27, 31))
    d = ImageDraw.Draw(hoja)
    TXT = (200, 205, 210)
    grande = Image.open(trabajo / "grande.png").convert("RGBA").resize((400, 400), Image.LANCZOS)
    hoja.paste(grande, (24, 24), grande)
    d.text((24, 436), "Icono (SVG): ficha roja con pestana oscura, 2 y etiqueta AF.", fill=TXT)
    d.text((24, 452), "Diseno original inspirado en la composicion del icono del juego.", fill=TXT)
    d.text((24, 468), "Barlow Condensed Bold (OFL) convertida a trazos.", fill=TXT)

    def fila(y0, fondo, color, titulo):
        d.rectangle((460, y0, 1176, y0 + 150), fill=fondo)
        d.text((472, y0 + 8), titulo, fill=color)
        x = 472
        for px in TAMANOS[:-1]:       # el de 256 no entra en la fila: esta en el .ico y se ve arriba a la izquierda
            im = Image.open(trabajo / f"{px}.png").convert("RGBA")
            hoja.paste(im, (x, y0 + 140 - px - 10), im)
            d.text((x, y0 + 134), f"{px}", fill=color)
            x += px + 22
    fila(24, (24, 27, 31), TXT, "Tamanos reales (fondo oscuro)")
    fila(190, (236, 238, 240), (40, 44, 48), "Tamanos reales (fondo claro)")

    # ampliados sin suavizar
    d.rectangle((460, 356, 1176, 540), fill=(24, 27, 31))
    d.text((472, 364), "Ampliados sin suavizar: 16 (x6), 24 (x5), 32 (x4), 48 (x3), 64 (x2)", fill=TXT)
    x = 472
    for px, k in ((16, 6), (24, 5), (32, 4), (48, 3), (64, 2)):
        im = Image.open(trabajo / f"{px}.png").convert("RGBA").resize((px * k, px * k), Image.NEAREST)
        hoja.paste(im, (x, 384 + (160 - px * k) // 2 if px * k < 150 else 384), im)
        x += im.width + 14

    # simulacion de acceso directo de Windows (escritorio claro y oscuro) con la flecha de acceso directo
    def flecha(base_img, ox, oy, px):
        t = max(8, round(px * 0.35)); dd = ImageDraw.Draw(base_img)
        x0, y1 = ox, oy + px
        dd.rectangle((x0, y1 - t, x0 + t, y1), fill=(255, 255, 255), outline=(190, 190, 190))
        u = t / 10
        dd.line((x0 + 2.5 * u, y1 - 2.5 * u, x0 + 7 * u, y1 - 7 * u), fill=(26, 115, 232), width=max(1, round(u * 1.4)))
        dd.polygon([(x0 + 4 * u, y1 - 8 * u), (x0 + 8 * u, y1 - 8 * u), (x0 + 8 * u, y1 - 4 * u)], fill=(26, 115, 232))
    d.text((472, 556), "Acceso directo (con flecha), escritorio claro y oscuro", fill=TXT)
    for fondo, x0 in (((205, 222, 238), 460), ((20, 24, 40), 820)):
        d.rectangle((x0, 576, x0 + 356, 740), fill=fondo)
        x = x0 + 16
        for px in (32, 48, 64):
            im = Image.open(trabajo / f"{px}.png").convert("RGBA")
            hoja.paste(im, (x, 576 + 164 - px - 24), im)
            flecha(hoja, x, 576 + 164 - px - 24, px)
            x += px + 24
        im = Image.open(trabajo / "128.png").convert("RGBA")
        hoja.paste(im, (x0 + 356 - 128 - 8, 576 + 164 - 128 - 8), im)
        flecha(hoja, x0 + 356 - 128 - 8, 576 + 164 - 128 - 8, 128)
    PREVIA.parent.mkdir(parents=True, exist_ok=True)
    hoja.save(PREVIA)
    print("listo:", SALIDA / "icono.ico", PREVIA)


if __name__ == "__main__":
    main()
