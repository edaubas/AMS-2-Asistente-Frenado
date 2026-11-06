# Fase 2 – Cómo correr el diagnóstico con AMS2

Esta es la compuerta de la fase 2: hay que ver que el lector funciona contra el juego real. **Todo esto está sin probar en AMS2.**

## Pasos

1. En AMS2: `Options › System › Shared Memory = Project CARS 2` y `UDP Frequency = OFF`. Reiniciá el juego si lo cambiás.
2. Bajá el `.exe` del último build: pestaña **Actions** del repo › el último run verde de la rama › artifact `AMS2Frenado-win-x64` (es un zip con `AMS2Frenado.exe`).
3. Abrí `AMS2Frenado.exe` (Windows puede avisar por ser un `.exe` sin firmar: "Más información › Ejecutar de todas formas").
4. Entrá a una sesión con IA (práctica o carrera, mejor con varios autos) y mirá la ventana.
5. Probá el botón **Empezar captura cruda**: manejá una o dos vueltas y apretá **Parar captura**. El archivo `captura-AAAAMMDD-HHMMSS.ams2cap` queda en `%APPDATA%\AMS2AsistenteFrenado\capturas\`. Mandanos ese archivo (pesa unos 1,2 MB por segundo).

## Qué deberías ver

- Con el juego en pista: arriba "Leyendo AMS2 (memoria compartida v14)".
- `Memoria: v14`, estado del juego `2 (jugando)` (o `6 (replay)` en un replay).
- **Pista**, **variante** y **largo** correctos (por ejemplo `Silverstone | Grand Prix | largo ~5891 m`).
- **Auto** y **clase** del auto visto (GT3, etc.).
- Una tabla con los participantes activos: nombre, **clase**, **distancia** de vuelta (sube mientras avanzan y vuelve a ~0 al cruzar la meta), **velocidad en km/h** (no debería ser 0 en los autos que andan), posición y vueltas.
- Freno y gas del auto visto que reaccionan a tus pedales.
- Con el juego cerrado o la opción desactivada: "No detecto AMS2…". Si la versión no es 14: aviso y no se muestra nada más.

## Qué avisar si falla

- Qué texto aparece arriba, y si la tabla está vacía o con valores en 0 (velocidades de la IA, distancias, clase).
- Si la pista, variante o clase salen vacías o raras.
- El archivo `.ams2cap`, si pudiste grabarlo.
