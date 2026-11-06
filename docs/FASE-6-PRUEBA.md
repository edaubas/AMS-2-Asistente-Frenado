# Fase 6: cómo probar el rediseño en Windows

> **Compila, sin ejecutar.** Nada de esto se vio nunca en Windows ni con AMS2. Esta lista es lo que hay que mirar primero; anotá lo que no se vea como dice. Para publicar: `dotnet publish src/AMS2Frenado.App -r win-x64 --self-contained -p:PublishSingleFile=true`. Spec: `docs/UI-SPEC.md`, sección 7.

Con AMS2 en pausa o en un menú (el cursor del juego suele estar oculto en carrera: los botones son para usar con el mouse en pausa).

1. **Textura legible.** Mirá una tarjeta (por ejemplo SIN DATOS) sobre el juego, con la opacidad en 85 % y en 95 %: la textura de carbono debe notarse apenas, sin moiré, y el texto debe leerse sin esfuerzo (también sobre un fondo claro: cielo, pianos). Esquina cortada abajo a la derecha. Probá a 125 % de escala de Windows.
2. **Engranaje.** Clic en ⚙: abre el menú (624 px, dos columnas). Clic de nuevo en ⚙ o en ✕ del menú: se cierra. Con el mouse afuera 15 s se cierra solo. No debe sacarle el foco a AMS2.
3. **Pin (fijar).** Chincheta vertical y rellena = fijado; inclinada y de contorno = libre (aparecen el contorno azul y el agarre). **No hay rótulo FIJO / LIBRE, ni tecla, ni chip de texto** (se sacaron en las correcciones 1); el estado se lee en el pin y en su tooltip. `Ctrl+Alt+L` hace lo mismo y el estado se ve igual.
4. **Bloqueado con botones activos.** Con el overlay fijado: los clics sobre la tarjeta **atraviesan** hasta el juego (probá clicar sobre el panel: el juego debe recibirlo), pero ⇄, ⚙, chincheta y ✕ **responden**. Liberado: arrastrar mueve; la barra de botones debe seguir al overlay sin saltos (si se ve una costura o un desfase, anotar cuánto).
5. **✕ en dos pasos.** Un clic en ✕: la fila cambia a "¿CERRAR LA APP?" con CERRAR (rojo) y CANCELAR justo donde estaba la ✕. Doble clic rápido en la ✕: debe **cancelar**, no cerrar. Sin tocar nada se cancela solo a los 5 s (barra fina que se consume). Grabando, avisa "Estás grabando: se corta la grabación.". "Salir" del menú hace lo mismo en su pie.
6. **Modo.** Clic en ⇄: popover con Grabar todo / Grabar solo vos / Mostrar, el actual marcado. Un clic **solo abre**; no cambia nada hasta elegir. Elegir Grabar muestra el chip GRABANDO y los contadores con ":" (cifras de 44).
7. **Sliders.** En el menú: Tamaño (75–150 %), Opacidad (85–95 %), Tamaño del aviso (50–300 %), Anticipación (pitido y símbolo) (0,0–2,0 s), Duración (1,0–2,0 s), Aparición del símbolo (1,0–3,0 s). Aplican en vivo, con coma decimal. Cerrá la app y volvé a abrirla: los valores deben quedar (`config.json` se guarda 500 ms después del último cambio). Con el overlay **fijado**, tamaño y opacidad siguen funcionando (decisión; ver abajo).
8. **Ocultar y volver.** En el menú, "Ocultar overlay": desaparece todo salvo una pastilla de 32×32 con el engranaje, en el mismo lugar. Clic en la pastilla → el menú ofrece "Mostrar overlay". También por la bandeja.
9. **FRENÁ.** La franja roja de 40 px sigue siendo lo único rojo grande; sin parpadeo ni pulsos. Texto de la franja con el espaciado nuevo (aproximado con *hair spaces*; si se ve raro o con huecos, anotar).
10. **Aviso.** ⚙ › Aviso › PROBAR: rampa, pitido (si está activo) y símbolo pleno. Anticipación 0,8 s: el pitido suena 0,8 s antes del punto de frenado y el símbolo llega al 100 % en ese instante (como en 5d; ver `docs/FASE-5-PRUEBA.md`).
11. **Textos.** SIN DATOS y GRABANDO ya no nombran la bandeja: dicen "botón ⇄ › Grabar todo", "⚙ › Datos" y "Parar de grabar: ⇄ › Mostrar.".
12. **Bandeja (respaldo).** El menú del ícono sigue teniendo todas las opciones; marcá si algo difiere del menú del overlay.

