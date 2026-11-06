# Fase 6: cómo probar el rediseño en Windows

> **Compila, sin ejecutar.** Nada de esto se vio nunca en Windows ni con AMS2. Esta lista es lo que hay que mirar primero; anotá lo que no se vea como dice. Para publicar: `dotnet publish src/AMS2Frenado.App -r win-x64 --self-contained -p:PublishSingleFile=true`. Spec: `docs/UI-SPEC.md`, sección 7.

Con AMS2 en pausa o en un menú (el cursor del juego suele estar oculto en carrera: los botones son para usar con el mouse en pausa).

1. **Textura legible.** Mirá una tarjeta (por ejemplo SIN DATOS) sobre el juego, con la opacidad en 85 % y en 95 %: la textura de carbono debe notarse apenas, sin moiré, y el texto debe leerse sin esfuerzo (también sobre un fondo claro: cielo, pianos). Esquina cortada abajo a la derecha. Probá a 125 % de escala de Windows.
2. **Engranaje.** Clic en ⚙: abre el menú (624 px, dos columnas). Clic de nuevo en ⚙ o en ✕ del menú: se cierra. Con el mouse afuera 15 s se cierra solo. No debe sacarle el foco a AMS2.
3. **Pin (fijar).** Chincheta vertical rellena + "FIJO" = fijado; inclinada y de contorno + "LIBRE" = libre (aparece el chip "LIBRE · arrastrá para mover · rueda: tamaño", el contorno azul y el agarre). `Ctrl+Alt+L` hace lo mismo y el estado se ve igual.
4. **Bloqueado con botones activos.** Con el overlay fijado: los clics sobre la tarjeta **atraviesan** hasta el juego (probá clicar sobre el panel: el juego debe recibirlo), pero ⇄, ⚙, chincheta y ✕ **responden**. Liberado: arrastrar mueve; la barra de botones debe seguir al overlay sin saltos (si se ve una costura o un desfase, anotar cuánto).
5. **✕ en dos pasos.** Un clic en ✕: la fila cambia a "¿CERRAR LA APP?" con CERRAR (rojo) y CANCELAR justo donde estaba la ✕. Doble clic rápido en la ✕: debe **cancelar**, no cerrar. Sin tocar nada se cancela solo a los 5 s (barra fina que se consume). Grabando, avisa "Estás grabando: se corta la grabación.". "Salir" del menú hace lo mismo en su pie.
6. **Modo.** Clic en ⇄: popover con Grabar todo / Grabar solo vos / Mostrar, el actual marcado. Un clic **solo abre**; no cambia nada hasta elegir. Elegir Grabar muestra el chip GRABANDO y los contadores con ":" (cifras de 44).
7. **Sliders.** En el menú: Tamaño (75–150 %), Opacidad (85–95 %), Tamaño del aviso (50–300 %), Anticipación (0,0–2,0 s), Duración (1,0–2,0 s), Inicio de la rampa (1,0–3,0 s, avanzado). Aplican en vivo, con coma decimal. Cerrá la app y volvé a abrirla: los valores deben quedar (`config.json` se guarda 500 ms después del último cambio). Con el overlay **fijado**, tamaño y opacidad siguen funcionando (decisión; ver abajo).
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
- Rampa configurable de la 5d conservada (por defecto 2,0 s), como opción avanzada del menú: el spec no la dibujaba; el menú crece unos 50 px respecto del dibujo (≈ 590 de alto, no 540).
- Popover de modo: es la misma ventana de botones, que crece hacia abajo (no una ventana más).
