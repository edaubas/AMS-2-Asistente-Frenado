# UI-SPEC – Validación del overlay antes de la fase 5

Agente 2, primera participación (sobre el lienzo). Rama `fase-5-ui-spec`. Solo docs: no se tocó `src/`.

**Cómo leer este documento**

- **[Evidencia]**: calculado con scripts de este repo (`docs/ui/contraste.py`, `docs/ui/contraste_extra.py`) o leído del lienzo y del código de la fase 4. Se puede repetir.
- **[Criterio]**: razonamiento de diseño. Hay que confirmarlo con el `.exe` real.
- **[Supuesto]**: algo que no pude verificar. Está listado en la sección 6.
- Pantallas nuevas: `docs/ui/pantallas.html` (fuente) y `docs/ui/pantallas.png` (vista).

**Material recibido.** `Main.dc.html`, `Estados.dc.html` y `canvas.json`. El tablero `Ventana.dc.html` (ventana desbloqueada + menú de bandeja) figura en `canvas.json`, pero **no estaba en lo que recibí**. Todo lo de bloqueo y bandeja se especifica desde el handover y no se validó contra un dibujo.

**Cambios del usuario que ya incorporé:**

- ~~El número grande es el cartel (400/300/200/100/50).~~ **Reemplazado en la fase 5b**, ver más abajo: el cartel ya no se muestra.
- Se agregan los estados Toque, Levantá y el flag Grabando.
- Sin texto explicativo extra en el panel de frenada.

