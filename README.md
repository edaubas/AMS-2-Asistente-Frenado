# AMS2 Asistente de Frenado

App externa para Windows que lee la telemetría de **Automobilista 2** y muestra, por pista, variante y clase, dónde frena la IA y dónde frenás vos. Es un ayuda memoria para aprender o recordar circuitos.

**Estado:** fases 1 (base del repo), 2 (lector y captura), 3 (grabación), 4 (guía) y 5 (overlay y bandeja) implementadas. **La fase 5 compila pero no se ejecutó nunca**: ni el overlay WPF, ni la bandeja, ni el atajo, ni el click-through, ni las fuentes se vieron en Windows; todo lo que lee el juego está *sin probar en AMS2*. Falta la fase 6 (release por tag). Pasos para probar el `.exe`: [docs/FASE-5-PRUEBA.md](docs/FASE-5-PRUEBA.md); diagnóstico de la fase 2: [docs/FASE-2-DIAGNOSTICO.md](docs/FASE-2-DIAGNOSTICO.md).

- [Handover: arquitectura, fases y agentes](docs/HANDOVER.md)
- [Carteles de frenada: cómo se calcula el número grande](docs/CARTELES.md)
- [Especificación de la interfaz (medidas, colores, estados)](docs/UI-SPEC.md) y [cómo probar la fase 5](docs/FASE-5-PRUEBA.md)
- [Prototipo en Python (referencia de la lógica)](prototipo/)

## Fase 3: grabación (Core, sin UI)

`AMS2Frenado.Core/Grabacion`: `Grabador.Procesar(lectura, segundos)` recibe lecturas (del juego, de una captura o del simulador) y arma, por pista + variante + clase:

