# AMS2 Asistente de Frenado

App externa para Windows que lee la telemetría de **Automobilista 2** y muestra, por pista, variante y clase, dónde frena la IA y dónde frenás vos. Es un ayuda memoria para aprender o recordar circuitos.

**Estado:** fases 1 (base del repo), 2 (lector y captura) y 3 (grabación, solo en Core, sin UI) implementadas; **la fase 2 espera que el usuario corra el diagnóstico con AMS2 real** (todo lo que lee el juego está *sin probar en AMS2*). Ver [docs/FASE-2-DIAGNOSTICO.md](docs/FASE-2-DIAGNOSTICO.md).

- [Handover: arquitectura, fases y agentes](docs/HANDOVER.md)
- [Prototipo en Python (referencia de la lógica)](prototipo/)

## Fase 3: grabación (Core, sin UI)

`AMS2Frenado.Core/Grabacion`: `Grabador.Procesar(lectura, segundos)` recibe lecturas (del juego, de una captura o del simulador) y arma, por pista + variante + clase:

- Detector fuerte (frenada: cae 30 km/h o más), suave (toque 10-30 km/h; levantá menos de 10 km/h) y levantada por acelerador (solo el auto visto), uno por participante. Ajustados a ~27 Hz (la frecuencia real medida): suavizado por tiempo y reinicio ante huecos de más de 0,3 s.
- Vueltas válidas: no la primera que se ve, sin boxes (`mPitModes != 0`) y sin invalidar. Las vueltas se cuentan por cruce de meta (la captura real muestra que el primer cruce no incrementa `mLapsCompleted`).
- Mediana y dispersión por curva; la IA (pool de todos los autos de la clase) y vos se emparejan por el vértice (velocidad mínima).
- Salida en `%APPDATA%\AMS2AsistenteFrenado\<pista>\<variante>\`: `mapa.json` (un punto cada 5 m) y `<clase>.json` (curvas con bloques `ia` y `vos`). Guardado atómico (`.tmp` + reemplazo) con `.bak`; al regrabar no se pisan nombres, notas ni campos agregados a mano.
- Se descartan lecturas sin sentido (`mVersion != 14`, largo de pista 0, pista sin nombre, juego fuera de "jugando").

Probado con el simulador portado del prototipo y con una captura real de AMS2 (Watkins Glen GP, GT3_Gen2): sale la lista de 8 zonas de frenada esperadas. Esa prueba solo corre si la variable de entorno `AMS2_CAPTURA_PRUEBA` apunta a un `.ams2cap` (la captura no está en el repo). **Sin probar en AMS2 en vivo con la app**: la grabación en tiempo real y los pedales del auto visto (`mBrake`/`mThrottle`) solo se probaron con simulador y con una captura.

## Requisito en el juego

`Options › System › Shared Memory = Project CARS 2` y `UDP Frequency = OFF`. Para ver el overlay encima del juego: `Graphics › Window Mode = Borderless`.

## Estructura

- `src/AMS2Frenado.Core`: `struct SharedMemory` v14 (20 700 bytes), lector con chequeo de versión y de `mSequenceNumber`, captura cruda y reproductor. Sin UI.
- `src/AMS2Frenado.App`: WPF; por ahora solo la ventana de diagnóstico.
- `src/AMS2Frenado.Tests`: xUnit (tamaño y offsets del struct, versión, secuencia, round-trip de captura, detectores, agrupado, grabador con simulador y captura real opcional).

## Compilar y probar

Requiere el SDK de .NET 10.

```
dotnet test                                   # Core + Tests (corre en Linux y Windows)
dotnet build src/AMS2Frenado.App              # WPF: en Linux solo compila
dotnet publish src/AMS2Frenado.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

GitHub Actions (`.github/workflows/build.yml`) corre las pruebas y sube `AMS2Frenado.exe` como artifact en cada push y pull request.