> ## Fase 5b: lo que cambió después de probar el .exe (vigente)
>
> El usuario probó la fase 5 con AMS2 y decidió lo siguiente. **Donde el resto de este documento diga otra cosa (cartel grande, mapa, "Rojo suave", cuatro atajos de esquina), manda esta lista.** Los cuadros y medidas viejos se dejaron como historia, marcados con ~~tachado~~ o con la nota "(fase 5b)".
>
> | # | Cambio | Dónde queda |
> |---|---|---|
> | 1 | **Sin mini mapa** en el overlay (ni "Sin mapa todavía", ni el contador MAPA de GRABANDO, ni "Mostrar mapa" / "Mapa compacto" en la bandeja). El grabador sigue guardando `mapa.json`. | 2, 3.2, 3.4 |
> | 2 | **Sin número grande de cartel** ("200 m"): se leía como cuenta regresiva. La frenada se apoya en la **barra de progreso** (la regla con rombo = IA y círculo = vos), las etiquetas, el nombre de la curva y las velocidades entrada → vértice. Los metros que faltan quedan como dato chico y decreciente hasta 0. | 2, [CARTELES.md](CARTELES.md) |
> | 3 | **Sin "Rojo suave"**: la franja roja de 40 px es la única. La opacidad del fondo se queda. | 3.3, 3.5 |
> | 4 | **Sin esquinas en la bandeja**: el usuario mueve el overlay a mano; posición y tamaño se siguen guardando. | 3.4 |
> | 5 | **Aviso de frenada** nuevo: símbolo de freno rojo en una ventana aparte. | 2.1 |
> | 6 | Pitido con WAV generado en memoria y `SoundPlayer` (antes `Console.Beep`). "Probar pitido" en el menú. | 3.4 |
> | 7 | Exportar / importar datos (.zip) y abrir la carpeta de capturas. | 3.4, [DATOS.md](DATOS.md) |
> | 8 | Pendientes (no implementados): botón de modo en el HUD, "✕" / menú contextual sobre el overlay desbloqueado y nombre de pista largo. | 4 (P16 a P18) |
>
> ## Fase 5c: ajustes tras la segunda prueba con AMS2 (vigente)
>
> El usuario probó el .exe de la 5b (run #9). **Donde el resto del documento (o la lista de la 5b) diga otra cosa sobre el aviso de frenada, el pitido, SIN DATOS o los modos de grabación, manda esta lista.** Todo lo de ventana y sonido sigue marcado "compila, sin ejecutar".
>
> | # | Cambio | Dónde queda |
> |---|---|---|
> | 1 | **SIN DATOS dice la causa real**: boxes, largo de pista distinto, sin curvas guardadas, archivo ilegible o datos sin curvas válidas. Antes "No hay curvas guardadas" salía también al estar solo en boxes. | 2 (SIN DATOS) |
> | 2 | **El pitido no suena si ya estás frenando** (misma regla que el símbolo). Un único decisor (`DecisorAviso`, Core) alimenta al pitido y al símbolo. | 2.1 |
> | 3 | **El símbolo aparece gradualmente**: la opacidad (y una escala leve) sube de 0 a 1 en los últimos 2 s (1 a 3 s) hasta el punto de frenado y llega al 100 % justo ahí. | 2.1 |
> | 4 | **Pitido y símbolo siguen solo a la referencia principal elegida.** Si la curva no tiene bloque de esa referencia, no hay ni pitido ni símbolo en esa curva. | 2.1 |
> | 5 | **Tercer modo "Grabar solo vos"**: la IA no se vuelve a grabar; chip "GRABANDO · SOLO VOS". | 2 (GRABANDO), 3.4 |

> ## Fase 6: rediseño del overlay y menú integrado (propuesta; ver sección 7)
>
> Estética de carbono con esquina cortada, barra de botones (modo, engranaje, fijar, cerrar), menú de configuración dentro del overlay y "Anticipación del aviso". **Si algo de las secciones 1 a 6 contradice la sección 7 (colores, bloqueo, bandeja, chips de 11 px, cabecera), manda la sección 7 cuando el usuario la apruebe.** Pantallas: `docs/ui/pantallas-fase6.html` y `.png`. Script de contraste: `docs/ui/contraste_fase6.py`.

---

## 1. Checklist del handover

| # | Ítem | Veredicto | Razón |
|---|------|-----------|-------|
| 1 | Se lee en menos de 0,5 s | **Cumple en diseño, no verificable sin el juego** | Ver 1.1. |
| 2 | Contraste de texto ≥ 4,5:1 | **Cumple con condiciones** | Con el fondo al 0,88 todo cumple, incluso sobre blanco. Fallan dos cosas: el slider de opacidad y los textos de 10 px. Ver 1.2. |
| 3 | Naranja y azul se distinguen con daltonismo y por brillo | **Cumple por matiz, no cumple por brillo** | Ver 1.3. Se arregla con redundancia de forma. |
| 4 | Sin parpadeo; el rojo no es invasivo | **No cumple tal como está en el lienzo** | El panel rojo completo es invasivo. Propongo franja roja y reglas anti-parpadeo (3.5). |
| 5 | Posición por defecto no tapa el HUD | **No verificable** | Depende del HUD real de AMS2. El overlay medido ocupa mucho alto (3.2). |
| 6 | Bloquear/desbloquear se entiende y el menú se encuentra | **No verificable** | Falta `Ventana.dc.html`. Con el overlay bloqueado, la bandeja es la única vía. En Windows 11 el ícono suele quedar oculto. Ver 3.4. |
| 7 | SIN DATOS y GRABANDO dicen qué hacer | **No cumple en el lienzo** | SIN DATOS dice "Pasá a Grabar" sin decir dónde. GRABANDO muestra contadores pero no dice cómo parar. Corregido en las pantallas 9 y 10. |

### 1.1 Lectura en menos de 0,5 s [Criterio]

- **A favor:**
  - Con el cartel como número principal, el elemento dominante tiene una sola cifra de 3 dígitos o menos, a 64 px y en blanco.
  - El nombre de la curva (22 px) queda a un movimiento de ojo.
  - Quién es cada color depende de una etiqueta de 12 px, que no se lee en 0,5 s. Por eso se agrega la forma (rombo = IA, círculo = vos) y se mantiene la etiqueta.
- **En contra:**
  - El lienzo usa etiquetas de 11 px y ejes de 10 px. Con el auto en movimiento no se leen. Los ejes se eliminan (ver 4, P7).
  - Con 320 px de ancho sobre un monitor de 1080p, el 64 px es el 6 % del alto de pantalla. Es legible, pero hay que ofrecer escala 75–150 %.
- **Qué verificar:** una prueba con la captura real (sección 5).

### 1.2 Contraste WCAG 2.x [Evidencia]

Texto sobre el panel `rgba(12,14,17,0.88)`. El fondo efectivo cambia con lo que haya detrás:

- Sobre negro: casi `#0C0E11`.
- Sobre blanco: `#292B2E`.
- Sobre cielo claro `#BFD8EE`: `#21262C`.

**Supuesto:** el cielo claro es `#BFD8EE`. El cielo del lienzo es `#5B6E7C`.

| Texto (color) | Negro | Cielo lienzo | Piano `#C9CDD1` | Cielo claro | **Blanco (peor)** |
|---|---|---|---|---|---|
| Cartel `#FFFFFF` | 19,54 | 17,57 | 15,39 | 15,19 | **14,21** |
| Secundario `#DDE2E7` | 14,99 | 13,48 | 11,80 | 11,65 | **10,90** |
| Título `#EEF1F4` | 17,24 | 15,50 | 13,57 | 13,40 | **12,53** |
| SIN DATOS `#C3CAD1` | 11,81 | 10,62 | 9,30 | 9,18 | **8,58** |
| Ámbar inminente `#FFC24D` | 12,17 | 10,94 | 9,58 | 9,46 | **8,85** |
| Naranja IA `#FF9A3D` | 9,25 | 8,32 | 7,28 | 7,19 | **6,73** |
| Azul vos `#5AA9FF` | 7,96 | 7,16 | 6,27 | 6,19 | **5,79** |
| Leyenda `#A9B2BC` | 9,10 | 8,18 | 7,17 | 7,08 | **6,62** |
| Eje 10 px `#8D97A2` | 6,59 | 5,93 | 5,19 | 5,12 | **4,79** |

Panel del mapa al 0,84, peor caso sobre blanco:

| Texto | Contraste |
|---|---|
| Leyenda | 5,76 |
| Eje | **4,17 (no cumple)** |
| Naranja | 5,86 |
| Azul | 5,04 |

Otros pares:

| Par | Contraste | Veredicto |
|---|---|---|
| Blanco sobre rojo `#D93A30` (FRENÁ del lienzo) | 4,57 | Pasa raspando. |
| Blanco sobre rojo `#C62F26` (propuesto) | 5,47 | Cumple. |
| Blanco sobre rojo `#C62F26` al 0,92 sobre blanco | 4,87 | Cumple. |
| Blanco sobre banda neutra `#3B4652` | 9,62 | Cumple. |
| `#A9B2BC` sobre rojo `#D93A30` | 2,13 | No usar. |
| Chip MOSTRAR | 7,90 | Cumple. |
| Chip GRABANDO | 6,94 | Cumple. |
| Chip GT3 | 11,60 | Cumple. |
| Marcador naranja sobre asfalto del mapa `#56616C` (sin borde) | 2,99 | No cumple 3:1 (gráfico). |
| Marcador azul sobre asfalto del mapa `#56616C` (sin borde) | 2,57 | No cumple 3:1 (gráfico). |
| Marcadores con borde `#0C0E11` | 9,15 (naranja) | Cumple. |

**El slider de opacidad rompe el contraste.** En el lienzo, `opacidad` se aplica a todo el contenedor (texto incluido), con rango 0,4–1. Peor caso, sobre blanco, para el blanco puro, `#A9B2BC`, naranja y azul:

| Opacidad | Blanco | `#A9B2BC` | Naranja | Azul |
|---|---|---|---|---|
| 0,85 | 8,78 | 4,65 | 4,64 | 4,12 |
| 0,70 | 5,38 | 3,22 | 3,17 | 2,90 |
| 0,40 | 2,32 | 1,75 | 1,72 | 1,65 |

Aplicando la opacidad **solo al fondo** (texto siempre al 100 %), sobre blanco:

| Alfa del fondo | Blanco | `#A9B2BC` | Naranja | Azul |
|---|---|---|---|---|
| 0,80 | 10,69 | 4,98 | 5,06 | 4,35 |
| 0,70 | 7,32 | 3,41 | 3,47 | 2,98 |

→ Rango permitido del slider: fondo 0,80–0,95, texto al 100 %. Ver 3.3.

### 1.3 Daltonismo [Evidencia]

Simulación con matrices de Machado 2009 (severidad 1,0, en RGB lineal). Diferencia de color en ΔE2000 y de luminancia en L\* de CIELAB.

| Visión | L\* naranja | L\* azul | ΔL\* | ΔE2000 | Contraste WCAG naranja/azul |
|---|---|---|---|---|---|
| Normal | 72,7 | 67,9 | 4,8 | 50,2 | 1,16 |
| Protanopía | 67,4 | 71,1 | 3,7 | 53,7 | 1,12 |
| Deuteranopía | 75,8 | 65,6 | 10,2 | 58,1 | 1,37 |
| Tritanopía | 69,0 | 69,9 | **0,8** | 69,0 | 1,03 |

**Lectura:**

- **Por matiz, bien.** Naranja `#FF9A3D` y azul `#5AA9FF` son casi opuestos en el eje amarillo–azul. Con protanopía y deuteranopía se ven como ocre y azul claro (ΔE > 50). Con tritanopía, como rosa y turquesa (ΔE 69).
- **Por brillo, no.** ΔL\* de 0,8 a 10: son prácticamente iso-luminantes. El handover pide "también por brillo", y eso **no se cumple**. La razón: los dos colores son los más brillantes que admite un texto ≥ 4,5:1 sobre el panel.
- **No lo arreglé cambiando los colores.** Para llegar a ΔL\* ≥ 15 hace falta un azul de L\* ≈ 55 (por ejemplo `#2F7FE8`), que sobre panel claro da 3,6:1. Fallaría el contraste de texto.
- **Arreglo: redundancia no cromática [Criterio]:**
  - IA = **rombo**, vos = **círculo**, en el mapa, en la leyenda, en la regla y en las etiquetas del panel (`◆ IA`, `● VOS`).
  - La palabra "IA" o "VOS" siempre acompaña a la forma.
  - Los dos marcadores de la regla se distinguen por posición y forma de cabeza, no solo por color.
- **Ámbar contra naranja:** el ámbar de inminente (`#FFC24D`) y el naranja de IA tienen ΔE2000 = 14 entre sí. Es poco. Como el ámbar va solo en la cifra del cartel y la IA va solo en la etiqueta con rombo, no se confunden. No usar ámbar en etiquetas.

---

## 2. Pantallas nuevas

Vista: `docs/ui/pantallas.png`.

**Orden vertical propuesto [Criterio]:** guía arriba, mapa abajo. Es lo contrario del lienzo. Razón: el cartel queda más cerca del borde superior y de la línea de visión, y el mapa puede colapsarse sin mover la guía.

**Estructura común de las tarjetas de guía:**

- Ancho 320.
- Radio 10.
- Fondo `rgba(12,14,17,0.88)`.
- Etiqueta de referencia (12 px, 700) encima del número.
- Cartel principal 64 px blanco, más " m" de 30 px `#A9B2BC`.
- Secundario a la derecha, 30 px `#DDE2E7`, con su etiqueta.

| Estado | Contenido exacto |
|---|---|
| **Lejos** | Tarjeta de una línea. A la izquierda "Próxima" (12 px, `#A9B2BC`); a la derecha "CURVA 7 · 640 m" (Barlow 600 22 px; el " · 640 m" en `#A9B2BC`, 500). Metros redondeados a 10. Sin panel grande. |
| **Aproximación** *(fase 5b)* | Encabezado: "CURVA 7" (22/700) y "248 → 96 km/h" (12 px, `#A9B2BC`). Debajo, la leyenda "◆ IA · ● VOS" (12 px, 700, naranja y azul). **Sin número grande.** Abajo, la barra de progreso (regla de 18 px con dos marcadores y el punto blanco "ahora") y la cuenta regresiva chica ("185 m", 14/600 `#DDE2E7`), que baja hasta 0. La regla no lleva números (ver P7). ~~Fila: etiqueta + cartel en blanco.~~ |
| **Inminente (< 100 m)** *(fase 5b)* | Igual que Aproximación, pero la cuenta regresiva pasa a `#FFC24D`. No cambia tamaño ni posición. ~~La cifra del cartel~~ ya no existe. |
| **FRENÁ** *(fase 5b)* | Franja superior de 40 px en `#C62F26` con "FRENÁ" (Barlow 700 30 px, blanca, espaciado 0,04 em) a la izquierda y "CURVA 7" (22/700) a la derecha. Cuerpo oscuro: leyenda "◆ IA · ● VOS" y "248 → 96 km/h". **Sin regla, ni cuenta regresiva, ni cartel.** Se oculta pasado el vértice. En el mismo instante se ve el **aviso de frenada** (2.1). |
| **TOQUE** | Franja neutra `#3B4652` con "TOQUE" y "CURVA N". Cuerpo: etiqueta + velocidad mínima en 64 px con " km/h" a 30 px. Debajo, metros chicos (14/600 `#DDE2E7`, ej. "85 m"). **Nunca rojo.** |
| **LEVANTÁ** | Igual que TOQUE con "LEVANTÁ" y **sin metros**. |
| **Cabecera** *(fase 5b, reemplaza al mini mapa)* | Tarjeta chica con la pista y variante (Inter 12, `#A9B2BC`, se recorta con "…" **sin pisar los chips**), el chip de la clase y el chip de modo (MOSTRAR / GRABANDO). Debajo, los avisos fuera de banda ("No se pudo guardar…"). Fondo con el mismo alfa que el panel. ~~Mini mapa: marcadores llenos y anillos vacíos.~~ |
| **GRABANDO** (flag) | Chip "● GRABANDO" (`#4A1F1C` / `#FF9C94`, punto `#FF5A4E`) en la cabecera, en lugar del chip MOSTRAR. Siempre visible mientras `Grabando` sea verdadero. Los contadores **IA / VOS** (sin MAPA, fase 5b) ocupan el lugar de la guía solo en SinDatos y Lejos. En cualquier otro estado manda la guía. Pie: "Parar de grabar: Bandeja › Modo › Mostrar." |
| **GRABANDO · SOLO VOS** *(fase 5c)* | Mismo chip rojo, mismos colores y punto, con el texto "GRABANDO · SOLO VOS" (modo "Grabar solo vos"). Los contadores muestran **solo VOS** (la columna de la IA se oculta y la de VOS ocupa el ancho) con el encabezado "GT3 · grabando solo vos" y el pie "Solo se graba tu auto: la IA ya guardada no se toca. Parar de grabar: Bandeja › Modo › Mostrar." La guía se muestra igual que en Grabar, con lo ya guardado. |
| **SIN DATOS** | "SIN DATOS" (Barlow 700 30 px, `#C3CAD1`). Línea 1 (13 px, `#DDE2E7`) y línea 2 (13 px, `#A9B2BC`) según la **causa** (fase 5c, abajo). |

**Causas del SIN DATOS (fase 5c).** El motor primero mira si hay algo que mostrar y recién después si la lectura es válida; así el panel nombra lo que de verdad pasa. Orden de prioridad:

| Causa | Línea 1 | Línea 2 |
|---|---|---|
| Archivo de la clase ilegible (JSON dañado y sin `.bak` bueno) | "No se pudo leer el archivo de datos." | "Revisá GT3_Gen2.json en la carpeta de datos (Bandeja › Datos) o restaurá su copia .bak." |
| No hay archivo, o no tiene curvas | "No hay curvas guardadas para GT3 en Pista de ejemplo · GP." | "Bandeja › Modo › Grabar todo y da unas 10 vueltas con IA en pista." |
| Hay curvas pero ninguna sirve (frenado sin sentido) | "Datos sin curvas válidas." | "Las curvas de GT3_Gen2.json no tienen un frenado utilizable. Regrabá la clase." |
| El largo de la pista del juego difiere más de 2 % del guardado | "Largo de pista distinto al guardado (guardado 5000 m, juego 5891 m)." | "Puede ser otra variante o una versión distinta de la pista. Regrabá la clase en Bandeja › Modo › Grabar todo." |
| Datos buenos pero todavía no hubo una lectura válida en pista (boxes, garaje) | "En boxes: la guía arranca al salir a pista." | (sin línea 2) |

En modo Grabar y Grabar solo vos, el SIN DATOS y el Lejos siguen cediendo su lugar a los contadores. Si ya hubo guía en la sesión y se entra a boxes, se conserva lo último que se mostró (como antes).
| ~~**Dos carteles distintos** / **Mismo cartel**~~ | *(Fase 5b)* Ya no se muestran carteles. Con IA y vos en la curva, la leyenda lleva las dos etiquetas y la barra los dos marcadores; con una sola referencia, una sola etiqueta y un solo marcador. |
| **Desbloqueado** | Contorno discontinuo de 2 px `#5AA9FF` con separación de 3 px, más un chip "DESBLOQUEADO · arrastrá para mover". Opacidad forzada a 100 % mientras esté desbloqueado. La ventana del aviso de frenada también se ve (2.1). |

**Reglas de Toque y Levantá [Criterio]:**

- **Velocidad secundaria:** mostrarla solo si la diferencia con la principal es de 3 km/h o más. Si no, es ruido.
- **Etiqueta única:** si la curva tiene una sola referencia, una sola etiqueta.

### 2.1 Aviso de frenada (fase 5b)

Símbolo de "freno" rojo en una **ventana overlay aparte** (la misma técnica que el panel: sin bordes, siempre arriba, click-through bloqueada), que aparece en el punto de frenado. Idea tomada de los avisos de frenada de los juegos de autos; el dibujo es **propio** (vector de WPF, ver abajo), no es ningún logo ni imagen de terceros.

**Dibujo.** Círculo con signo de exclamación "!" y dos arcos a los costados, como el testigo del sistema de frenos de un auto. Cuadro de 120 × 120; centro (60, 60):

| Pieza | Geometría | Trazo |
|---|---|---|
| Anillo | circunferencia r = 30 | 7 px |
| Arco derecho / izquierdo | arco r = 47, de −40° a +40° (y su espejo) | 7 px, puntas redondas |
| "!" | línea de (60, 43) a (60, 65) | 7 px, puntas redondas |
| Punto del "!" | círculo r = 4,2 en (60, 77) | relleno |

- **Color:** rojo `#E53935` sobre fondo **transparente**.
- **Contorno oscuro fino:** `#0C0E11`, 2 px por lado (trazo 11 contra 7), para que el rojo se lea sobre cualquier fondo (cielo, asfalto, nieve). Primero se dibujan todos los contornos y después todo el rojo.
- **Tamaño:** 120 px por defecto; escala **50–300 %** (60 a 360 px). Cuadrada, sin márgenes extra.
- **Contraste [Evidencia, WCAG calculado]:** `#E53935` contra su contorno `#0C0E11` da **4,57 : 1**; contra blanco, 4,23 : 1; contra el cielo claro `#BFD8EE`, solo **2,87 : 1**. Por eso el contorno oscuro es obligatorio: separa el símbolo del fondo aunque el rojo solo no alcance. Hay que verlo sobre cielo claro y asfalto con el .exe real.
- **Daltonismo:** no depende solo del rojo: la forma (círculo con "!" y arcos) y la franja con la palabra FRENÁ alcanzan.

**Comportamiento (fase 5c, vigente).** La decisión la toma `DecisorAviso` (Core, con pruebas) **una vez por muestra** y la comparten el símbolo y el pitido; `AvisoFrenada` solo la dibuja a 60 Hz (opacidad y escala).

| Regla | Valor |
|---|---|
| Rampa (aparece gradualmente) | La opacidad sube linealmente de 0 a 1 según el **tiempo que falta** a **T_aviso** (ver abajo). Empieza a **2,0 s** antes de T_aviso (configurable de 1 a 3 s: `avisoRampaS` y menú "Inicio de la rampa del símbolo") y llega al **100 % justo en T_aviso**, que sin anticipación es el punto de frenado. La escala acompaña: 85 % → 100 % |
| La rampa no vuelve atrás | Se guarda el máximo de la pasada: si levantás y la velocidad cae, o hay ruido, no baja. Un hueco de datos de ~0,45 s solo la adelanta |
| Pleno | Se mantiene al 100 % hasta que el pedal (`mBrake` del auto visto) supera **0,15**, o pasan **1,5 s** después del **punto** de frenado, no de T_aviso (configurable, 1 a 2 s: `avisoDuracionS`), o la guía deja de estar en FRENÁ (pasó el vértice) |
| Transición | Sube **exactamente** al valor de la rampa, sin fundido ni tope propios (la rampa ya es la entrada; en la 5c un tope de 80 ms y de 40 ms por tick de reloj retrasaba el 100 % respecto del pitido); al desaparecer, fundido corto de **80 ms**. Sin parpadeo, pulso ni destello. Se anima a 60 Hz con el reloj, no con la telemetría (~27 Hz) |
| Pitido | **Una sola vez**, en **T_aviso**, en la misma muestra en que el símbolo llega al 100 %, y solo si el pedal no supera 0,15 en ese momento |
| Ya estás frenando | Si el pedal supera 0,15 **al llegar** al punto, o ya lo superaste **dentro de la zona inmediata** (últimos **1,0 s o 40 m** antes de T_aviso, lo que abarque más), esa pasada queda atendida: ni símbolo ni pitido, aunque después sueltes el pedal. Un toque de pedal más lejos del punto (por ejemplo el final de la frenada anterior) solo oculta el símbolo mientras dura y lo deja seguir cuando soltás |
| Referencia principal | Rige **solo** la referencia principal elegida (bandeja › Referencia principal). Si la curva no tiene bloque de esa referencia (la guía muestra la otra para mostrar algo, con su etiqueta IA o VOS), **no hay ni pitido ni símbolo** en esa curva. Al cambiar la referencia rige la nueva |
| Repetición | **No vuelve** a aparecer ni pitar en la misma curva en la misma pasada. Se rearma al cambiar de curva o al volver a Lejos / SIN DATOS (otra vuelta) |
| Toque y Levantá | Nunca disparan el aviso (solo las curvas de frenada) |
| Opción en el menú | "Aviso de frenada (símbolo)" activa o desactiva; "Probar aviso" corre una pasada de prueba (rampa completa, pita al terminarla si el pitido está activado; aunque el aviso esté desactivado) y "Probar pitido" la misma pero pita siempre; tamaño, anticipación y duración también en el menú |
| Con "Ocultar overlay" | El aviso también se apaga |

**T_aviso y la anticipación (fase 5d).** Hay un único instante de aviso: **T_aviso = instante del punto de frenado − anticipación A** (`avisoAnticipacionS`, 0 a 2,0 s, paso 0,1 s, por defecto 0 = como antes). Se calcula con el mismo tiempo restante que usa la rampa (metros hasta el punto ÷ velocidad actual, piso de 3 m/s): T_aviso llega cuando ese tiempo baja de A (o cuando la guía ya está en FRENÁ). De ahí dependen los dos: la rampa termina (opacidad 1,0) y el pitido se pide **en la misma muestra** del `DecisorAviso` (una sola condición, `llegoAviso`), así que no puede haber desfase de lógica; la rampa empieza `avisoRampaS` antes de T_aviso. El símbolo sigue al 100 % hasta que frenás, hasta 1,5 s después del **punto** de frenado (estimado con la velocidad mientras la guía no llegó a FRENÁ) o hasta el vértice, lo que ocurra primero; la regla "ya estoy frenando" se evalúa con T_aviso. Ejemplo: A = 1,5 s a 216 km/h (60 m/s): pita cuando faltan 90 m. La ventana de la guía (5 s a la velocidad actual, entre 120 y 400 m) limita lo anticipable: con rampa 3 s + A 2 s a más de ~290 km/h la rampa arranca un poco tarde. Persistencia en `config.json`; la propiedad `ServicioOverlay.AvisoAnticipacionS` queda lista para el deslizador del menú de configuración (mientras tanto, bandeja › "Anticipación del pitido y símbolo": 0; 0,2; 0,4; 0,6; 0,8; 1,0; 1,5; 2,0 s).

**Latencia propia (no eliminable, sin medir).** La lógica garantiza la sincronía en la muestra; lo que queda después del `Evaluar` no se puede medir en Linux: (1) la muestra llega cuantizada a la telemetría (hasta 1/27 s = 37 ms o 1/39 s = 26 ms de demora, media la mitad); (2) la ventana es transparente (`AllowsTransparency`, ventana en capas) y el cuadro pintado se compone en el siguiente refresco, 1 a 2 cuadros a 60 Hz (16–33 ms); (3) `SoundPlayer` arranca el WAV con una demora de decenas de ms. La ventana no suma ningún fundido de entrada propio. `DecisorAviso.CompensacionLatenciaS` / `ServicioOverlay.CompensacionLatenciaS` (0 a 0,25 s, **0 por defecto, sin calibrar en pista**) adelanta T_aviso, y con él pitido y símbolo por igual; no corrige la diferencia relativa sonido-vs-imagen, que habría que medir con video + audio.

**Por qué el símbolo llegaba tarde (fase 5b).** En el código de la 5b el pitido y el símbolo se disparaban en la misma muestra (la guía entrando en FRENÁ), pero el símbolo nacía **con opacidad 0 en ese instante** y recién llegaba al 100 % a los 80 ms (sin contar el tope de 40 ms por tick del reloj y que la ventana es transparente con composición por software), mientras el pitido suena de inmediato. Además, aparecer *en* el punto de frenado no deja tiempo de reacción. Con la rampa el símbolo ya está completo en el punto y empieza a verse 2 s antes; el pitido sigue sonando en el punto. No se pudo medir en vivo (compila, sin ejecutar).

**Comportamiento anterior (fase 5b, reemplazado).** Aparecía de golpe al entrar la guía en FRENÁ, con fundido de 80 ms; desaparecía con el pedal > 0,15, a los 1,5 s o al pasar el vértice; el pitido sonaba aunque ya estuvieras frenando y con la referencia que cayera por defecto.

**Ventana.** Bloqueada: invisible (opacidad 0), no recibe clics ni foco (`WS_EX_TRANSPARENT | NOACTIVATE | TOOLWINDOW`). Desbloqueada (**el mismo atajo `Ctrl+Alt+L` y el mismo ítem de la bandeja valen para las dos ventanas**): se ve el símbolo a media opacidad como mínimo sobre un fondo oscuro, con contorno azul discontinuo y un chip "AVISO"; se arrastra con el botón izquierdo y se cambia el tamaño con la rueda (pasos de 10 %) o con el agarre de la esquina. Posición (por monitor) y escala se guardan en `config.json` (`avisoPosiciones`, `avisoEscala`). Sin posición guardada: centrada en horizontal, a 30 % del alto del monitor.

**Verificar con el .exe real:** que la rampa de 2 s se vea natural y llegue completa justo en el punto; si 0,15, la zona de 1 s / 40 m y 1,5 s son buenos valores; que el fundido de 80 ms no se note como parpadeo; el contraste del contorno; que la ventana transparente no robe el foco ni los clics de AMS2.

**Estados no cubiertos por `PanelGuia` pero necesarios [Criterio]**

Hace falta mostrar estos mensajes en la tarjeta de SIN DATOS, con el mismo estilo:

| Situación | Texto sugerido |
|---|---|
| AMS2 no detectado | "Esperando AMS2" / "Activá Options › System › Shared Memory = Project CARS 2." |
| `mVersion` distinta de 14 | "Versión de datos no compatible" |

El handover pide avisar en el panel en el segundo caso. Falta definir el primero.

---

## 3. Medidas finales para WPF

Todas las medidas están en px = DIP a 96 dpi. Con escala del usuario 75–150 % se aplica un `ScaleTransform` al conjunto.

### 3.1 Tipografía

| Elemento | Fuente | Tamaño | Peso | Color |
|---|---|---|---|---|
| ~~Cartel~~ (fase 5b: solo la velocidad mínima de Toque y Levantá) | Barlow Condensed | 64 (line-height 0,95) | 700 | `#FFFFFF` |
| Unidad " km/h" | Barlow Condensed | 30 | 600 | `#A9B2BC` |
| Secundario | Barlow Condensed | 30 | 600 | `#DDE2E7` |
| Palabra de estado (FRENÁ, TOQUE, LEVANTÁ) | Barlow Condensed | 30, espaciado 0,04 em | 700 | `#FFFFFF` |
| "CURVA N" | Barlow Condensed | 22, espaciado 0,03 em | 700 | `#EEF1F4` |
| Cuenta regresiva y metros de Toque | Barlow Condensed | 14 | 600 | `#DDE2E7` (inminente `#FFC24D`) |
| Etiqueta IA / VOS | Inter | 12, espaciado 0,08 em | 700 | naranja o azul |
| Texto de apoyo, leyendas, velocidades "248 → 96" | Inter | 12 | 400 | `#A9B2BC` |
| Texto de SIN DATOS (explicación) | Inter | 13 | 400 | `#DDE2E7` / `#A9B2BC` |
| Chips | Inter | 11 | 600 | ver 3.2 |
| Contadores de GRABANDO (IA y VOS) | Barlow Condensed | 26 | 600 | `#EEF1F4` |

**Cambio respecto del lienzo:** los textos de 10 y 11 px pasan a 12 px como mínimo. La regla pierde sus números de 10 px.

**Fuentes en WPF.** El lienzo las carga desde Google Fonts. WPF no hace eso. Hay que **embeber Barlow Condensed (500/600/700) e Inter (400/500/600)** como recursos (licencia OFL, permite embeber). Como respaldo: `Bahnschrift SemiCondensed` para Barlow y `Segoe UI` para Inter. Los dígitos del cartel deben ocupar un ancho fijo (reservar el ancho de "000"), así no se mueve el layout.

### 3.2 Tamaños, márgenes y posición

| Medida | Valor |
|---|---|
| Ancho por defecto | 320 |
| Ancho mínimo | 240 (escala 75 %) |
| Ancho máximo | 480 (escala 150 %) |
| Radio de tarjetas | 10 |
| Radio de chips y regla | 4 |
| Relleno de tarjeta | 12 arriba, 14 a los lados, 14 abajo |
| Separación entre tarjetas | 8 |
| Separación interna | 10 |
| Franja de estado (FRENÁ, TOQUE, LEVANTÁ) | 40 de alto |
| Regla | 18 de alto, marcadores de 4 × 24 |
| ~~Mapa~~ | *(fase 5b: no existe)* |
| Cabecera (pista + chips) | ancho del panel, relleno 8 / 12 |
| Aviso de frenada | 120 × 120 (escala 50–300 %), ventana aparte |
| Posición por defecto | Arriba a la derecha, margen 24 / 24 (solo la primera vez; después se mueve a mano) |
| Posición y escala | Se recuerdan por monitor (overlay y aviso por separado) |

**Alto aproximado del conjunto** *(fase 5b, sin mapa; estimado, no medido en WPF)*:

| Estado | Alto aproximado |
|---|---|
| Cabecera sola (Lejos: una línea + cabecera) | ~100 |
| Aproximación (encabezado, leyenda, barra, metros + cabecera) | ~210 |
| FRENÁ (franja 40 + cuerpo + cabecera) | ~170 |

~~Mapa + guía ~470 (44 % del alto a 1080p).~~ Sin el mapa el conjunto ocupa menos de la mitad.

**Por qué no tapa el HUD [Supuesto, no verificado].** Supuse esta disposición del HUD de AMS2:

- Tabla de posiciones y tiempos arriba a la izquierda.
- Retrovisor virtual arriba al centro.
- Velocidad y marcha abajo al centro y a la derecha.

Con eso, arriba a la derecha queda libre. **Riesgo:** AMS2 puede mostrar ahí un cronómetro de vuelta o un mapa de pista según la configuración. Por eso:

- La posición es arrastrable (fase 5b: se sacaron los atajos de esquina del menú; se mueve a mano).
- La verificación con el `.exe` real (sección 5) incluye una captura con el HUD completo activo.

### 3.3 Colores y opacidades

| Rol | Valor |
|---|---|
| Fondo del panel de guía | `rgba(12,14,17,0.88)` |
| ~~Fondo del mapa~~ | *(fase 5b: la cabecera usa el mismo alfa que el panel)* |
| Rango del slider "Opacidad" | Alfa del fondo **0,80–0,95**, mapeado desde 0–100 % del slider. El texto no se atenúa nunca. |
| Naranja IA | `#FF9A3D` |
| Azul vos | `#5AA9FF` |
| Ámbar inminente | `#FFC24D` |
| Rojo FRENÁ (franja) | `#C62F26` (antes `#D93A30` sobre todo el panel). Única opción (fase 5b) |
| Rojo del aviso de frenada | `#E53935`, contorno `#0C0E11` de 2 px por lado |
| Franja neutra (Toque, Levantá) | `#3B4652` |
| Fondo de regla / base de regla | `#232A31` / `#33414E` |
| Marcas de regla | `#4A5561` |
| Texto principal / secundario / apoyo | `#FFFFFF` / `#DDE2E7` / `#A9B2BC` |
| Chip GT3 | `#2A3139` / `#EEF1F4` |
| Chip MOSTRAR | `#1E3A2A` / `#8EE0A9` |
| Chip GRABANDO | `#4A1F1C` / `#FF9C94`, punto `#FF5A4E` |
| Chip DESBLOQUEADO | `#17304D` / `#8CC3FF` |

### 3.4 Bloqueo, desbloqueo y bandeja

**Bloqueado (por defecto):**

- La ventana usa los estilos extendidos `WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`. Los clics atraviesan hasta AMS2, no toma foco y no aparece en Alt-Tab.
- `Topmost = true`.

**Desbloqueado:**

- Se quita `WS_EX_TRANSPARENT`. Se mantiene `NOACTIVATE` si se puede.
- Contorno discontinuo azul y chip (pantalla 12).
- Se arrastra con el botón izquierdo y se escala con la rueda o con el control del menú.
- **Fase 5b:** lo mismo vale para la ventana del aviso de frenada (2.1): un solo atajo y un solo ítem bloquean o desbloquean las dos.
- Opacidad al 100 %.

**Cómo se entiende sin manual [Criterio]:**

- El contorno discontinuo es la señal. El chip dice qué hacer y cómo volver.
- Primer arranque: globo de notificación "El overlay está bloqueado. Usá el ícono de la bandeja para desbloquearlo".
- Atajo global configurable (propuesta: `Ctrl+Alt+L`).
- El ícono de bandeja cambia con el estado (candado cerrado / abierto). En Windows 11 el ícono suele quedar oculto en el desplegable: avisarlo en el globo.

**Menú de bandeja, orden vigente (fase 5b):**

1. Bloquear overlay (con marca y el atajo).
2. Modo *(fase 5c)*: **Grabar todo (IA + vos)** / **Grabar solo vos** / **Mostrar**.
3. Referencia principal: IA / Vos.
4. Tamaño del overlay y Opacidad del fondo.
5. Aviso de frenada (marca), Probar aviso, Tamaño del aviso (50–300 %), **Anticipación del aviso** (1,0 a 3,0 s antes), Duración del aviso después del punto (1,0 / 1,5 / 2,0 s).
6. Pitido al frenar (marca) y Probar pitido.
7. Atajo de bloqueo.
8. Ocultar / Mostrar overlay.
9. Datos: Abrir carpeta de datos, Abrir carpeta de capturas, Exportar datos…, Importar datos….
10. Diagnóstico….
11. Salir.

~~Posición: cuatro esquinas, Mapa compacto, Mostrar mapa, Rojo suave~~: sacados.

Con el overlay bloqueado, **no hay forma de hacer clic sobre él**. La bandeja y el atajo son la única vía. Eso es lo que más hay que probar (sección 5).

### 3.5 Anti-parpadeo y rojo no invasivo

**Lo que ya hace la lógica de la fase 4 [Evidencia, `OpcionesGuia`]:**

- Histéresis de 20 m.
- 1,0 s sostenido antes de retroceder de estado.
- Margen de vértice de 5 m.

La interfaz **no agrega su propia lógica de estado**. Solo dibuja.

**Reglas para la interfaz [Criterio]:**

| Elemento | Regla |
|---|---|
| Aparición del panel de guía | Fundido de entrada de 120 ms. |
| Desaparición del panel de guía | Fundido de salida de 200 ms. |
| Curva siguiente ya activa | Cambio de contenido inmediato, sin fundido de salida (para chicanes). |
| Paso Aproximación → Inminente | Cambio de color instantáneo. Sin animación de tamaño. |
| Aparición de la franja FRENÁ | Fundido de 80 ms. **Sin pulso, sin destello, sin parpadeo.** |
| Actualización de números | Máximo 10 Hz. Ancho de cifras fijo. |
| Cuenta regresiva | Metros enteros, decreciente hasta 0; en Lejos, redondeados a 10. |
| Duración de FRENÁ en pantalla | Desde el punto de frenado hasta el vértice (unos segundos). Sin límite adicional. |
| Aviso de frenada | Rampa de 2 s hasta el punto (1 a 3 s), pleno hasta 1,5 s después, fundido de salida de 80 ms (2.1) |

**Rojo no invasivo.** En el lienzo todo el panel (320 × ~200) se pone rojo `#D93A30`. Es mucha superficie roja en el campo visual periférico, justo cuando más atención hay que prestar a la pista. Propuesta: **solo la franja superior de 40 px en rojo**, y el cuerpo sigue oscuro. ~~Alternativa: opción "Rojo suave"~~ (contorno de 3 px y franja `#5A1D19`): **sacada en la fase 5b** por decisión del usuario; queda la franja roja de 40 px por defecto.

---

## 4. Problemas, con prioridad

| # | Prioridad | Problema | Propuesta |
|---|---|---|---|
| P1 | **Bloquea** | El lienzo todavía muestra la cuenta regresiva como número grande ("185 m" y "FRENÁ · VOS"). Es lo contrario de la decisión del usuario. | Rehacer Aproximación, Inminente y FRENÁ como en la sección 2. |
| P2 | **Bloquea** | Las fuentes dependen de Google Fonts. En WPF no cargan: el overlay saldría con otra tipografía y distintos anchos. | Embeber Barlow Condensed e Inter como recursos. Respaldo `Bahnschrift SemiCondensed` y `Segoe UI`. Cifras de ancho fijo. |
| P3 | **Importante** | El slider de opacidad (0,4–1) atenúa también el texto. Por debajo de 0,85 el contraste cae de 4,5:1 sobre blanco. | Aplicar la opacidad solo al fondo, con rango 0,80–0,95 (3.3). |
| P4 | **Importante** | Naranja y azul son iso-luminantes (ΔL\* 0,8–10). No se distinguen por brillo. | Forma redundante: rombo = IA, círculo = vos, en todo el overlay (1.3). |
| P5 | **Importante** | El panel rojo completo es invasivo. | Franja roja de 40 px, cuerpo oscuro (3.5). ~~Opción "Rojo suave"~~ (sacada en 5b). |
| P6 | **Resuelto en 5b** | Overlay alto: ~470 px a 1080p (44 % del alto). | Sin mini mapa: queda menos de la mitad. Posición arrastrable (3.2). |
| P7 | **Importante** | Textos de 10 y 11 px (ejes de regla, leyendas, etiquetas). Eje de 10 px = 4,17:1 sobre blanco en el mapa. | Mínimo 12 px. Quitar los números de la regla, que queda como indicador. |
| P8 | **Importante** | SIN DATOS y GRABANDO no dicen cómo actuar con precisión (dónde cambiar de modo, cuándo parar). | Textos de las pantallas 9 y 10. |
| P9 | **Importante** | Con el overlay bloqueado no se puede hacer clic; la bandeja suele estar oculta en Windows 11. | Atajo global, globo en el primer arranque, ícono de estado (3.4). |
| P10 | **Importante** | Falta el tablero `Ventana.dc.html` en el material recibido; no se puede validar el menú. | Mandarlo antes de la fase 5, o aprobar el orden de la sección 3.4. |
| P11 | Menor | Marcadores del mapa sin borde: 2,99 y 2,57:1 sobre el asfalto. Solapan cuando IA y vos están cerca. | Halo oscuro y radio mayor (5–6). Desplazar el marcador de vos unos 6 px en sentido perpendicular a la pista. |
| P12 | Menor | Ámbar de Inminente y naranja de IA son parecidos (ΔE2000 = 14). | Ámbar solo en la cifra del cartel, nunca en etiquetas. |
| P13 | Menor | La regla muestra 100/200/300 m fijos, pero la ventana va de 120 a 400 m. | Escala de la regla = `VentanaM` del `PanelGuia`. |
| P14 | Menor | El estado "AMS2 no detectado" y "versión no compatible" no tienen pantalla. | Usar la tarjeta de SIN DATOS con los textos de la sección 2. |
| P15 | Menor | Falta definir si la velocidad secundaria de Toque aparece siempre. | Solo si difiere 3 km/h o más. |
| P16 | Pendiente (5b) | Cambiar de modo (Grabar / Mostrar) obliga a ir a la bandeja. | **No implementado.** Previsto: botón en la cabecera del HUD (el chip de modo) que alterne el modo, activo solo con el overlay desbloqueado. |
| P17 | Pendiente (5b) | Con el overlay desbloqueado no hay forma de cerrarlo sin la bandeja. | **No implementado.** Previsto: "✕" y menú contextual (clic derecho) sobre el overlay desbloqueado con Bloquear, Ocultar y Salir. |
| P18 | Menor (5b) | El nombre de la pista se recortaba por el chip de la clase. | **Arreglado** (era trivial): la cabecera usa dos columnas, el nombre ocupa el espacio libre, se recorta con "…" y muestra el nombre completo al pasar el mouse. Falta verlo en pantalla. |

---

## 5. Qué verificar con el `.exe` real (segunda participación)

Material necesario: capturas de pantalla con AMS2 en Borderless, HUD completo activo y overlay encima.

1. **Lectura en 0,5 s:** que la barra de progreso, el nombre de la curva y de quién es cada color se lean con un vistazo (fase 5b: ya no hay cartel).
2. **Contraste real:** sobre cielo, pianos, nieve o lluvia, con el slider en 0,80 y en 0,95. Medir con el color de píxel real de las capturas.
3. **HUD:** que arriba a la derecha no tape tiempos, posiciones, retrovisor virtual ni mapa. Probar con varias configuraciones de HUD y con 1080p, 1440p y 4K.
4. **Fuentes:** que se vean Barlow Condensed e Inter y no el respaldo; que las cifras no se muevan de ancho.
5. **Parpadeo:** grabar 10 s de video pasando por Lejos, Aproximación, Inminente, FRENÁ y de vuelta, incluyendo una chicane. Contar cambios de estado por segundo.
6. **Rojo de FRENÁ:** que la franja roja no distraiga ni resulte molesta. *(Fase 5b: ya no hay "Rojo suave" para comparar.)* Ver también el aviso de frenada (2.1).
7. **Bloqueo y clics:** con el overlay bloqueado, que los clics y las teclas lleguen a AMS2 y que no robe el foco. Con el overlay desbloqueado, que al arrastrar no minimice ni cambie el modo de AMS2.
8. **Always on Top:** que se mantenga encima de AMS2 en Borderless, sin PowerToys.
9. **Bandeja y atajo:** que el ícono se encuentre en Windows 11, que el atajo no choque con los botones de la base Moza ni con atajos del juego, y que el globo de primer arranque se vea.
10. **Daltonismo:** pasar las capturas reales por un simulador y confirmar que forma y etiqueta alcanzan.
11. ~~Mapa~~ *(fase 5b)*: la barra de progreso con el rombo (IA) y el círculo (vos) se distingue a escala real, y los dos marcadores no se tapan.
12. **Textos de SIN DATOS y GRABANDO:** que se entiendan sin documentación.

---

## 6. Supuestos

- **HUD de AMS2:** disposición supuesta en 3.2. No verificada.
- **Cielo claro:** `#BFD8EE`. El peor caso usado es blanco puro.
- **Simulación de daltonismo:** matrices de Machado 2009 con severidad completa. Es una aproximación, no una prueba con personas.
- **Pantalla:** monitor de 1080p, escala 100 %. El lienzo está a 1280 × 720.
- **Mapeo de ámbar:** el ámbar de Inminente se aplica a la cifra del cartel (el lienzo lo aplicaba a la cuenta regresiva, que ahora es secundaria). Es mi lectura de la decisión del usuario.
- **Ventana.dc.html:** no recibido, por lo que bloqueo y bandeja se especifican sin dibujo de referencia.
- **Atajo `Ctrl+Alt+L`:** propuesta. No se comprobó si choca con Moza o con AMS2.
- **Alto del overlay:** medido en la ilustración HTML, no en WPF.

---

## 7. Fase 6 – Rediseño

Agente 2. Rama `fase-6-ui-spec`. Solo docs: no se tocó `src/`. Pantallas: [`docs/ui/pantallas-fase6.html`](ui/pantallas-fase6.html) (fuente) y [`.png`](ui/pantallas-fase6.png). Contraste: `python3 docs/ui/contraste_fase6.py`.

**Etiquetas.** **[Evidencia]** = calculado con el script o leído del código. **[Criterio]** = decisión de diseño. **[Supuesto]** = ver 7.12. Todo lo que dependa del juego o de Windows en ejecución: **sin verificar en AMS2** (el .exe de fases previas tampoco se ejecutó en este entorno).

**Material de estética.** Capturas locales del usuario, solo para mirar. Se tomó el estilo (carbono, esquina cortada, acento rojo, títulos en mayúsculas condensadas, cifras con ":", indicadores segmentados). No se copió ninguna imagen, logo ni nombre; la textura y los íconos son vectores propios.

### 7.1 Qué cambia, por pedido

| Pedido | Resolución | Dónde |
|---|---|---|
| A. Estética | Tarjetas carbono con corte de 14 px, acento rojo chico, barra segmentada, cifras con ":" en GRABANDO | 7.2, 7.3 |
| B. Engranaje y menú | Ventana de menú de 624 px, dos columnas, interruptores, deslizadores, páginas internas | 7.5 |
| C. Fijar | Botón pin con dos estados; "fijado" = no mover ni cambiar tamaño; engranaje y pin siempre activos | 7.4 |
| D. ✕ y modo | ✕ con doble paso en el lugar; modo abre popover de 3 opciones (no cicla) | 7.4 |
| E. Anticipación | Deslizador 0 a 2,0 s, paso 0,1, defecto 0 | 7.6 |

### 7.2 Tokens, tipografía y medidas

**Colores [Criterio, contraste en 7.8]**

| Token | Hex | Uso |
|---|---|---|
| `base` | `#0C0E11` | Fondo del panel (con alfa 0,85 a 0,95) |
| `menu` / `sup` | `#0F1216` / `#161A1F` | Fondo opaco del menú / tarjetas y botones |
| `sup-h` / `sup-p` | `#222831` / `#2F3842` | Hover / presionado |
| `borde` / `borde-f` | `#2E3640` / `#5A6673` | Borde de tarjeta / de botón |
| `pista` | `#66727F` | Pista de deslizador |
| `rojo` | `#C62F26` | Franja FRENÁ, acento de 3 px, interruptor ON, botón CERRAR |
| `rojo-et` | `#FF8A80` | Texto de etiquetas rojas chicas y pin fijado |
| `aviso` | `#E53935` | Símbolo de aviso (sin cambios, contorno `#0C0E11`) |
| `ia` / `vos` / `ambar` | `#FF9A3D` / `#5AA9FF` / `#FFC24D` | Sin cambios |
| `neutra` | `#3B4652` | Franja TOQUE / LEVANTÁ |
| `t1` `tit` `t2` `t3` `sd` | `#FFFFFF` `#EEF1F4` `#DDE2E7` `#A9B2BC` `#C3CAD1` | Texto (sin cambios) |

**Tipografía.** Solo las fuentes ya embebidas. "Ancho" de los títulos: se aproxima con espaciado, sin sumar fuentes.

| Elemento | Fuente | Tamaño / peso | Nota |
|---|---|---|---|
| FRENÁ, TOQUE, LEVANTÁ, SIN DATOS | Barlow Condensed | 30 / 700, espaciado 0,06 em, mayúsculas | Antes 0,04 em |
| CURVA N, título del menú, "Datos" | Barlow Condensed | 22 / 700, espaciado 0,06 a 0,08 em, mayúsculas | Con acento rojo de 28 × 3 debajo |
| Cifra de TOQUE / LEVANTÁ | Barlow Condensed | 64 / 700 | Sin cambios |
| Cifras de GRABANDO | Barlow Condensed | 44 / 600, separadas por ":" | Antes 26 |
| Valor del deslizador | Barlow Condensed | 18 / 600 | Coma decimal ("0,8 s") |
| Cuenta de metros | Barlow Condensed | 14 / 600 | Sin cambios |
| Etiqueta de sección, etiqueta roja | Inter | 12 / 700, 0,12 em, mayúsculas | `rojo-et` |
| Fila del menú | Inter | 14 / 600 | `tit` |
| Ayuda | Inter | 12 / 400 | `t3`, máx. 3 líneas |
| Chips, teclas, botones de texto | Inter | **12** / 600 a 700 | Antes 11: ahora 12 px es el mínimo en todo |

**Medidas** (px = DIP, a escala 100 %)

| Medida | Valor |
|---|---|
| Ancho de tarjeta | 320 (240 a 480 con la escala) |
| Esquina cortada | **14 × 14 a 45°, abajo a la derecha** (16 en el menú). Sin radios redondeados |
| Franja de estado | 40 de alto, rectangular |
| Relleno de tarjeta | 12 arriba, 14 a los lados, **18 abajo** (el corte no pisa texto) |
| Cabecera + barra | 320 × ≈ 72 (fila de pista 20 + barra 32 + márgenes). Va **arriba** de la tarjeta |
| Separación entre tarjetas | 8 |
| Barra segmentada | 16 segmentos de 10 de alto, separación 3; marcadores de 14 × 12 con tallo de 8; alto total 32 |
| Botón | **28 × 28 visibles, 32 × 32 clicables**; ícono de 16 |
| Interruptor | 44 × 24; perilla de 18 con tilde |
| Deslizador | pista de 4; perilla de 8 × 20; zona de arrastre de 28 de alto |
| Segmentado | alto 36, separación 2 |
| Menú | 624 de ancho (dos columnas de 288, separación 16, relleno 16), ≈ 540 de alto |
| Alto del conjunto en Aproximación | ≈ 290 (antes ≈ 210 + cabecera de ≈ 36) [estimado, no medido en WPF] |

**Textura "carbono" barata [Criterio].** Un `DrawingBrush` congelado y compartido, `TileMode=Tile`, `ViewportUnits=Absolute`, tile de **6 × 6 DIP**: una diagonal de 1 px blanca al **4 %** (`M0,6 L6,0 M-1,1 L1,-1 M5,7 L7,5`, trazo 1) más dos puntas para que cierre sin costura. Sin imágenes. Se aplica como segunda capa sobre el relleno de `base`, y las dos capas llevan `Opacity` = alfa del fondo: **la opacidad es solo del fondo y el texto queda al 100 %**.

### 7.3 Estados del panel

Los estados son los de siempre (ver 2 y 5c); cambia el dibujo, no la lógica. Ver `pantallas-fase6.png`, secciones 1 y 1b.

| Estado | Cambio visual |
|---|---|
| Lejos | "PRÓXIMA" como etiqueta de 12 px; "CURVA 7 · 640 m" igual |
| Aproximación / Inminente | Título con acento rojo; **barra segmentada**: segmento 0 blanco = ahora, segmentos hasta el punto de frenado en gris `#7C8794`, el resto vacío `#232A31`, marca roja en el extremo; rombo = IA y círculo = vos como cabezas de 14 px con tallo. Metros chicos abajo (ámbar en Inminente) |
| FRENÁ | Franja roja de 40 px, cuerpo oscuro con leyenda y velocidades. Es la **única superficie roja grande** (≈ 16 % del conjunto: 320 × 40 sobre 320 × 250) |
| TOQUE / LEVANTÁ | Franja neutra, cifra de 64 px |
| SIN DATOS | Mismas 5 causas de la 5c, más "Esperando AMS2" y "Versión no compatible". Los textos que decían "Bandeja ›" ahora dicen "botón ⇄ ›" o "⚙ ›" (ver P26) |
| GRABANDO / SOLO VOS | Cifras de 44 px separadas por ":" con etiqueta roja ("IA · vueltas", "VOS · válidas"). Pie: "Parar de grabar: ⇄ › Mostrar." |

### 7.4 Barra de botones (pedidos C y D)

Orden, de izquierda a derecha: **⇄ modo**, **⚙ ajustes**, **📌 fijar**, separador, **✕ cerrar**. ~~A la izquierda de la fila, la etiqueta FIJO / LIBRE y la tecla Ctrl+Alt+L.~~ **Correcciones 1: sin rótulo ni tecla junto a la barra** (el usuario los marcó para sacar). El estado fijado / libre se ve solo en el ícono del pin y en su tooltip, que conserva el atajo configurado real.

| Botón | Reposo | Acción | Tooltip |
|---|---|---|---|
| ⇄ Modo | Sin borde, ícono `t3` (discreto) | Abre un popover con Grabar todo / Grabar solo vos / Mostrar; marca el actual. **No cicla** | "Modo: Mostrar · clic para cambiar" |
| ⚙ Ajustes | Borde `borde-f`, fondo `sup` | Abre o cierra el menú (alterna) | "Ajustes" |
| 📌 Fijar | Ver abajo | Fija o libera | Ver abajo |
| ✕ Cerrar | Sin borde, ícono `t3`; en hover fondo `rojo` | Doble paso (abajo) | "Cerrar la aplicación" |

**Pin [Criterio]**

| Estado | Ícono | Fondo / borde | Etiqueta | Tooltip |
|---|---|---|---|---|
| Fijado | Chincheta **vertical, rellena** | `#3A1614` / `rojo`, ícono `rojo-et` | *(ninguna)* | "Fijado: no se puede mover ni cambiar de tamaño. Clic para liberar (Ctrl+Alt+L)" |
| Libre | Chincheta **inclinada 45°, solo contorno** | `sup` / `borde-f` | *(ninguna)* | "Libre: arrastrá para mover, rueda para el tamaño. Clic para fijar (Ctrl+Alt+L)" |

La forma (relleno y ángulo) lleva el estado; el color solo acompaña. **Sin texto suelto de estado** (correcciones 1): ni la palabra FIJO / LIBRE, ni la tecla, ni el chip "LIBRE · arrastrá para mover · rueda: tamaño" (antes arriba, al desbloquear). En Libre se mantienen el contorno azul discontinuo y el agarre de tamaño. Mientras está libre el fondo va al 100 %, como antes.

**Qué significa "fijado" [Evidencia, decisión del usuario].** Solo que no se puede mover ni cambiar de tamaño con mouse, rueda o agarre. El engranaje, el pin, el modo y la ✕ funcionan siempre. El resto del overlay deja pasar los clics al juego.

**Área clicable [Criterio].** Cada botón dibuja 28 × 28 y responde en 32 × 32. Razones: se usa con el mouse en pausa o menús, con apuntado impreciso; 32 es del orden de los botones de la barra de título de Windows y supera el mínimo de 24 × 24 de WCAG 2.2 (criterio 2.5.8, de memoria, no se recalculó). Con escala del overlay menor a 0,875 los botones caerían bajo 28 px reales: se les aplica una **escala inversa** (0,875 ÷ escala) para mantener 28 px como mínimo. El separador de 9 px aísla la ✕.

**No estorban la lectura [Criterio].** La barra está **arriba** y el número (cifra de TOQUE, marcadores, barra) está debajo. Los botones sin borde en reposo (⇄ y ✕) reducen el ruido.

**✕ con doble paso [Criterio].** Un clic reemplaza la fila de botones, en el mismo lugar, por "¿Cerrar la app?" con **CERRAR** (rojo) a la izquierda y **CANCELAR** justo donde estaba la ✕, de modo que un doble clic accidental cancela. Se cancela solo a los 5 s (barra fina que se consume) y con Esc si hay foco. Si está grabando, agrega "Estás grabando: se corta la grabación." "Salir" del menú usa el mismo doble paso en su pie. La ✕ cierra la **aplicación** (igual que "Salir"); ocultar solo el overlay está en ⚙.

### 7.5 Menú de configuración (pedido B)

Se abre con el engranaje, en una ventana propia **opaca** (no depende de la opacidad ni del juego), del lado donde haya lugar (arriba o abajo, a la izquierda de la barra si el overlay está contra el borde derecho). Dos columnas, todo visible sin desplazar. Sin submenús flotantes: "Datos" y "Atajo de bloqueo" abren una **página dentro del mismo menú** con "‹" para volver. Los deslizadores aplican en vivo.

| Zona | Grupo | Control | Rango / paso | Se ve |
|---|---|---|---|---|
| Izq. 1 | **Modo** | Segmentado de 3: Grabar todo / Solo vos / Mostrar | — | Ayuda de una línea del modo elegido |
| Izq. 2 | **Referencia principal** | Segmentado de 2: ◆ IA / ● VOS | — | — |
| Izq. 3 | **Overlay** | Interruptor **Fijar** (igual al pin, con la tecla) | — | — |
| | | Deslizador **Tamaño** | 75 a 150 %, paso 5 | "100 %" |
| | | Deslizador **Opacidad del fondo** | **85** a 95 %, paso 1 | "88 %" |
| | | Botón **Ocultar overlay** | — | Ver P23 |
| Der. 1 | **Aviso de frenada** | Interruptor + botón **Probar** | — | — |
| | | Deslizador **Tamaño del aviso** | 50 a 300 %, paso 10 | "100 %" |
| | | Deslizador **Anticipación (pitido y símbolo)** *(antes "Anticipación del aviso")* | **0,0 a 2,0 s, paso 0,1** | "0,8 s" + ayuda (7.6) |
| | | Deslizador **Duración máxima** | 1,0 a 2,0 s, paso 0,1 | "1,5 s" |
| Der. 2 | **Pitido** | Interruptor + botón **Probar** | — | — |
| Der. 3 | **Atajo** | Fila "Atajo de bloqueo ›" | 4 sugerencias; "Otro: editá `atajo` en config.json" | La tecla actual |
| Pie | | **Datos ›** (abrir carpeta de datos, abrir carpeta de capturas, exportar…, importar…), **Diagnóstico…**, **Salir** | — | — |

**Orden [Criterio].** Lo que se cambia más en pausa (modo y referencia) arriba a la izquierda; ajustes del overlay debajo; aviso y pitido a la derecha; lo raro (datos, diagnóstico, salir) en el pie, con Salir lejos de los botones Probar. Controles propios: interruptor con tilde en la perilla (la forma lleva el estado, no solo el color), deslizador con perilla recortada, segmentado con la opción elegida en blanco.

**Valores [Criterio].** La opacidad se muestra como el alfa real (85 a 95 %), no como un porcentaje normalizado. Tamaño y opacidad siguen funcionando con el overlay fijado, porque son acciones deliberadas del menú (la bandeja hoy se comporta así); si el usuario prefiere lo contrario, se deshabilitan con la leyenda "Liberá con 📌 para cambiar el tamaño" (ver P27).

### 7.6 Anticipación del aviso (pedido E)

| Dato | Valor |
|---|---|
| Etiqueta | **Anticipación (pitido y símbolo)** *(correcciones 1: "del aviso" se leía como solo el símbolo; vale para el pitido y el símbolo; igual en el submenú de la bandeja)* |
| Control | Deslizador, 0,0 a 2,0 s, paso 0,1, valor numérico visible ("0,8 s"), marcas cada 0,5 s |
| Defecto | **0,0 s** |
| Ayuda | "Segundos antes del punto de frenado en que suena el pitido y el símbolo llega al 100 %. 0 = justo en el punto." |
| Regla | El pitido suena en `punto − anticipación`. El símbolo llega al 100 % **en ese mismo instante**. La rampa dura **2,0 s fijos** y termina ahí |

Línea de tiempo en `pantallas-fase6.png` (sección 4b). Sigue valiendo todo lo de la 5c (no suena si ya estás frenando, una sola vez por curva, solo la referencia principal). Dos decisiones [Criterio]:

- **La duración máxima se sigue contando desde el punto de frenado** (no desde el pitido). Con anticipación A el símbolo pleno dura A + duración. Ver P33.
- **"Probar aviso"** pasa a mostrar la rampa de 2 s, el pitido (si está activo) al completarse y el símbolo pleno 1,5 s. Así se oye y ve lo que se eligió. Ver P34.

**Para el Agente 1 [Evidencia, `DecisorAviso`, `ConfigApp`].** Hoy `AvisoRampaS` (1 a 3 s, defecto 2,0) es lo que el usuario ve como "Anticipación del aviso" en la bandeja: cuándo **empieza** a verse el símbolo. Reusar esa clave con el significado nuevo cambiaría el comportamiento de quien ya tiene 2,0. Propuesta: clave nueva `avisoAnticipacionS` (defecto 0, 0 a 2,0); la rampa pasa a ser una constante de 2,0 s (la clave vieja se ignora, sin conversión); el decisor compara el tiempo restante contra `anticipación` para pitar y para llevar el progreso a 1. Tests nuevos: A = 0 (igual que hoy), A = 0,8 y A = 2,0, y "ya frenando" justo antes del pitido.

### 7.7 Animaciones [Criterio]

Nada parpadea, pulsa ni se repite. Si Windows tiene las animaciones desactivadas, todas las duraciones valen 0.

| Qué | Duración | Nota |
|---|---|---|
| Panel de guía, entrada / salida | 120 / 200 ms | Sin cambios |
| Paso Aproximación → Inminente | Instantáneo | Sin cambios |
| Franja FRENÁ | Fundido de 80 ms | Sin cambios |
| Símbolo de aviso | Rampa 2,0 s; salida 80 ms | Sin cambios salvo 7.6 |
| Menú: abrir / cerrar | 120 ms (opacidad + 6 px de desplazamiento) / 80 ms | Una vez |
| Página interna del menú | 100 ms, fundido | — |
| Interruptor | 120 ms, la perilla se desliza | — |
| Hover y presionado de botón | 80 ms, color | — |
| Pin: fijar / liberar | 120 ms, rotación 45° a 0° | — |
| Confirmación de cerrar | Fundido de 100 ms; la barra de 5 s se consume lineal | Es un indicador de tiempo, no un pulso |
| Deslizador | Sin animación | Sigue al mouse |

### 7.8 Contraste [Evidencia]

Calculado con `docs/ui/contraste_fase6.py` (WCAG 2.x). El texto va siempre al 100 %; solo el fondo lleva alfa. El peor caso incluye el **pico de la textura** (blanco al 4 % sobre `base`) y fondo del juego **blanco puro**.

**Texto sobre el panel de guía, peor caso (fondo blanco, textura al pico)**

| Texto | Alfa 0,80 | **Alfa 0,85 (mínimo nuevo)** | Alfa 0,88 | Alfa 0,95 |
|---|---|---|---|---|
| Blanco `#FFFFFF` | 9,47 | **11,34** | 12,59 | 15,71 |
| Título `#EEF1F4` | 8,36 | **10,01** | 11,11 | 13,86 |
| Secundario `#DDE2E7` | 7,27 | **8,70** | 9,66 | 12,05 |
| SIN DATOS `#C3CAD1` | 5,72 | **6,85** | 7,61 | 9,49 |
| Apoyo `#A9B2BC` | **4,41 no** | **5,28** | 5,86 | 7,32 |
| Ámbar `#FFC24D` | 5,90 | **7,06** | 7,84 | 9,78 |
| Naranja IA `#FF9A3D` | **4,49 no** | **5,37** | 5,96 | 7,44 |
| Azul vos `#5AA9FF` | **3,86 no** | **4,62** | 5,13 | 6,40 |
| Etiqueta roja `#FF8A80` | **4,15 no** | **4,97** | 5,52 | 6,88 |

**Corrección a la sección 1.2.** Allí el rango del deslizador era 0,80 a 0,95, pero el azul sobre blanco ya daba 4,35 con alfa 0,80 (menor a 4,5). Con la textura baja a 3,86. **El alfa mínimo pasa a 0,85** (`MedidasUi.AlfaMinimo`: 0,80 → 0,85; con alfa 0,85 todo cumple, el azul con 4,62). Para llegar a 4,5 sobre blanco el mínimo por color es: apoyo 0,81, naranja 0,81, rojo etiqueta 0,83, azul 0,85. Sobre negro, cielo y pianos, todo cumple incluso con 0,80.

**Otros pares**

| Par | Contraste | Veredicto |
|---|---|---|
| Blanco sobre franja `#C62F26` (plana, sin textura) | 5,47 | Cumple |
| Blanco sobre franja `#3B4652` | 9,62 | Cumple |
| Opción elegida `#0C0E11` sobre blanco | 19,33 | Cumple |
| Opción no elegida `#DDE2E7` sobre `sup` | 13,40 | Cumple |
| CERRAR: blanco sobre `rojo` / en hover `#D8382E` | 5,47 / 4,64 | Cumple |
| Tilde `rojo` sobre perilla blanca | 5,47 | Cumple |
| Tecla Ctrl+Alt+L `#EEF1F4` sobre `#2A3139` | 11,60 | Cumple |
| Chips MOSTRAR / GRABANDO / LIBRE | 7,90 / 6,94 / 7,26 | Cumplen |
| Texto sobre menú `#0F1216`: blanco / `t3` / `rojo-et` | 18,78 / 8,75 / 8,23 | Cumple |

**Componentes gráficos (mínimo 3:1)**

| Par | Contraste | Veredicto |
|---|---|---|
| Borde de botón `#5A6673` sobre el menú | 3,20 | Cumple |
| Ícono `#DDE2E7` sobre `sup` / hover | 13,40 / 11,38 | Cumple |
| Ícono discreto (⇄, ✕) `#A9B2BC` sobre el panel | 9,00 | Cumple |
| Pin fijado `#FF8A80` sobre `#3A1614` | 7,05 | Cumple |
| Pista del deslizador `#66727F` sobre `sup` | 3,56 | Cumple (la `#33414E` primera daba 1,67 y se descartó) |
| Perilla blanca sobre pista `#66727F` | 4,91 | Cumple |
| Interruptor ON `rojo` sobre `sup` / OFF borde `#7C8794` | 3,19 / 4,79 | Cumple |
| Segmento "ahora" blanco sobre vacío `#232A31` | 14,51 | Cumple |
| Segmento recorrido `#7C8794` sobre vacío / sobre panel | 3,97 / 5,29 | Cumple |
| Rombo naranja / círculo azul sobre el panel | 9,15 / 7,87 | Cumple |
| Acento rojo `#C62F26` sobre el panel | 3,53 | Cumple |
| Segmento vacío `#2B343D` sobre el panel | 1,53 | **No cumple**: decorativo, no lleva información (la posición la dan el segmento blanco, los grises y los marcadores) |
| Símbolo `#E53935` contra su contorno `#0C0E11` / sobre cielo claro sin contorno | 4,57 / 2,87 | Sin cambios: el contorno oscuro es obligatorio |

Daltonismo: sin cambios respecto de 1.3 (naranja y azul distintos por matiz, iso-luminantes por brillo; la forma rombo / círculo y la palabra IA / VOS son la redundancia). El pin y el interruptor no dependen del color (forma y texto FIJO / LIBRE).

### 7.9 Checklist del handover, revalidada

| # | Ítem | Veredicto | Razón |
|---|---|---|---|
| 1 | Se lee en menos de 0,5 s | **Cumple en diseño, sin verificar en AMS2** | La jerarquía no cambió (título, barra, etiquetas). La barra segmentada sigue sin números; la cabecera con botones queda arriba y fuera de la zona de lectura. Cifras de GRABANDO más grandes (44) [Criterio] |
| 2 | Contraste de texto ≥ 4,5:1 | **Cumple con una condición** | Con alfa ≥ 0,85 todo cumple, incluso sobre blanco y con la textura al pico (mín. 4,62, azul). Con 0,80 fallan azul, apoyo, naranja y etiqueta roja: por eso el mínimo sube a 0,85 (7.8) [Evidencia] |
| 3 | Naranja y azul se distinguen con daltonismo y por brillo | **Cumple por matiz y por forma; no por brillo** | Sin cambios: iso-luminantes; rombo / círculo y palabra lo resuelven (1.3) [Evidencia] |
| 4 | Sin parpadeo; rojo no invasivo | **Cumple en diseño, sin verificar en AMS2** | Ninguna animación repetida (7.7). Superficie roja grande solo en FRENÁ (≈ 16 %); el resto es acento de 28 × 3, marca de 2 px, interruptores y CERRAR (< 1 %) [Evidencia aritmética] |
| 5 | Posición por defecto no tapa el HUD | **No verificable** | El conjunto crece ≈ 32 px por la barra (≈ 290 en Aproximación). Posición arrastrable. Ver P29 |
| 6 | Bloquear/desbloquear se entiende y el menú se encuentra | **Cumple en diseño, sin verificar en AMS2** | Pin visible con estado en forma y palabra, atajo visible, engranaje siempre activo; ya no depende de la bandeja. Se usa con el mouse en pausa; con el cursor oculto en carrera no hay forma de usarlo (es esperado) |
| 7 | SIN DATOS y GRABANDO dicen qué hacer | **Cumple** | Los textos apuntan a los botones del overlay (⇄, ⚙) y no a la bandeja; GRABANDO dice cómo parar. Hay que actualizar las cadenas (P26) |

### 7.10 Problemas, con prioridad

| # | Prioridad | Problema | Propuesta |
|---|---|---|---|
| P19 | **Bloquea** | Con el overlay fijado, solo los botones deben recibir clics. `WM_NCHITTEST` con `HTTRANSPARENT` pasa el clic solo a ventanas **del mismo hilo**; AMS2 es otro proceso [Referencia de la API de Win32, sin ejecutar] | **Ventana aparte para los botones** (7.11) |
| P20 | **Bloquea** | Reusar `avisoRampaS` como anticipación cambia el comportamiento de configuraciones existentes (7.6) | Clave nueva `avisoAnticipacionS`; rampa fija 2,0 s; tests |
| P21 | **Bloquea** | Alfa mínimo 0,80 no cumple 4,5:1 sobre blanco (azul 3,86) | `AlfaMinimo` = 0,85 (una constante) |
| P22 | Importante | Con escala < 0,875 los botones bajan de 28 px reales | Escala inversa de la barra (7.4) |
| P23 | Importante | "Ocultar overlay" deja sin botones para volver a mostrarlo (solo bandeja) | Al ocultar, dejar visible solo una pastilla de 32 × 32 con el engranaje que abre el menú con "Mostrar overlay". **Decidir con el usuario** |
| P24 | Importante | Las ventanas sin activación no reciben teclado: Esc y flechas no funcionan [sin verificar] | Menú pensado solo para mouse; cierre con ✕ del menú y el engranaje; no robar el foco de AMS2 |
| P25 | Importante | El menú podría quedar abierto al volver a manejar y tapar la pista | Se cierra solo tras 15 s con el mouse fuera del menú [Criterio, ajustable] |
| P26 | Importante | Seis textos nombran la bandeja (`ConstructorVista`: 3; `ServicioOverlay`: 2 pies de GRABANDO; texto de "Cómo bloquear") | Cambiarlos por "botón ⇄ ›" y "⚙ ›" (ver 7.3); el globo de primer arranque también |
| P27 | Importante | ¿Tamaño y opacidad con el overlay fijado? Se dejó activo (como la bandeja) | Confirmar con el usuario; alternativa en 7.5 |
| P28 | Importante | Un cambio de modo accidental a Grabar puede modificar datos guardados [sin verificar qué se pisa] | Popover de 3 opciones, nunca ciclo; el chip GRABANDO ya es visible |
| P29 | Importante | La barra suma ≈ 32 px de alto (≈ 290 en Aproximación, ≈ 27 % de 1080p) | Verificar con el HUD completo en el .exe |
| P30 | Menor | Textura con escala o DPI no enteros puede dar moiré | Si molesta, relleno plano (constante en código) |
| P31 | Menor | Tooltips en ventanas sin activación pueden no mostrarse [sin verificar] | El estado ya está en la etiqueta FIJO / LIBRE y el atajo en la barra |
| P32 | Menor | Costura entre la ventana de botones y la del overlay si la textura queda desfasada | Mismo pincel con el desfase de la posición de la ventana |
| P33 | Menor | Con anticipación alta el símbolo pleno dura A + duración | Revisar con el usuario si prefiere contar la duración desde el pitido |
| P34 | Menor | "Probar aviso" cambia de significado (7.6) | Confirmar con el usuario |
| P35 | Menor | Menú de 624 px en pantallas de menos de ≈ 800 px de ancho | Caer a una columna con desplazamiento |

Los pendientes P16 (botón de modo) y P17 (✕ / menú sobre el overlay) de la 5b quedan **resueltos en diseño** por 7.4.

### 7.11 Implementación en WPF

**Clics solo en los botones cuando está fijado.**

| Opción | Veredicto | Razón |
|---|---|---|
| A. `WM_NCHITTEST` devolviendo `HTTRANSPARENT` fuera de los botones | **No usar** | `HTTRANSPARENT` solo reenvía a ventanas del mismo hilo; los clics no llegarían a AMS2 [documentación de Win32, sin ejecutar] |
| B. **Ventana chica aparte solo para los botones** | **Recomendada** | La ventana grande conserva tal cual `WS_EX_LAYERED \| TRANSPARENT \| NOACTIVATE \| TOOLWINDOW` cuando está fijada (camino de la 5c, ya compilado). La ventana de botones es otra, sin `TRANSPARENT`, con `NOACTIVATE`. Sin condiciones de carrera ni sondeo |
| C. Alternar `WS_EX_TRANSPARENT` según la posición del cursor (temporizador o gancho) | Último recurso | Depende de un sondeo (≈ 60 Hz); un clic durante el cambio de estilo puede perderse; más piezas móviles |

Detalles de B:

- **Ventana de botones** (`VentanaBarra`): ≈ 140 × 32 con los cuatro botones y el separador; solo ese rectángulo bloquea clics del juego. `Topmost`, `ShowActivated=false`, `WS_EX_NOACTIVATE \| TOOLWINDOW`, **sin** `AllowsTransparency` (es un rectángulo: se dibuja por hardware y la prueba de impacto es trivial), con la ventana del overlay como `Owner` para que quede encima.
- ~~La etiqueta FIJO / LIBRE y la tecla viven en la ventana grande.~~ Ya no existen (correcciones 1). Al confirmar la ✕ la ventana de botones se agranda a la fila completa (292 × 32) durante la pregunta.
- **Posición:** la barra va **arriba** del overlay a propósito: su desfase respecto de la esquina superior de la ventana grande es fijo aunque la tarjeta crezca o se achique, así que la ventana de botones solo se reubica al mover, cambiar el tamaño o cambiar el DPI (acciones explícitas), no en cada cambio de estado. Si la barra estuviera abajo habría que moverla en cada estado y se vería un salto entre las dos ventanas.
- **DPI:** reposicionar con `SetWindowPos` y `SWP_NOACTIVATE`; en monitores con DPI distinto, medir en píxeles físicos [sin verificar].
- **Foco:** `NOACTIVATE` hace que un clic en los botones no le saque el foco a AMS2. A cambio, el teclado no llega al menú (P24). Quitar `NOACTIVATE` para tener teclado robaría el foco del juego: no recomendado.

**Menú con deslizadores.**

- **Recomendación: una ventana propia** (`WindowStyle=None`, `AllowsTransparency` para el corte de esquina, `Topmost`, `ShowActivated=false`, sin barra de tareas, `Owner` = ventana de botones), no un `Popup` ni un `ContextMenu`.
- **Por qué no `ContextMenu`:** cierra con cualquier clic fuera, captura el mouse y el teclado, y un `Slider` dentro de un `MenuItem` pelea con esa captura al arrastrar la perilla. **Por qué no `Popup`:** se cierra al perder la activación (que aquí nunca la tiene), tiene problemas conocidos de orden en Z con ventanas `Topmost` y de reposicionamiento con DPI [sin verificar]. Con una ventana propia el cierre es explícito (✕ del menú, engranaje, 15 s fuera del menú, o al ocultar el overlay).
- **Posición:** se calcula al abrir y no se mueve mientras está abierto. Cambiar el tamaño del overlay con el deslizador mueve la barra (y el engranaje) pero no el menú.
- **Deslizadores:** `IsMoveToPointEnabled=True` (clic en la pista salta), `IsSnapToTickEnabled` con el paso de 7.5. Aplicar en vivo y **guardar `config.json` 500 ms después del último cambio**, no en cada movimiento.
- **Páginas internas** ("Datos", "Atajo") en lugar de submenús: se cambia el contenido de la misma ventana; evita ventanas anidadas.

**Rendimiento de la textura.**

- Un solo `DrawingBrush` **congelado** (`Freeze()`) compartido por todas las tarjetas y el menú; `RenderOptions.CachingHint = Cache`.
- Alfa en el `Opacity` del **pincel**, no en el elemento: `Opacity` en un contenedor fuerza render fuera de pantalla (y atenuaría el texto, contra la regla).
- Esquina cortada con un `Path` / `StreamGeometry` de relleno, no con `Clip`. Sin `DropShadowEffect` ni otros efectos de mapa de bits.
- Las ventanas con `AllowsTransparency` se componen por software: el costo es área × frecuencia de repintado. El overlay se actualiza a ≤ 10 Hz salvo el aviso (ventana chica, 60 Hz, como hoy). La textura es estática, así que no suma costo por cuadro **[estimado, sin medir en WPF ni en AMS2]**.

### 7.12 Supuestos y sin verificar

- **Sin verificar en AMS2:** todo el comportamiento de ventanas (clics, foco, orden en Z, DPI), la lectura en 0,5 s, el alto con el HUD completo y la textura a escala real. Las pantallas son un dibujo en HTML, no capturas del .exe.
- **Referencias del usuario:** solo se miraron localmente; el parecido es de estilo.
- **Fondo peor caso:** blanco puro; textura al pico del 4 %.
- **Documentación de Win32** (`HTTRANSPARENT` entre procesos, `NOACTIVATE`): recordada, no probada aquí.
- **WCAG 2.5.8 (24 px):** de memoria, solo para justificar los 28 / 32 px.
- **Alturas y anchos del menú y del conjunto:** estimados sobre la ilustración, no medidos en WPF.
- **Nombre de la clave nueva** (`avisoAnticipacionS`), el autocierre de 15 s, el desplazamiento de 6 px y las duraciones: propuestas.
- **"Ancho" de los títulos** aproximado con espaciado: no hay fuente ancha embebida.
- **Atajo `Ctrl+Alt+L`:** sin comprobar choques con Moza o AMS2 (igual que antes).

### 7.13 Implementación (Agente 1, rama `fase-6-rediseno`) [compila, sin ejecutar]

Lo que se hizo distinto o además de lo dibujado:

- **Rampa del símbolo.** No se fijó en 2,0 s: se conserva `avisoRampaS` de la 5d (1,0 a 3,0 s, defecto 2,0) como deslizador "Aparición del símbolo" (correcciones 1: antes "Inicio de la rampa (avanzado)"; sin "avanzado" y sin la palabra "rampa" en ningún texto visible) en la columna del aviso, con la ayuda "Cuántos segundos antes empieza a verse el símbolo, de a poco. Por defecto 2,0 s.". Por eso el menú mide unos 50 px más que lo dibujado. `avisoAnticipacionS` se mantiene. Decisión a confirmar con el usuario.
- **Escala inversa (7.4).** Debajo de 0,875 de escala del overlay la barra se queda en 0,875: se garantiza el área clicable de 28 px reales (32 × 0,875); el dibujo de 28 puede bajar a 24,5.
- **Popover de modo.** Es la misma ventana de botones, que crece hacia abajo (220 de ancho), no una ventana aparte.
- **Pastilla (P23).** Con el overlay oculto la ventana grande sigue abierta con el contenido colapsado (es el `Owner` de la barra) y queda la pastilla de 32 × 32 con el engranaje, donde estaba el hueco de la barra; el menú ofrece "Mostrar overlay".
- **Posición de la barra.** Se recalcula en cada tick a partir del hueco `ReservaBarra` de la cabecera (no hay eventos de movimiento que se puedan perder). Con el chip LIBRE quitado (correcciones 1) la cabecera ya no crece al liberar.
- **Espaciado de títulos.** WPF no tiene espaciado de letras: se aproxima con *hair spaces* (U+200A) en FRENÁ, TOQUE, LEVANTÁ, SIN DATOS y los títulos del menú. Sin verificar cómo lo resuelven Barlow Condensed y el respaldo.
- **Colores.** Solo los tokens de 7.2; el contraste se recalculó con `docs/ui/contraste_fase6.py` (sin cambios). Presionado del botón rojo (`#A82720`) es más oscuro que el rojo base, con texto blanco.
- **Contorno azul del modo libre.** Rectangular (sin la esquina cortada).

### 7.14 Correcciones de errores 1 (rama `correcciones-ui-1`) [compila, sin ejecutar]

Cambios pedidos por el usuario tras probar el .exe de la fase 6 (todos aprobados):

| # | Cambio | Dónde |
|---|---|---|
| 1 | Se quitan: el rótulo "FIJO / LIBRE" con la tecla junto a la barra, el chip "LIBRE · arrastrá para mover · rueda: tamaño" y el "(avanzado)" del deslizador. El estado se lee en el pin (relleno o contorno, color, inclinación) y en su tooltip ("Fijado: … (Ctrl+Alt+L)" / "Libre: … (Ctrl+Alt+L)"). Se mantiene el contorno discontinuo azul y el agarre. | 7.4 |
| 2 | "Anticipación del aviso" pasa a **"Anticipación (pitido y símbolo)"** en el menú y en la bandeja. Ayuda: "Segundos antes del punto de frenado en que suena el pitido y el símbolo llega al 100 %. 0 = justo en el punto." | 7.5, 7.6 |
| 3 | "Inicio de la rampa (avanzado)" pasa a **"Aparición del símbolo"** (bandeja: igual). Ayuda: "Cuántos segundos antes empieza a verse el símbolo, de a poco. Por defecto 2,0 s.". La clave interna `avisoRampaS` no cambia. | 7.5 |
| 4 | El chip del modo (MOSTRAR / GRABANDO / GRABANDO · SOLO VOS) sigue siempre el modo configurado, con o sin AMS2 (también en "esperando AMS2", "versión no compatible" y "entrá a pista"). | cabecera (5b) |
| 5 | La ventana de ajustes se mueve arrastrando su **cabecera** (título y zona vacía, también en Datos y Atajo). Cursor `SizeAll` en la cabecera; los botones ✕ y ‹ siguen siendo botones. Queda siempre dentro del área de trabajo del monitor bajo el cursor. | 7.5 |
| 6 | Ícono propio: cuadrado rojo `#C62F26` de esquinas redondeadas (radio 48 de 256), "2" grande blanco y cinta oscura `#0C0E11` con borde blanco y "AF" (Barlow Condensed Bold a trazos). Fuente: `src/AMS2Frenado.App/Recursos/icono.svg`. | nuevo |

**Ventana de ajustes movible.** La ventana es NOACTIVATE (nunca toma foco): `DragMove` y `WM_NCLBUTTONDOWN`/`HTCAPTION` arrancan el bucle modal de movimiento del sistema, poco fiable sobre una ventana sin activar y con `AllowsTransparency`. Se hace a mano: captura de mouse WPF en la cabecera, posición del cursor con `GetCursorPos` (absoluta, para que mover la ventana no realimente el cálculo) y `SetWindowPos` con `SWP_NOACTIVATE`. Límites: la ventana entera dentro del área de trabajo del monitor donde está el cursor (se puede pasar de un monitor a otro). La última posición se recuerda durante la sesión y se guarda en `config.json` (`ajustesPosicion`, DIP de pantalla, `null` = automática junto a la barra); se lleva dentro del monitor al usarla. El cálculo automático (al abrir y la corrección con el alto real) solo corre si el usuario no eligió posición; un clic en la cabecera sin mover no cuenta como elegir. Si al cambiar de página el menú crece y se sale de pantalla, se lo sube lo justo. Lógica en `ArrastreVentana` y `PosicionAjustes` (Core, con pruebas).

**Ícono.** Se usa en: `ApplicationIcon` del .exe (7 tamaños: 16, 24, 32, 48, 64, 128, 256, PNG dentro del ICO), `Icon` de la ventana de diagnóstico y la bandeja. El menú, el overlay, la barra y el aviso no tienen barra de título ni botón en la barra de tareas: no necesitan ícono. **Bandeja:** se conserva la idea candado cerrado / abierto como un indicador de 16 px (disco oscuro con candado minimo) abajo a la derecha del ícono; a 16 px reales (100 % de escala) se lee como un punto, por eso el estado también está en el tooltip del ícono y en el pin. Tamaños chicos: 16, 24 y 32 usan solo el "2" (AF se omite: a 32 px mediría ~5 px); 48 y 64 llevan una cinta más alta con AF más grande; 128 y 256, el diseño completo. Vista previa: `docs/ui/icono-vista-previa.png`. Reproducir: `python3 tools/icono/generar_icono.py` (fonttools, Pillow, Node + playwright). No copia ningún logo de terceros (AMS2, Reiza, Content Manager): solo comparte el concepto genérico "cuadrado rojo con número grande y etiqueta chica".