- Detector fuerte (frenada: cae 30 km/h o más), suave (toque 10-30 km/h; levantá menos de 10 km/h) y levantada por acelerador (solo el auto visto), uno por participante. Ajustados a ~27 Hz (primera captura real); verificados también a ~39 Hz (captura de qualy) y a ~26 y ~19 Hz recortando muestras: las zonas salen iguales: suavizado por tiempo y reinicio ante huecos de más de 0,3 s.
- Vueltas válidas: no la primera que se ve, sin boxes (`mPitModes != 0`) y sin invalidar. Las vueltas se cuentan por cruce de meta (la captura real muestra que el primer cruce no incrementa `mLapsCompleted`).
- Mediana y dispersión por curva; la IA (pool de todos los autos de la clase) y vos se emparejan por el vértice (velocidad mínima).
- Salida en `%APPDATA%\AMS2AsistenteFrenado\<pista>\<variante>\`: `mapa.json` (un punto cada 5 m) y `<clase>.json` (curvas con bloques `ia` y `vos`). Guardado atómico (`.tmp` + reemplazo) con `.bak`; al regrabar no se pisan nombres, notas ni campos agregados a mano.
- Se descartan lecturas sin sentido (`mVersion != 14`, largo de pista 0, pista sin nombre, juego fuera de "jugando").

Probado con el simulador portado del prototipo y con una captura real de AMS2 (Watkins Glen GP, GT3_Gen2): salen las 8 zonas de frenada esperadas (cada una con una sola curva de la IA, tolerancia 70 m; con dos capturas distintas: ~9 y ~98 vueltas limpias de la IA). Cualquier otra curva es *extra* y debe ser solo tuya (sin referencia de la IA): son eventos reales de tu manejo medidos con el pedal (toques, levantadas o re-frenadas de una vuelta); su cantidad depende de cuántas vueltas limpias tengas, por eso las pruebas no exigen un total fijo. Esa prueba solo corre si la variable de entorno `AMS2_CAPTURA_PRUEBA` apunta a un `.ams2cap` (la captura no está en el repo). **Sin probar en AMS2 en vivo con la app**: la grabación en tiempo real y los pedales del auto visto (`mBrake`/`mThrottle`) solo se probaron con simulador y con una captura.

## Fase 4: guía (Core, sin UI)

`AMS2Frenado.Core/Guia/`: `MotorGuia` toma las curvas de `<clase>.json` (y `carteles` de `mapa.json`), la distancia de vuelta, la velocidad y el largo de pista, y devuelve un `PanelGuia` inmutable.

- Estados: `SinDatos`, `Lejos`, `Aproximacion`, `Inminente`, `Frena`, `Toque`, `Levanta`. El flag `Grabando` queda previsto y lo fija el modo en la fase 5.
- Ventana por tiempo: 5 s a la velocidad actual, mínimo 120 m, máximo 400 m (216 km/h = 300 m). Inminente a menos de 100 m del punto de frenado; se oculta pasado el vértice. Todo en `OpcionesGuia`.
- Histéresis: para la curva que se sigue el estado solo avanza; retrocede únicamente si la posición se aleja más de un margen (20 m) durante al menos 1 s. No parpadea por ruido de ±metros ni por huecos de datos de ~0,45 s a ~27 Hz. Los metros mostrados no suben por ruido.
- Número grande de la frenada: el **cartel** (400/300/200/100/50) donde frenar, no el metro de la pista; ver [docs/CARTELES.md](docs/CARTELES.md). Los metros restantes quedan como dato chico. IA (naranja) y vos (azul) como dos carteles; si coinciden se muestra uno.
- Toque y Levantá: la velocidad mínima es el dato principal; los metros van en chico (Toque) o no hay (Levantá).

Probado con simulación y con la captura real (Watkins Glen GP, GT3_Gen2, variable `AMS2_CAPTURA_PRUEBA`, se salta si falta): la secuencia Lejos → Aproximación → Inminente → Frená → oculto es monótona en las 8 curvas de la IA (y en las curvas extra tuyas, que la guía sigue con la otra referencia), para el auto visto y para los 46 autos de la IA. **Sin probar en AMS2 en vivo**; los 5 s, los umbrales y el desfase de cartel se calibran en pista.

## Fase 5: overlay y bandeja

Dos partes, separadas a propósito:

- **`AMS2Frenado.Core/Overlay/` (con pruebas, corre en Linux):** todo lo que no es ventana. `ConstructorVista` decide qué texto, color y forma se ve por estado (cartel grande, rombo = IA naranja, círculo = vos azul, franja roja solo en FRENÁ, franja neutra en TOQUE/LEVANTÁ, ámbar solo en la cifra de Inminente); `Suavizador` aplica las reglas anti-parpadeo (fundidos de 80/120/200 ms, cifras a 10 Hz como máximo); `ServicioOverlay` integra todo en cada tick (modo Grabar alimenta al `Grabador`; modo Mostrar carga `<clase>.json` y `mapa.json` de la pista + variante + clase y alimenta al `MotorGuia`; sin AMS2, versión distinta de 14 o sin datos avisa en el panel); además `ConfigApp`/`AlmacenConfig` (`%APPDATA%\AMS2AsistenteFrenado\config.json`, escritura atómica, tolera archivo dañado), esquinas, atajo (`Ctrl+Alt+L` por defecto), proyección del mini mapa y selección de archivos de datos.
- **`AMS2Frenado.App` (compila, sin ejecutar):** `VentanaOverlay` (WPF sin bordes, siempre arriba, semitransparente; bloqueada es click-through con `WS_EX_LAYERED|TRANSPARENT|NOACTIVATE|TOOLWINDOW`; desbloqueada se mueve y se redimensiona), `Bandeja` (`NotifyIcon` de `System.Windows.Forms`: se eligió por ser lo más corto y conocido en WPF, y solo se usa para el ícono y el menú), `AtajoGlobal` (`RegisterHotKey`; si el registro falla avisa con un globo y la app sigue), `ControladorApp` (lee ~60 Hz y une todo). La ventana de diagnóstico de la fase 2 sigue accesible desde la bandeja.
- **Fuentes:** Barlow Condensed (500/600/700) e Inter (400/600/700) embebidas desde las fuentes originales con licencia OFL (`src/AMS2Frenado.App/Fuentes/`, con sus licencias). Si WPF no las toma, cae al respaldo de la spec (Bahnschrift SemiCondensed / Segoe UI). Pendiente de comprobar en Windows qué pesos elige WPF (SemiBold/Medium) y el espaciado entre letras de la spec, que WPF no ofrece en `TextBlock` y no se aplicó.
- **Mapa:** el eje vertical es `-z` (z crece hacia adelante); si el trazo saliera espejado en el juego, es un cambio de signo en `ProyeccionMapa`.
- **Carteles por pista** siguen en el `mapa.json` de cada pista (campo `carteles`), no en `config.json`.

El número de vuelta y el aviso "Otras clases" de la spec: el panel de grabación muestra la clase en lugar de "vuelta N" (no se sabe si `mCurrentLap` empieza en 0 o en 1); lo demás sigue la spec.

## Requisito en el juego

`Options › System › Shared Memory = Project CARS 2` y `UDP Frequency = OFF`. Para ver el overlay encima del juego: `Graphics › Window Mode = Borderless`.

## Estructura

- `src/AMS2Frenado.Core`: `struct SharedMemory` v14 (20 700 bytes), lector con chequeo de versión y de `mSequenceNumber`, captura cruda y reproductor, grabación, guía y la lógica del overlay (`Overlay/`). Sin UI.
- `src/AMS2Frenado.App`: WPF: overlay, bandeja, atajo y la ventana de diagnóstico. Sin ejecutar en Windows.
- `src/AMS2Frenado.Tests`: xUnit (tamaño y offsets del struct, versión, secuencia, round-trip de captura, detectores, agrupado, grabador con simulador y captura real opcional).

## Compilar y probar

Requiere el SDK de .NET 10.

```
dotnet test                                   # Core + Tests (corre en Linux y Windows)
dotnet build src/AMS2Frenado.App              # WPF: en Linux solo compila
dotnet publish src/AMS2Frenado.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

GitHub Actions (`.github/workflows/build.yml`) corre las pruebas y sube `AMS2Frenado.exe` como artifact en cada push y pull request.
