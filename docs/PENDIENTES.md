# Pendientes

Lista viva de cosas anotadas por el usuario.

> **Fase 6 (rama `fase-6-rediseno`):** los pendientes marcados **[RESUELTO en la fase 6]** están implementados y **compilan, sin ejecutar**: falta probarlos en Windows con `docs/FASE-6-PRUEBA.md`.

## Rediseño visual de la app (estilo GT World Challenge) — [RESUELTO en la fase 6]

> Implementado: tarjetas de carbono, esquina cortada, acento rojo, títulos condensados, barra segmentada, cifras con ":" (ver README, "Fase 6"). Pendiente: probarlo en Windows y que el Agente 2 revise sobre el resultado real.

- **Pedido:** cambiar el diseño de la app y acercarlo a la estética de GT World Challenge. Anotado el 6 de noviembre de 2026.
- **Referencia:** dos capturas de la web/app de GT World Challenge Europe (página de un evento con cuenta regresiva y calendario). No se suben al repo: son material de terceros. Solo sirven de referencia visual.
- **Rasgos que se ven en las capturas** (observación mía, no un requisito aprobado):
  - Paneles oscuros con textura tipo fibra de carbono y una esquina cortada en diagonal.
  - Acento rojo para bloques de fecha, subrayados y etiquetas chicas.
  - Títulos en blanco, mayúsculas y tipografía condensada ancha.
  - Cifras grandes separadas por ":" con etiquetas chicas rojas debajo (estilo cuenta regresiva).
  - Indicadores segmentados (barras cortas, una activa en blanco).
- **Supuestos:**
  - "La app" es el overlay y el menú. No hay otra pantalla.
  - Se toma el estilo, no se copian logos, nombres ni imágenes de terceros.
- **A definir antes de hacerlo:**
  - Qué partes del overlay cambian (panel de guía, chips, barra, aviso de frenada).
  - Que se mantengan la legibilidad y el contraste de la spec (`docs/UI-SPEC.md`, mínimo 4,5:1) y el rojo de FRENÁ.
  - Pasa primero por el Agente 2 (UI/UX), que actualiza la spec.

## Menú de configuración dentro del overlay (engranaje) — [RESUELTO en la fase 6]

> Implementado: engranaje con menú propio de 624 px con todas las opciones de la bandeja, pin que fija y libera (bloqueado = solo no mover ni cambiar el tamaño), ✕ con doble paso, botón de modo con popover y sliders enlazados a la configuración. Los clics con el overlay fijado se resolvieron con una ventana aparte solo para los botones (`VentanaBarra`); no se usa `WM_NCHITTEST`. La bandeja queda como respaldo.

- **Pedido:** que todo el menú de la bandeja sea accesible desde el overlay mismo, sin ir al ícono de la bandeja de Windows, que es incómodo. Anotado el 6 de noviembre de 2026.
- **Cómo:** un ícono de engranaje (el típico de configuración) en el overlay, que abre un menú con las mismas opciones, con el diseño del rediseño visual de arriba.
- **Opciones a replicar** (las del menú actual): bloquear overlay, modo, referencia principal, tamaño del overlay, opacidad del fondo, aviso de frenada (activar, probar, tamaño, duración máxima), pitido (activar, probar), atajo de bloqueo, ocultar overlay, datos (abrir carpetas, exportar, importar), diagnóstico y salir.
- **Decisiones del usuario (6 de noviembre de 2026):**
  - "Bloqueado" significa solo que no se puede mover ni cambiar de tamaño. El engranaje tiene que poder usarse SIEMPRE, también bloqueado.
  - Junto al engranaje va un botón con el ícono típico de "fijar" (chincheta) que bloquea y desbloquea. El atajo `Ctrl+Alt+L` se mantiene.
- **Consecuencia técnica a resolver:** hasta ahora, bloqueado = los clics atraviesan toda la ventana. Con esta decisión, solo los botones (engranaje, fijar, y los que se sumen) reciben clics, y el resto del overlay sigue dejando pasar los clics al juego. Opciones: respuesta a `WM_NCHITTEST` que devuelve "transparente" fuera de los botones, o una ventana chica aparte para los botones. Lo decide el desarrollador y lo documenta.
- **Supuestos:**
  - Con AMS2 en pantalla, el cursor suele estar oculto. Los botones sirven sobre todo en pausa o en menús.
  - La bandeja se mantiene como respaldo.
- **Se hace junto con:** el "✕" para cerrar, el botón de modo y el rediseño estilo GT World Challenge (ver arriba).

## Plan acordado

Cuando terminen los ajustes de `fase-5c-ajustes` y se corrijan sus errores, se aplican todos los pendientes de este documento en una tanda:

1. Agente 2 (UI/UX): actualiza `docs/UI-SPEC.md` con el rediseño, el engranaje con menú, el botón de fijar, el "✕" y el botón de modo.
2. Agente 1: lo implementa, en una rama nueva.
3. El usuario prueba el `.exe` y el Agente 2 revisa sobre el resultado real.

## Otros pendientes ya anotados