## Decisiones tomadas (para confirmar)

- Los botones son una ventana aparte; la ventana grande sigue click-through. Menú propio, no `Popup`/`ContextMenu`.
- Alfa mínimo 0,85 (rango 0,85–0,95).
- Escala inversa de la barra: debajo de 0,875 de escala del overlay se queda en 0,875. Lo que se garantiza es el **área clicable** (32 × 0,875 = 28 px reales); el dibujo de 28 puede quedar en 24,5 px.
- Ocultar overlay deja una pastilla con el engranaje (default razonable, P23).
- Tamaño y opacidad activos con el overlay fijado (P27). "Probar aviso" muestra rampa, pitido y símbolo pleno (P34).
- Aparición del símbolo (`avisoRampaS`, de la 5d) conservada (por defecto 2,0 s), sin rótulo "avanzado": el spec no la dibujaba; el menú crece unos 50 px respecto del dibujo (≈ 590 de alto, no 540).
- Popover de modo: es la misma ventana de botones, que crece hacia abajo (no una ventana más).

## Correcciones 1 (rama `correcciones-ui-1`): qué volver a probar

> **Compila, sin ejecutar**: nada de esto se vio en Windows. Detalle y decisiones en `docs/UI-SPEC.md` 7.14.

1. **Sin rótulos de estado.** Fijar y liberar con la chincheta y con `Ctrl+Alt+L`: no debe quedar ningún texto suelto (ni "FIJO"/"LIBRE", ni la tecla, ni el banner azul "LIBRE · arrastrá para mover…"). Liberado: contorno discontinuo azul y agarre. El tooltip del pin dice "Fijado: …" / "Libre: …" con el atajo. El slider ya no dice "(avanzado)".
2. **Anticipación (pitido y símbolo).** Se llama así en ⚙ y en la bandeja; la ayuda dice "Segundos antes del punto de frenado en que suena el pitido y el símbolo llega al 100 %. 0 = justo en el punto." Con 0,8 s: pitido y símbolo al 100 % 0,8 s antes del punto.
3. **Aparición del símbolo.** Ya no aparece la palabra "rampa" en ningún texto (⚙ ni bandeja: "Aparición del símbolo", 1,0 a 3,0 s). Ayuda: "Cuántos segundos antes empieza a verse el símbolo, de a poco. Por defecto 2,0 s."
4. **Chip de modo sin juego.** Con AMS2 cerrado (panel "ESPERANDO AMS2"), con el juego en menú ("Esperando una sesión en pista") y en pista: ⇄ o ⚙ › Grabar todo → el chip dice GRABANDO al instante; Solo vos → GRABANDO · SOLO VOS; Mostrar → MOSTRAR. Causa: el chip se armaba solo con una lectura válida del juego.
5. **Mover los ajustes.** Abrí ⚙ y arrastrá desde el título "AJUSTES" o desde la zona vacía de la cabecera (el cursor cambia a cruz de cuatro flechas): la ventana debe seguir al mouse sin saltos y sin sacarle el foco al juego. No debe poder salir de la pantalla (probá los cuatro bordes y, si tenés dos monitores, pasarla al otro). Los botones ✕ y ‹ siguen funcionando (un clic no mueve). Cerrá y volvé a abrir: abre donde la dejaste; cerrá la app y volvé a abrirla: también (`ajustesPosicion` en `config.json`; para volver a la posición automática, borrá esa línea). Al pasar a "Datos" o "Atajo" no debe saltar a la posición automática.
6. **Ícono.** El .exe (Explorador, barra de tareas, Alt-Tab), la ventana de Diagnóstico y la bandeja muestran el cuadrado rojo con "2" (y "AF" en 48 px o más). En la bandeja, indicador chico abajo a la derecha: candado cerrado = fijado, abierto = libre (a 100 % de escala se ve apenas como un punto; el estado está también en el tooltip del ícono). Mirá si el "2" se distingue en 16 y 24 px y si el indicador molesta; anotá si preferís otra cosa. En el .exe de salida el ícono quedó embebido (7 tamaños, verificado en el PE; **sin verificar en Windows**).

Decisiones: arrastre manual (captura de mouse + `GetCursorPos` + `SetWindowPos` con `SWP_NOACTIVATE`) en vez de `DragMove`; posición de ajustes guardada en `config.json`; "AF" omitido a 16, 24 y 32 px; el menú, el overlay, la barra y el aviso no llevan `Icon` (sin barra de título ni botón en la barra de tareas).
