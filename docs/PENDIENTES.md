# Pendientes

Lista viva de cosas anotadas por el usuario que todavía no se implementaron.

## Rediseño visual de la app (estilo GT World Challenge)

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

## Menú de configuración dentro del overlay (engranaje)

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

- Botón en el HUD para cambiar de modo (secundario).
- "✕" o menú contextual sobre el overlay desbloqueado para cerrarlo.
- Ajustes de la rama `fase-5c-ajustes` (en curso): SIN DATOS con causa, pitido y símbolo según la referencia principal, rampa del símbolo y modo "Grabar solo vos".
