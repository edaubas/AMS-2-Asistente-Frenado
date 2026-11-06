# AMS2 – guía de puntos de frenado

App externa (no es un mod: no toca archivos del juego). Lee la memoria compartida de AMS2
y te dice, para la pista y el auto que estás usando, cuántos metros faltan para el punto de frenado de la próxima curva.

## Requisitos
- Windows y Python 3.8+ (solo librería estándar, no hay que instalar nada).
- En AMS2: `Options > System > Shared Memory = Project CARS 2` y `UDP Frequency = OFF`.
- Para ver la ventana encima del juego: `Graphics > Window Mode = Borderless` (en pantalla completa exclusiva no se ve).

## Uso (desde esta carpeta)
- `python app.py dump` — verifica que lee el juego (pista, auto, participantes, distancias).
- `python app.py record` — manejá vueltas; guarda los puntos de frenado en `puntos_frenado.json` al cerrar cada vuelta.
- `python app.py run` — ventana siempre visible con la próxima curva y los metros hasta frenar. `Esc` la cierra.
- `python app.py run --consola --beep` — en consola, con pitidos a 150 m, 75 m y 0 m.
- `python app.py record --ia todas` — **graba a todas las IA a la vez** (sin conducir vos): infiere sus frenadas por la caída de velocidad y guarda la mediana por curva y por auto. Lo mejor es una práctica/carrera con varios autos de la misma clase y dejar pasar varias vueltas (ideal 5 o más por auto).
- `python app.py record --ia 3` — igual, pero solo el participante 3 (índices con `dump`).
- `--demo` (con cualquier comando) prueba todo sin el juego.

## Cómo funciona la tabla
- `puntos_frenado.json`: pista → variante → auto → curvas. Se edita a mano (nombres, notas, metros).
- `frenar_en` = metros de vuelta donde empezar a frenar · `largo_frenada` = metros de frenada · `apice` = donde la velocidad es mínima.
- Si re-grabás, las curvas que ya nombraste (a menos de 60 m) conservan el nombre y la nota. Se guarda `.bak` de la tabla anterior.
- Las curvas se numeran desde la meta ("Curva 1…"); no coinciden con la numeración oficial hasta que las renombres.

## Límites
- Con `record` (sin `--ia`) la tabla refleja **tu** frenada; con `--ia` refleja la de la IA, que no es la óptima pero sirve de referencia.
- Frenadas inferidas de la IA: salen por la caída de velocidad y pueden quedar unos metros tarde. En carrera el tráfico mueve los puntos; por eso conviene la mediana de muchos autos y vueltas.
- La primera vuelta que ve la app y las vueltas con boxes no cuentan.
- `python verificar_offsets.py "<ruta>\SharedMemory.h"` compara esta lectura con el header del juego (requiere g++); si Reiza cambia la versión, la app avisa y no lee.
