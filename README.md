# AMS2 Asistente de Frenado

App externa para Windows que lee la telemetría de **Automobilista 2** y muestra, por pista, variante y clase, dónde frena la IA y dónde frenás vos. Es un ayuda memoria para aprender o recordar circuitos.

**Estado:** fases 1 (base del repo) y 2 (lector y captura) implementadas; **la fase 2 espera que el usuario corra el diagnóstico con AMS2 real** (todo lo que lee el juego está *sin probar en AMS2*). Ver [docs/FASE-2-DIAGNOSTICO.md](docs/FASE-2-DIAGNOSTICO.md).

- [Handover: arquitectura, fases y agentes](docs/HANDOVER.md)
- [Prototipo en Python (referencia de la lógica)](prototipo/)

## Requisito en el juego

`Options › System › Shared Memory = Project CARS 2` y `UDP Frequency = OFF`. Para ver el overlay encima del juego: `Graphics › Window Mode = Borderless`.

## Estructura

- `src/AMS2Frenado.Core`: `struct SharedMemory` v14 (20 700 bytes), lector con chequeo de versión y de `mSequenceNumber`, captura cruda y reproductor. Sin UI.
- `src/AMS2Frenado.App`: WPF; por ahora solo la ventana de diagnóstico.
- `src/AMS2Frenado.Tests`: xUnit (tamaño y offsets del struct, versión, secuencia, round-trip de captura).

## Compilar y probar

Requiere el SDK de .NET 10.

```
dotnet test                                   # Core + Tests (corre en Linux y Windows)
dotnet build src/AMS2Frenado.App              # WPF: en Linux solo compila
dotnet publish src/AMS2Frenado.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

GitHub Actions (`.github/workflows/build.yml`) corre las pruebas y sube `AMS2Frenado.exe` como artifact en cada push y pull request.