- ~~Botón en el HUD para cambiar de modo (secundario).~~ **[RESUELTO en la fase 6]**: botón ⇄ con popover de 3 opciones.
- ~~"✕" o menú contextual sobre el overlay desbloqueado para cerrarlo.~~ **[RESUELTO en la fase 6]**: ✕ con doble paso (funciona también fijado).
- ~~Ajustes de la rama `fase-5c-ajustes`~~ (SIN DATOS con causa, pitido y símbolo según la referencia principal, rampa del símbolo y modo "Grabar solo vos"): **hechos en 5c/5d**.
- ~~Deslizador de la anticipación del aviso~~ (`avisoAnticipacionS`, 0 a 2,0 s) **[RESUELTO en la fase 6]**: está en el menú del engranaje.

## Quedan abiertos tras la fase 6

- Probar todo en Windows con AMS2 (`docs/FASE-6-PRUEBA.md`): clics con el overlay fijado, orden en Z de las tres ventanas, foco, DPI, textura sin moiré, lectura en 0,5 s y alto del conjunto con el HUD completo (P29).
- Confirmar con el usuario: pastilla al ocultar el overlay (P23), tamaño y opacidad activos con el overlay fijado (P27), "Probar aviso" con rampa + pitido + símbolo (P34), duración contada desde el punto y no desde el pitido (P33).
- Aparición del símbolo (`avisoRampaS`): el spec proponía fijarla en 2,0 s; se conservó configurable (1,0 a 3,0 s) y ya no se rotula "avanzada". Decidir si se quita.
- Correcciones 1 sin ejecutar en Windows: ventana de ajustes movible, ícono (exe, diagnóstico, bandeja) y chip de modo sin juego (`docs/FASE-6-PRUEBA.md`, sección "Correcciones 1").
- Tooltips y teclado (Esc) en ventanas sin activación: pueden no funcionar (P24, P31).
- Release por tag.

## Para revisar antes del próximo build (anotado el 6 de noviembre de 2026)

Solo anotado: el usuario va a hacer pruebas y avisa antes de implementar.

### Bug: el overlay desaparece al abrir la app y vuelve a aparecer solo

- **Síntoma:** al abrir `AMS2Frenado.exe`, el overlay desaparece, pero el proceso sigue corriendo (se ve en el Administrador de tareas, con 0 % de CPU y ~0,2 MB de memoria) y al rato el overlay vuelve a aparecer.
- **Estado:** sin investigar. No se sabe todavía si pasa con AMS2 cerrado, abierto o en pausa, ni cuánto tarda en volver.
- **Hipótesis a verificar** (ninguna comprobada, no se puede ejecutar WPF desde el entorno de desarrollo):
  - La ventana pierde el orden "siempre arriba" frente a AMS2 en Borderless y vuelve cuando el juego cede el foco.
  - El overlay arranca oculto o colapsado hasta la primera lectura válida o hasta el primer tick de reposicionamiento (la ventana grande conserva el contenido colapsado cuando se oculta, y la barra de botones se reposiciona en cada tick).
  - Una posición guardada en `config.json` que queda fuera de pantalla hasta que se corrige.
  - Un `mutex` de instancia única que deja una instancia vieja sin ventana visible.
- **Qué pedir al usuario para acotarlo:** si ocurre con AMS2 cerrado, cuánto tarda en volver, si lo vuelve a mostrar al tocar el ícono de la bandeja, y el contenido de `config.json`.

### Cambio de menú: orden y nombre de la anticipación

- En el menú de Ajustes, dentro de "Aviso de frenada", el control de anticipación va **debajo del título "Aviso de frenada" y arriba de "Símbolo"**.
- La etiqueta pasa a ser solamente **"Anticipación de aviso"**, sin la aclaración entre paréntesis. La ayuda de abajo ("Segundos antes del punto de frenado en que suena el pitido y el símbolo llega al 100 %. 0 = justo en el punto.") se mantiene.
- Mismo cambio de nombre en el submenú de la bandeja.

### Cambio de diseño: textura de fondo de carbono forjado

- **Pedido:** usar como fondo de la app una textura de este estilo: **carbono forjado** (fibra de carbono picada: astillas rectangulares de distintos tamaños, giradas al azar, en grises muy oscuros con vetas finas más claras). El usuario pasó una foto de referencia. Hoy el fondo usa una diagonal fina al 4 % (tipo carbono tejido).
- **La foto no se sube al repo** (material de terceros). Solo sirve de referencia.
- **Supuestos** (sin confirmar):
  - "Fondo de la app" = los fondos de los paneles del overlay y del menú de Ajustes, no el escritorio ni la ventana del símbolo.
  - Se genera una textura propia, no se usa la foto.
- **A resolver al implementarlo:**
  - Cómo generarla barata en WPF: una baldosa repetible hecha con vectores (astillas como rectángulos girados, con un patrón fijo generado por semilla) o una imagen pequeña propia embebida. Debe repetirse sin costuras visibles.
  - Que no baje la legibilidad: el texto y los marcadores deben seguir cumpliendo contraste de 4,5:1 con el pico más claro de la textura (se revalida con `docs/ui/contraste_fase6.py`). Si hace falta, la textura va con menos contraste que la foto.
  - Que no genere "moiré" ni consumo de CPU notable al redibujar a 60 Hz.
  - Pasa por el Agente 2 (UI/UX) para actualizar la spec si cambia alguna medida o color.
