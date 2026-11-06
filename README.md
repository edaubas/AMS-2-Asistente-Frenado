# AMS2 Asistente de Frenado

App externa para Windows que lee la telemetría de **Automobilista 2** y muestra, por pista, variante y clase, dónde frena la IA y dónde frenás vos. Es un ayuda memoria para aprender o recordar circuitos.

**Estado:** fases 1 (base del repo), 2 (lector y captura), 3 (grabación), 4 (guía), 5 (overlay y bandeja) y **5b (ajustes tras la primera prueba con AMS2)** y **5c (ajustes tras la segunda prueba)** implementadas. **Lo de ventana, sonido y cuadros de diálogo compila pero no se ejecutó nunca**: ni el overlay WPF, ni el aviso de frenada, ni el pitido, ni exportar/importar, ni la bandeja, el atajo o las fuentes se vieron en Windows; todo lo que lee el juego está *sin probar en AMS2*. Falta la fase 6 (release por tag). Pasos para probar el `.exe`: [docs/FASE-5-PRUEBA.md](docs/FASE-5-PRUEBA.md); diagnóstico de la fase 2: [docs/FASE-2-DIAGNOSTICO.md](docs/FASE-2-DIAGNOSTICO.md).

- [Handover: arquitectura, fases y agentes](docs/HANDOVER.md)
- [Carteles de frenada: cómo se calculan (desactivados en la UI desde la fase 5b)](docs/CARTELES.md)
- [Qué guarda la app y dónde (tamaños, capturas, respaldo)](docs/DATOS.md)
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
- Cartel de la frenada (400/300/200/100/50): se sigue calculando en el Core ([docs/CARTELES.md](docs/CARTELES.md)) pero **desde la fase 5b no se muestra** (se leía como cuenta regresiva). Los metros restantes van como dato chico y decreciente.
- Toque y Levantá: la velocidad mínima es el dato principal; los metros van en chico (Toque) o no hay (Levantá).

Probado con simulación y con la captura real (Watkins Glen GP, GT3_Gen2, variable `AMS2_CAPTURA_PRUEBA`, se salta si falta): la secuencia Lejos → Aproximación → Inminente → Frená → oculto es monótona en las 8 curvas de la IA (y en las curvas extra tuyas, que la guía sigue con la otra referencia), para el auto visto y para los 46 autos de la IA. **Sin probar en AMS2 en vivo**; los 5 s, los umbrales y el desfase de cartel se calibran en pista.

## Fase 5: overlay y bandeja

Dos partes, separadas a propósito:

- **`AMS2Frenado.Core/Overlay/` (con pruebas, corre en Linux):** todo lo que no es ventana. `ConstructorVista` decide qué texto, color y forma se ve por estado (leyenda con rombo = IA naranja y círculo = vos azul, barra de progreso hasta el punto de frenado, franja roja solo en FRENÁ, franja neutra en TOQUE/LEVANTÁ, ámbar solo en la cuenta regresiva chica de Inminente); `Suavizador` aplica las reglas anti-parpadeo (fundidos de 80/120/200 ms, cifras a 10 Hz como máximo); `ServicioOverlay` integra todo en cada tick (modo Grabar alimenta al `Grabador`; modo Mostrar carga `<clase>.json` y `mapa.json` de la pista + variante + clase y alimenta al `MotorGuia`; sin AMS2, versión distinta de 14 o sin datos avisa en el panel); además `ConfigApp`/`AlmacenConfig` (`%APPDATA%\AMS2AsistenteFrenado\config.json`, escritura atómica, tolera archivo dañado), atajo (`Ctrl+Alt+L` por defecto) y selección de archivos de datos.
- **`AMS2Frenado.App` (compila, sin ejecutar):** `VentanaOverlay` (WPF sin bordes, siempre arriba, semitransparente; bloqueada es click-through con `WS_EX_LAYERED|TRANSPARENT|NOACTIVATE|TOOLWINDOW`; desbloqueada se mueve y se redimensiona), `Bandeja` (`NotifyIcon` de `System.Windows.Forms`: se eligió por ser lo más corto y conocido en WPF, y solo se usa para el ícono y el menú), `AtajoGlobal` (`RegisterHotKey`; si el registro falla avisa con un globo y la app sigue), `ControladorApp` (lee ~60 Hz y une todo). La ventana de diagnóstico de la fase 2 sigue accesible desde la bandeja.
- **Fuentes:** Barlow Condensed (500/600/700) e Inter (400/600/700) embebidas desde las fuentes originales con licencia OFL (`src/AMS2Frenado.App/Fuentes/`, con sus licencias). Si WPF no las toma, cae al respaldo de la spec (Bahnschrift SemiCondensed / Segoe UI). Pendiente de comprobar en Windows qué pesos elige WPF (SemiBold/Medium) y el espaciado entre letras de la spec, que WPF no ofrece en `TextBlock` y no se aplicó.
- **Carteles por pista** siguen en el `mapa.json` de cada pista (campo `carteles`), no en `config.json`.

