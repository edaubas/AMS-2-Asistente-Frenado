#!/usr/bin/env python3
"""
Genera el icono de AMS2 Asistente de Frenado a partir de cero (diseno ORIGINAL, no copia ningun logo de terceros).

Concepto: cuadrado rojo de esquinas redondeadas (#C62F26, el rojo de la franja FRENA del spec), un "2" grande blanco y una
cinta oscura (#0C0E11) abajo con las letras "AF" (Asistente de Frenado). Las letras salen de Barlow Condensed Bold (SIL OFL,
ya embebida en la app) convertidas a trazos, asi el SVG no depende de ninguna fuente instalada.

Uso (desde cualquier carpeta):  python3 tools/icono/generar_icono.py
Necesita: fonttools (pip install fonttools), Pillow y Node con 'playwright' (Chromium) para rasterizar el SVG.
Salida:
  src/AMS2Frenado.App/Recursos/icono.svg        fuente vectorial completa
  src/AMS2Frenado.App/Recursos/icono.ico        16, 24, 32, 48, 64, 128 y 256 px (PNG dentro del ICO)
  docs/ui/icono-vista-previa.png                hoja con todos los tamanos (reales y ampliados)
Tamanos: 16, 24 y 32 usan la variante "simple" (solo el 2, bien grande; a 32 px las letras medirian ~5 px y no se leen, asi que AF
se omite); 48 y 64 usan la variante "medio" (cinta mas alta con AF mas grande, para que se lea); 128 y 256, el icono completo.
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


def svg(variante):
    """variante: 'completo' (2 + cinta + AF), 'medio' (cinta y AF mas grandes, para 48-64 px) o 'simple' (solo el 2)."""
    cuerpo = []
    # el borde oscuro va hacia ADENTRO del cuadrado (2 px de margen = mitad del trazo) para no dejar halo afuera
    cuerpo.append(f'<rect x="8" y="8" width="240" height="240" rx="48" fill="{ROJO}"/>')
    cuerpo.append(f'<rect x="10" y="10" width="236" height="236" rx="46" fill="none" stroke="{ROJO_OSCURO}" stroke-width="4" opacity="0.4"/>')
    if variante == "simple":
        d = trazo_texto("2", 196, 128, 218)
        cuerpo.append(f'<path d="{d}" fill="{BLANCO}"/>')
    elif variante == "medio":
        d = trazo_texto("2", 124, 128, 148)
        cuerpo.append(f'<path d="{d}" fill="{BLANCO}"/>')
        cuerpo.append(f'<rect x="34" y="162" width="188" height="70" rx="35" fill="{OSCURO}" stroke="{BLANCO}" stroke-width="5"/>')
        a = trazo_texto("AF", 44, 128, 219, separacion=6)
        cuerpo.append(f'<path d="{a}" fill="{BLANCO}"/>')
    else:
        d = trazo_texto("2", 142, 128, 164)
        cuerpo.append(f'<path d="{d}" fill="{BLANCO}"/>')
        # cinta / pastilla oscura con borde blanco fino (separa el rojo del fondo, tambien sobre fondos oscuros)
        cuerpo.append(f'<rect x="44" y="176" width="168" height="52" rx="26" fill="{OSCURO}" stroke="{BLANCO}" stroke-width="4"/>')
        a = trazo_texto("AF", 32, 128, 218, separacion=5)
        cuerpo.append(f'<path d="{a}" fill="{BLANCO}"/>')
    return ('<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 256 256" width="256" height="256">'
            '<title>AMS2 Asistente de Frenado</title>' + "".join(cuerpo) + "</svg>")


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

    # hoja de vista previa (1200 x 700): icono grande, tamanos reales sobre oscuro y sobre claro, y 16/24/32 ampliados sin suavizar
    from PIL import ImageDraw
    hoja = Image.new("RGB", (1200, 700), (24, 27, 31))
    d = ImageDraw.Draw(hoja)
    grande = Image.open(trabajo / "grande.png").convert("RGBA").resize((400, 400), Image.LANCZOS)
    hoja.paste(grande, (24, 24), grande)
    d.text((24, 440), "Icono (SVG): 2 + cinta AF. Barlow Condensed Bold convertida a trazos.", fill=(200, 205, 210))

    def fila(y0, fondo, texto_color, titulo):
        d.rectangle((460, y0, 1176, y0 + 150), fill=fondo)
        d.text((472, y0 + 8), titulo, fill=texto_color)
        x = 472
        for px in TAMANOS[:-1]:       # el de 256 no entra en la fila: esta en el .ico y se ve arriba a la izquierda
            im = Image.open(trabajo / f"{px}.png").convert("RGBA")
            hoja.paste(im, (x, y0 + 140 - px - 10), im)
            d.text((x, y0 + 134), f"{px}", fill=texto_color)
            x += px + 22
    fila(24, (24, 27, 31), (200, 205, 210), "Tamanos reales (fondo oscuro)")
    fila(190, (236, 238, 240), (40, 44, 48), "Tamanos reales (fondo claro)")
    d.rectangle((460, 356, 1176, 676), fill=(24, 27, 31))
    d.text((472, 364), "Ampliados 8x sin suavizar: 16, 24 y 32 px (solo el 2, AF se omite)", fill=(200, 205, 210))
    x = 472
    for px in (16, 24, 32, 48):
        im = Image.open(trabajo / f"{px}.png").convert("RGBA")
        ampl = im.resize((px * 8 if px < 48 else px * 6, px * 8 if px < 48 else px * 6), Image.NEAREST)
        if x + ampl.width > 1176: break
        hoja.paste(ampl, (x, 392), ampl)
        x += ampl.width + 14
    PREVIA.parent.mkdir(parents=True, exist_ok=True)
    hoja.save(PREVIA)
    print("listo:", SALIDA / "icono.ico", PREVIA)


if __name__ == "__main__":
    main()
