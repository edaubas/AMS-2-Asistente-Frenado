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

## Otros pendientes ya anotados

- Botón en el HUD para cambiar de modo (secundario).
- "✕" o menú contextual sobre el overlay desbloqueado para cerrarlo.
- Ajustes de la rama `fase-5c-ajustes` (en curso): SIN DATOS con causa, pitido y símbolo según la referencia principal, rampa del símbolo y modo "Grabar solo vos".