## Fase 5d: sincronía del símbolo con el pitido y anticipación

Detalle en [docs/UI-SPEC.md](docs/UI-SPEC.md) (T_aviso), qué probar en [docs/FASE-5-PRUEBA.md](docs/FASE-5-PRUEBA.md) sección 9. **Ventana y sonido compilan pero no se ejecutaron.**

- **Un único instante de aviso T_aviso** = punto de frenado − anticipación. La rampa del símbolo llega a 1,0 en T_aviso y el pitido se pide en la misma muestra del `DecisorAviso`; probado con el simulador y con capturas reales (27 y 39 Hz, huecos de 0,45 s, anticipaciones 0, 0,5, 1 y 2 s, velocidades de 100 a 300 km/h).
- **Nueva opción `avisoAnticipacionS`** (0 a 2,0 s, paso 0,1; 0 = como antes): el pitido suena y el símbolo llega al 100 % esos segundos *antes* del punto. Provisorio en la bandeja › "Anticipación del pitido y símbolo"; `ServicioOverlay.AvisoAnticipacionS` queda listo para el deslizador del menú de configuración.
- **Sin fundido ni tope de entrada propios** en la ventana del símbolo (solo queda el fundido de salida). Latencia que no se puede quitar (telemetría, composición de la ventana transparente, `SoundPlayer`) documentada, con `CompensacionLatenciaS` en 0 hasta calibrar en pista.
- El menú "Anticipación del aviso" pasó a llamarse "Inicio de la rampa del símbolo" (es `avisoRampaS`).

## Fase 5c: ajustes tras la segunda prueba con AMS2

Decisiones del usuario después de probar el .exe de la 5b (detalle en [docs/UI-SPEC.md](docs/UI-SPEC.md), qué probar en [docs/FASE-5-PRUEBA.md](docs/FASE-5-PRUEBA.md) sección 8). **Todo lo de ventana y sonido compila pero no se ejecutó**; la lógica tiene pruebas en Linux.

- **SIN DATOS con la causa real:** "En boxes: la guía arranca al salir a pista", "Largo de pista distinto al guardado (guardado X m, juego Y m)", "No hay curvas guardadas para <clase> en <pista · variante>" (solo si de verdad no hay archivo o curvas), "No se pudo leer el archivo de datos" (JSON dañado: `Almacen.LeerClaseDetallado` expone el motivo sin romper `LeerClase`) y "Datos sin curvas válidas".
- **Un solo decisor para el pitido y el símbolo** (`DecisorAviso`, Core): el pitido no suena si ya estás frenando (pedal > 0,15 al llegar al punto o dentro de la zona inmediata de 1 s / 40 m antes).
- **Símbolo gradual:** la opacidad (y una escala leve) sube de 0 a 1 en los últimos 2 s (configurable de 1 a 3 s: `avisoRampaS`) hasta el punto de frenado y llega al 100 % justo ahí; no vuelve atrás por ruido. Causa del símbolo tardío de la 5b: nacía con opacidad 0 en el punto y tardaba 80 ms en completarse (ver UI-SPEC 2.1).
- **La referencia principal manda** en el pitido y el símbolo: si la curva no tiene bloque de la referencia elegida (la guía cae a la otra para mostrar algo), no hay ni pitido ni símbolo en esa curva.
- **Tercer modo "Grabar solo vos"** (`ModoApp.GrabarSoloVos`, `Grabador(soloVos: true)`): la IA no se vuelve a grabar y sus bloques, nombres y notas guardados no se tocan; `mapa.json` existente tampoco. Chip "GRABANDO · SOLO VOS". Menú: Grabar todo (IA + vos) / Grabar solo vos / Mostrar. Ver [docs/DATOS.md](docs/DATOS.md).

