# Ícono de la app

**Diseño original**, inspirado en la composición del ícono de AMS2 (ficha roja con pestaña oscura, número grande y etiqueta chica), pero con formas propias dibujadas desde cero: no reproduce el artwork ni el lettering "AMS" de Reiza. Los textos ("FRENADO", "2", "AF") salen de Barlow Condensed Bold (SIL OFL, embebida en la app) convertida a trazos.

- Ficha roja con degradé, pestaña oscura con la esquina izquierda en diagonal, "2" blanco con contorno/sombra oscuros, etiqueta "AF" abajo a la derecha.
- Legibilidad: 16/24/32 px muestran solo silueta + "2"; "AF" se lee desde 64 px; "FRENADO" desde 128 px.

## Regenerar

`python3 tools/icono/generar_icono.py` (fonttools, Pillow, Node + playwright). Escribe `icono.svg`, `icono.ico` (16, 24, 32, 48, 64, 128, 256; PNG dentro) y `docs/ui/icono-vista-previa.png`. Verificar el .exe publicado: `python3 tools/icono/verificar_exe.py AMS2Frenado.exe` (pefile).

## Usar un ícono propio (por ejemplo el original de AMS2, solo en privado)

Copiá tu `.ico` a `src/AMS2Frenado.App/Recursos/icono.ico` y recompilá. **No lo commitees** (para que git no lo vea: `git update-index --assume-unchanged src/AMS2Frenado.App/Recursos/icono.ico`). Conviene que traiga 16, 24, 32, 48, 64, 128 y 256 px. El ícono de la bandeja toma el de 32 px del .ico y le dibuja encima el indicador del candado.
