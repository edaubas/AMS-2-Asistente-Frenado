# Fase 5: cómo probar el .exe con AMS2

Todo lo de la ventana, la bandeja, el atajo, el click-through y las fuentes **compila pero nunca se ejecutó** (se desarrolló en Linux). Esta lista es para probarlo en Windows con AMS2. Anotá qué falla y con qué pantalla.

**Antes:** `Options › System › Shared Memory = Project CARS 2`, `UDP Frequency = OFF`, `Graphics › Window Mode = Borderless`. El `.exe` sale del artifact `AMS2Frenado-win-x64` de GitHub Actions (rama `fase-5-overlay`). Para empezar de cero, borrá `%APPDATA%\AMS2AsistenteFrenado\config.json`.

## 1. Arranque y bandeja

1. Ejecutá `AMS2Frenado.exe`. Debe aparecer un globo con el aviso de que el overlay está bloqueado, dónde está el ícono y el atajo. Mirar: si el globo se ve, y si el ícono (candado naranja sobre círculo oscuro) está en la bandeja o en la flecha `^` de Windows 11.
2. Sin AMS2 abierto el overlay dice **ESPERANDO AMS2** y cómo activar la memoria compartida.
3. Clic derecho en el ícono: menú con Bloquear overlay, Modo, Referencia principal, Posición, Tamaño, Opacidad del fondo, Mostrar mapa, Mapa compacto, Rojo suave, Pitido, Atajo de bloqueo, Ocultar/Mostrar, Abrir carpeta de datos, Diagnóstico, Salir. Mirar: que cada marca refleje el estado.
4. Abrí **Diagnóstico…** y comprobá que sigue leyendo AMS2 como en la fase 2.
5. Abrí otra copia del `.exe`: debe avisar que ya está en ejecución.

## 2. Grabar

1. Con AMS2 en pista (cualquier clase con IA), bandeja › **Modo › Grabar**. El mapa muestra el chip rojo **GRABANDO** y, mientras no haya guía, los contadores IA / VOS / MAPA con el texto "Parar de grabar: Bandeja › Modo › Mostrar".
2. Da unas 10 vueltas limpias (sin boxes, sin invalidar). Mirar: que los contadores suban; que aparezca el trazado en el mini mapa.
3. Bandeja › **Modo › Mostrar** (esto guarda). Abrí **Abrir carpeta de datos**: debe haber `<pista>\<variante>\mapa.json` y `<clase>.json`.

## 3. Mostrar (la guía)

1. En modo Mostrar, en la misma pista y clase, manejá una vuelta. Esperado: línea única "Próxima CURVA N · 640 m" (metros de a 10) → Aproximación (cartel grande, ej. "200 m", regla, cuenta regresiva chica) → Inminente (cifra y cuenta en ámbar, sin cambio de tamaño) → **FRENÁ** (franja roja arriba de 40 px, cuerpo oscuro, sin regla ni texto extra) → desaparece pasado el vértice. Toque y Levantá: franja gris azulada (nunca roja) con la velocidad mínima.
2. En una clase sin datos: **SIN DATOS** con el texto de qué hacer.
3. Bandeja › **Referencia principal**: IA o Vos cambia cuál cartel es el grande. Con carteles distintos, el principal va grande y el otro chico a la derecha; con el mismo cartel, un solo número con "◆ IA · ● VOS".
4. Mini mapa: puntos llenos = frenadas, anillos vacíos = toque/levantá; rombo = IA, círculo = vos; triángulo blanco = tu auto. Mirar: que el trazo no salga espejado ni girado respecto de la pista real.
5. **Pitido** (apagado por defecto): activarlo; debe sonar una vez al entrar en FRENÁ.

## 4. Bloqueo, atajo y mover

1. Bloqueado (por defecto): los clics y las teclas llegan a AMS2, el overlay no roba el foco ni sale en Alt-Tab, y sigue encima de AMS2 en Borderless sin PowerToys.
2. **Ctrl+Alt+L** (o bandeja › Bloquear overlay): se desbloquea. Debe verse contorno azul discontinuo, el chip "DESBLOQUEADO · arrastrá para mover", el fondo al 100 % y un agarre en la esquina inferior derecha.
3. Arrastrá la ventana con el botón izquierdo; cambiá el tamaño con la rueda o el agarre (escala 75–150 %, ancho 240–480 px). Volvé a bloquear con el atajo.
4. Cerrá y abrí la app: posición, tamaño y bloqueo se recuerdan (`config.json`).
5. Bandeja › **Posición**: las cuatro esquinas (margen 24 px). Mirar: que no tape tiempos, posiciones, retrovisor ni mapa del HUD.
6. Si Ctrl+Alt+L ya lo usa otro programa (la base Moza, por ejemplo), debe salir un globo "Atajo no disponible" y la app seguir; probá otro atajo en bandeja › Atajo de bloqueo.

## 5. Qué mirar en pantalla (para la segunda revisión de diseño)

- Que se vean **Barlow Condensed** (números y títulos) e **Inter** (textos chicos) y no el respaldo; que el cartel no cambie de ancho al pasar de 100 a 50.
- Contraste con el fondo en 80 % y en 95 % (bandeja › Opacidad del fondo); el texto nunca debe atenuarse.
- Parpadeo: grabá 10 s de video pasando por Lejos, Aproximación, Inminente, FRENÁ y una chicane; las cifras no deben cambiar más de 10 veces por segundo y la franja de FRENÁ no debe pulsar.
- Rojo de FRENÁ: probá también **Rojo suave en FRENÁ**.
- Con 1080p, 1440p y 4K, y con escala de Windows distinta de 100 %.
- Daltonismo: forma (rombo / círculo) y palabra deben alcanzar.

## Qué no cubre

No hay release por tag (fase 6). Las pruebas automáticas (`dotnet test`) cubren la lógica del Core —incluidos el modelo de vista, el anti-parpadeo, la configuración y el servicio con el simulador y con una captura real opcional— pero **no** la ventana, la bandeja, el atajo ni el click-through.