## Fase 5b: ajustes tras probar el .exe con AMS2

Decisiones del usuario después de la primera prueba (detalle en [docs/UI-SPEC.md](docs/UI-SPEC.md)):

- **Sin mini mapa** en el overlay: el grabador sigue guardando `mapa.json`; el overlay queda mucho más chico (guía + una cabecera con pista, clase y modo).
- **Sin número grande** de cartel: la frenada se apoya en la barra de progreso, la comparación IA / vos, el nombre de la curva, las velocidades entrada → vértice y los metros chicos que bajan hasta 0. El cartel sigue en el Core ([docs/CARTELES.md](docs/CARTELES.md)).
- **Sin "Rojo suave"** y **sin esquinas en la bandeja**: franja roja de 40 px; el overlay se mueve a mano y la posición y el tamaño se guardan.
- **Aviso de frenada** (5b; en la 5c aparece gradualmente y lo decide `DecisorAviso`, ver arriba) (`AvisoFrenada` en el Core, con pruebas; `VentanaAviso` en la App, sin ejecutar): ventana aparte con el símbolo de freno rojo (vector propio) que aparece al llegar al punto de frenado y se va al apretar el freno (`mBrake` > 0,15), a los 1,5 s (configurable, 1 a 2 s) o al pasar el vértice; una vez por curva y pasada; fundido de 80 ms. Se bloquea y desbloquea junto con el overlay (mismo atajo). Menú: activar, "Probar aviso", tamaño (50–300 %) y duración.
- **Pitido** robusto: tono senoidal generado en memoria (`GeneradorTono`, con pruebas) y reproducido con `System.Media.SoundPlayer` fuera del hilo de la interfaz (respaldo `SystemSounds.Beep`); "Probar pitido" en el menú. Reemplaza a `Console.Beep`. **No se pudo ejecutar** (Linux).
- **Respaldo de datos:** bandeja › Datos › Exportar datos… (`.zip` de `%APPDATA%\AMS2AsistenteFrenado\` sin `capturas`) e Importar datos… (valida rutas contra zip-slip, deja `.bak` y no toca lo que el `.zip` no trae), más "Abrir carpeta de capturas". Lógica en `Core/Datos/RespaldoDatos.cs` con pruebas; los cuadros de diálogo compilan sin ejecutar. Dónde está cada cosa y cuánto pesa: [docs/DATOS.md](docs/DATOS.md).
- **Previsto, no implementado:** botón en el HUD para cambiar de modo y "✕" / menú contextual sobre el overlay desbloqueado.

Diferencia con la spec: el panel de grabación muestra la clase en lugar de "vuelta N" (no se sabe si `mCurrentLap` empieza en 0 o en 1); lo demás sigue la spec.

## Requisito en el juego

`Options › System › Shared Memory = Project CARS 2` y `UDP Frequency = OFF`. Para ver el overlay encima del juego: `Graphics › Window Mode = Borderless`.

## Estructura

- `src/AMS2Frenado.Core`: `struct SharedMemory` v14 (20 700 bytes), lector con chequeo de versión y de `mSequenceNumber`, captura cruda y reproductor, grabación, guía, la lógica del overlay (`Overlay/`) y el respaldo de datos (`Datos/`). Sin UI.
- `src/AMS2Frenado.App`: WPF: overlay, aviso de frenada, bandeja, atajo, pitido y la ventana de diagnóstico. Sin ejecutar en Windows.
- `src/AMS2Frenado.Tests`: xUnit (tamaño y offsets del struct, versión, secuencia, round-trip de captura, detectores, agrupado, grabador con simulador y captura real opcional).

## Compilar y probar

Requiere el SDK de .NET 10.

```
dotnet test                                   # Core + Tests (corre en Linux y Windows)
dotnet build src/AMS2Frenado.App              # WPF: en Linux solo compila
dotnet publish src/AMS2Frenado.App -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o publish
```

GitHub Actions (`.github/workflows/build.yml`) corre las pruebas y sube `AMS2Frenado.exe` como artifact en cada push y pull request.
