# Análisis: "abro el .exe, el overlay desaparece y vuelve solo"

6 de noviembre de 2026 · Rama `analisis-overlay-desaparece` · **Solo análisis: no se cambió el comportamiento de la app.**

**Síntoma (build de la fase 6):** al abrir `AMS2Frenado.exe` el overlay desaparece; el proceso sigue en el Administrador de tareas con 0 % de CPU y ~0,2 MB de memoria; al rato el overlay vuelve a aparecer.

**Convención:** *Evidencia* = lo que dice el código (archivo:línea sobre el commit `45edf2a`, antes de la instrumentación). *Hipótesis* = razonamiento mío, sin ejecutar nada. **No se pudo ejecutar WPF ni Windows** (Linux: solo compila), así que ninguna causa está confirmada.

## Supuestos

- "Desaparece" = el usuario ve el overlay un instante o no lo ve nada tras abrir el .exe; no se sabe si con AMS2 abierto o cerrado, cuánto tarda en volver ni si es en el arranque o más tarde.
- El .exe es el artefacto de `.github/workflows/build.yml` (win-x64, autocontenido, un solo archivo), bajado de GitHub (viene con la marca de "descargado de Internet") y **sin firmar**.
- La memoria que mira el usuario es la columna "Memoria" del Administrador de tareas (conjunto de trabajo privado activo).

## Lo que descarta el código (con evidencia)

| Candidata del pedido | Qué dice el código | Conclusión |
|---|---|---|
| (a) arranca oculta/colapsada hasta la primera lectura | La ventana grande se muestra siempre (`ControladorApp.cs:74` `Show()`), y `AplicarVisible(_cfg.Visible)` (`:75`) solo la colapsa si `visible=false` en `config.json` (por defecto `true`, `ConfigApp.cs:31`). La cabecera (`PanelCarbono TarjetaCabecera`) está siempre visible en el XAML (`VentanaOverlay.xaml:~20`); `Tarjeta` arranca `Collapsed` (`:~55`) pero el servicio arranca con `EsperandoAms2()` (`ServicioOverlay.cs:~57`) y se pinta en el primer tick. `VistaPanel.Nada` solo se usa como valor inicial del suavizador. | **Poco probable**. Única excepción: `"visible": false` guardado en `config.json` (entonces queda solo la pastilla 32×32 y no "vuelve sola"). |
| (c) posición fuera de pantalla | `PosicionInicial` (`ControladorApp.cs:436-452`) pasa toda posición guardada por `Esquinas.AjustarDentro` contra el área de trabajo del monitor principal; la clave es el `DeviceName` del monitor. `AjustarDentro` siempre devuelve algo dentro del área. | **Poco probable**. Riesgo menor con DPI mixto (ver "No verificado"). |
| (e) lectura bloqueante en el hilo de UI | `LectorTelemetria`/`BloqueMmf` no tienen `Sleep`, `Wait`, `.Result`, `lock` ni reintentos con espera; `Abrir()` es un `OpenExisting` que falla de inmediato si AMS2 no corre (`BloqueMmf.cs:19-43`); los 6 reintentos de `Leer` son copias de memoria (`LectorTelemetria.cs:34-45`). `grep Sleep|Wait|Task.|Thread` en App y Core: 0 usos en código de producción. | **Descartada como causa de 0 %/0,2 MB**. |
| (f) el tick oculta ventanas al reposicionar | `SincronizarBarra` solo cambia `Left`/`Top`/`Width`/`Height` (`ControladorApp.cs:276-285`, `VentanaBarra.cs:66-83`); no hay `Hide()`/`Visibility`/`Opacity` en ese camino. | **Descartada**. |
| (g) la ventana de diagnóstico roba/oculta | Solo se crea al pedirla (`ControladorApp.cs:400-406`). | **Descartada**. |
| (h) todos los Hide/Collapsed/Opacity=0 al arrancar | Ver tabla siguiente. | Ninguno oculta las ventanas grande/barra al arrancar salvo `visible=false`. |

### (h) Ocultamientos en el código (grep de `Hide(`, `Visibility.Collapsed/Hidden`, `Opacity`)

| Dónde | Condición | Efecto |
|---|---|---|
| `VentanaOverlay.xaml.cs:79-82` `AplicarVisible` | `_cfg.Visible == false` | Colapsa `Raiz` (solo contenido; la ventana sigue abierta). |
| `VentanaOverlay.xaml.cs` `PintarTarjeta` | `Panel.Tipo == Ninguna` | Colapsa `Tarjeta` (la cabecera sigue). |
| `VentanaAviso.xaml` `Simbolo Opacity=0` + `Pintar` | Siempre al arrancar | **Intencional**: el aviso nace transparente (ventana abierta, opacidad 0). |
| `VentanaMenu.xaml.cs:268-277` | `CerrarAnimado` | Único `Hide()` del código; solo el menú, al cerrarse. |
| `VentanaBarra.Reconstruir` | `Oculto` | Pastilla 32×32 (solo si `visible=false`). |
| `ControladorApp.cs:263` | Ocultar/mostrar overlay | Cierra el menú. |

## Causas candidatas, ordenadas

| # | Candidata | Probab. | Evidencia (código) | Hipótesis (razonamiento) | Qué la confirmaría | Qué la descartaría |
|---|---|---|---|---|---|---|
| 1 | **Arranque lento del .exe antes de que corra código de la app** (SmartScreen/Defender sobre un .exe sin firmar con marca de Internet; autoextracción y descompresión del bundle) | **Alta** | `AMS2Frenado.App.csproj:13` `IncludeNativeLibrariesForSelfExtract=true` y `:15` `EnableCompressionInSingleFile=true`; workflow `build.yml:23` `--self-contained -p:PublishSingleFile=true`; sin firma. No hay en el código de la app ninguna espera que deje un proceso inactivo (ver fila (e)). | Un proceso de 0,2 MB y 0 % de CPU es un proceso **recién creado que todavía no ejecutó el runtime .NET** (un WPF ya iniciado ocupa decenas de MB). Lo explica que Windows lo retenga: verificación en la nube de Defender/SmartScreen (decenas de segundos la primera vez) o extracción de las DLL nativas de WPF a `%TEMP%\.net`. "Desaparece" sería "todavía no apareció" y "vuelve" sería "terminó de arrancar". | Con `--diagnostico`: la línea `arranque` dice "desde la creación del proceso hasta OnStartup: N s" con N grande (≫ 2 s). Además: la segunda vez que se abre el .exe (ya verificado) arranca rápido; desbloquear el archivo (Propiedades › Desbloquear) o excluirlo de Defender lo acelera. | N pequeño (< 2 s) y el overlay igual no aparece. |
| 2 | **Excepción durante el arranque tragada: proceso vivo sin ventanas** | Media-baja | `App.xaml.cs:30` `ev.Handled = true` y `ShutdownMode="OnExplicitShutdown"` (`App.xaml:4`): si `new ControladorApp()` o `Iniciar()` (`App.xaml.cs:33-34`) lanzan, la excepción se descarta (solo `Debug.WriteLine`, que en Release no deja rastro) y el proceso queda vivo sin timer ni ventanas. Los inicializadores de `ControladorApp` crean ventanas (`ControladorApp.cs:29-30`) y la bandeja (`:39`), con carga de XAML, fuentes embebidas (`./Fuentes/#Barlow Condensed`) e ícono. | Explica "proceso vivo, sin overlay, 0 % CPU", pero **no** el "vuelve a aparecer" (a menos que el usuario lo haya relanzado). Probable solo si algo falla solo en la máquina del usuario (fuentes, ícono, GPU, DPI). | Con `--diagnostico`: línea `error ... DispatcherUnhandledException` y falta `Iniciar() terminó`/`timer iniciado`. | Aparecen `Iniciar() terminó` y `timer iniciado`. |
| 3 | **Pérdida de "siempre arriba" frente al juego** (Borderless/pantalla completa) y reaparición cuando AMS2 cede el foco | Media-baja | Las tres ventanas son `Topmost` y se reafirman con `SetWindowPos(HWND_TOPMOST)` cada **2 s** (`ControladorApp.cs:139-143`, `Win32.cs:72-73`); `NOACTIVATE|TOOLWINDOW|LAYERED` (`Win32.cs:56-62`); la barra es *owned* por la grande (`VentanaBarra.cs:42`). | En pantalla completa exclusiva ningún overlay se ve; en Borderless se ve. Si el juego retoma el primer plano tras el arranque, el re-afirmado lo corrige en ≤ 2 s (no "al rato", salvo que el tick no corra). No explica el 0,2 MB. | Registro: `Topmost=True`, `IsVisible=True` y posición correcta en las ventanas mientras no se ve nada ⇒ es de orden en Z; verlo con AMS2 cerrado descarta. | Pasa con AMS2 **cerrado**. |
| 4 | **Cierre incompleto: `Salir()` falla a mitad y no llama a `Shutdown()`** | Baja | `ControladorApp.cs` (baseline `:600-604`) `Salir()` llama `Dispose()` y recién después `Shutdown()`; `Dispose` cierra la bandeja, menú, barra, aviso y ventana (`:606-621`) sin `try/finally`. Con `OnExplicitShutdown` y `Handled = true`, una excepción ahí deja un proceso zombi con timer parado (0 % CPU) y sin ventanas. | Solo aplica si el usuario cerró con ✕/Salir en la prueba anterior y volvió a abrir. No explica "vuelve a aparecer". | Registro: `Salir() pedido` sin `Dispose terminó`, y/o `error` justo después. | `Dispose terminó; Shutdown()` y `OnExit` presentes. |
| 5 | **Instancia anterior viva (mutex) y segunda instancia** | Baja | Mutex `Local\AMS2AsistenteFrenado-instancia` (`App.xaml.cs:17`); la segunda instancia muestra un `MessageBox` y hace `Shutdown()` (`:21-23`). Si hay un zombi (causas 2 o 4), la siguiente apertura muestra "ya está en ejecución", no el overlay. | Si el usuario ve ese mensaje, lo habría contado; un `MessageBox` modal detrás de otra ventana sí podría pasar desapercibido. | Dos procesos `AMS2Frenado` en el Administrador. Registro: `primeraInstancia=False`. | Un solo proceso. |
| 6 | **Render por software de ventanas con `AllowsTransparency` (capa) o hilo de UI lento** | Baja | `VentanaOverlay.xaml:6` y `VentanaAviso.xaml:5` usan `AllowsTransparency=True` (render por software); textura con `DrawingBrush` en mosaico de 6×6 (`Estilo.cs:14-38`, `CachingHint.Cache`); tick de 16 ms (`ControladorApp.cs:28`). | Podría dar parpadeos o lentitud, no un proceso de 0 % CPU y 0,2 MB. | Registro: líneas `tick-lento` (≥ 100 ms) frecuentes. | Sin `tick-lento`. |
| 7 | **`visible:false` o posición en `config.json` de pruebas anteriores** | Baja | `ConfigApp.cs:31` y `AlmacenConfig.Leer` (`:117-143`): `visible`, `posiciones` se leen tal cual (con `AjustarDentro`). | Un build anterior pudo dejar `visible:false`: solo se vería la pastilla de 32×32. | `config.json` con `"visible": false`; registro `config: visible=False`. | `visible=True`. |

**Lectura conjunta:** el dato más fuerte es "0,2 MB y 0 % de CPU", que **no se corresponde con una app WPF ya iniciada** (el timer de 16 ms más el repintado darían CPU > 0 y decenas de MB). Eso favorece la causa 1 (el proceso todavía no arrancó) o la 2/4 (arrancó y quedó sin hacer nada). Es un razonamiento, no una medición.

## Propuestas de arreglo (no aplicadas)

1. **Para la causa 1** (si el registro la confirma):
   - `AMS2Frenado.App.csproj:15`: quitar `EnableCompressionInSingleFile` (el bundle sin comprimir arranca más rápido; el .exe pesa más).
   - Alternativa si sigue lento: distribuir la carpeta `publish` (varios archivos, sin autoextracción) en lugar de un solo .exe.
   - `build.yml`: agregar `-p:PublishReadyToRun=true` (menos tiempo de JIT al arrancar) y, a futuro, firmar el .exe.
   - UX: mostrar de inmediato una ventana/splash mínima o un globo de la bandeja "Iniciando…" **antes** de crear el resto, para que el usuario vea que arrancó (solo ayuda cuando la demora es posterior a `OnStartup`).
   - Documentar en `docs/FASE-6-PRUEBA.md` "Desbloquear" el .exe y esperar la primera vez.
   - Prueba en Core/Tests: **no aplica** (es de empaquetado/Windows).
2. **Para las causas 2 y 4** (robustez; conviene hacerlo igual):
   - `App.xaml.cs:33-34`: envolver `new ControladorApp()` + `Iniciar()` en `try/catch`; ante un fallo, mostrar un `MessageBox` con el mensaje, escribirlo al registro y llamar `Shutdown()`. Nunca dejar el proceso vivo sin ventanas.
   - `ControladorApp.Salir()` (baseline `:600-604`): `try { Dispose(); } finally { Application.Current.Shutdown(); }`.
   - Prueba: la lógica queda en WPF, no en Core. Si se quiere un test, mover a Core una función `Resultado Intentar(Action, Action<Exception>)` trivial y probarla (poco valor); más útil el registro.
3. **Para la causa 3** (si el registro la confirma): además del reafirmado cada 2 s, volver a hacer `SetWindowPos(HWND_TOPMOST)` al activarse otra aplicación (`Application.Deactivated` / `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`), y bajar el período a 0,5 s mientras no haya lectura `Ok`. Prueba en Core: un `RelojReafirmar` puro (¿toca reafirmar en `t`?) con xUnit; el hook en sí no se puede probar sin Windows.

## Instrumentación agregada (apagada por defecto)

- **Core** `Overlay/RegistroDiagnostico.cs` (+ `FormatoRegistro`): formato `fecha hora.ms [categoría] mensaje`, una línea por evento, máx. 300 caracteres, sin saltos de línea, la carpeta del usuario reemplazada por `~`; rotación a `registro.log.1` al llegar a ~1 MB (máximo ≈ 2 MB en disco); nunca lanza; seguro entre hilos. 11 pruebas xUnit (`RegistroDiagnosticoTests`): apagado = no crea archivo, formato, recorte, anonimizado, marcas de activar/apagar, umbral de tick (100 ms), deduplicación de cambios, rotación, continuar archivo existente, no lanzar con ruta inválida, error sin pila.
- **App** `Diag.cs` + cableado: ruta `%APPDATA%\AMS2AsistenteFrenado\registro.log`. Se anota: arranque (versión, SO, argumentos, **segundos desde la creación del proceso hasta `OnStartup`**, monitores, config relevante), creación del HWND/mostrado/visible/oculto/cerrado de cada ventana con `Visibility`, `IsVisible`, `Topmost`, `Opacity`, posición, tamaño, monitor y HWND; foto de las cuatro ventanas una vez por segundo (solo si cambió algo); cambios de estado de lectura (`SinJuego`/`Ok`/`VersionDistinta`/`Inestable`); ticks ≥ 100 ms; primer tick; errores atrapados (`Tick`, `Leer`, `DispatcherUnhandledException`, excepciones no atrapadas); salida (`Salir()`, `Dispose`, `OnExit`).
- **Cómo se activa:** (a) acceso directo o consola: `AMS2Frenado.exe --diagnostico`; (b) con la app abierta: ⚙ › Datos › **Guardar registro de diagnóstico**, o bandeja › **Guardar registro de diagnóstico**. No se guarda en `config.json`: al reiniciar vuelve a estar apagado. El archivo se ve con ⚙ › Datos › Abrir carpeta de datos.
- Compila, **sin ejecutar** en Windows; el cableado no cambia ninguna decisión de la app (solo lee estado y escribe texto, todo con `try/catch`).

## Qué pedirle al usuario

1. ¿Pasa con **AMS2 cerrado**, abierto en menús o en pista? ¿Y con el juego en pantalla completa o Borderless?
2. Cuánto tarda en volver (cronometrado) y si es en el primer arranque del .exe nuevo o también en los siguientes.
3. Abrir el .exe con `--diagnostico` (acceso directo con `AMS2Frenado.exe --diagnostico` en "Destino") y mandar `%APPDATA%\AMS2AsistenteFrenado\registro.log` (y `registro.log.1` si existe) y `config.json`.
4. En el Administrador de tareas: cuántos procesos `AMS2Frenado` hay, y si la columna "Memoria" sube después de un rato (y si el ícono de la bandeja ya está desde el inicio).
5. Propiedades del .exe: si dice "Este archivo proviene de otro equipo… Desbloquear"; probar tildar Desbloquear. ¿Antivirus además de Defender?
6. ¿Se ve el ícono de la bandeja mientras el overlay no está? ¿Aparece algún mensaje o ventana de Windows (SmartScreen) al abrir?

## Qué NO se pudo verificar (no hay Windows/WPF acá)

- Nada se ejecutó: ni el arranque, ni el orden en Z, ni el render. Las causas 1-7 son hipótesis.
- Si Defender/SmartScreen o la autoextracción explican de verdad 0,2 MB y 0 % de CPU, y cuánto demoran.
- Que el registro compile y escriba bien en Windows (compila en Linux; el cableado WPF nunca corrió). `Process.StartTime` y `Screen.FromHandle` se usan dentro de `try/catch`.
- Comportamiento de DPI (el proyecto no tiene `app.manifest`; la conversión píxel/DIP en `MonitorDe`, `ControladorApp.cs:413-425`, con monitores de DPI distinto no se probó).
- Si `DispatcherUnhandledException` recibe excepciones de `OnStartup` (se asume que sí, por cómo corre `Application.Run`).
- Orden en Z real frente a AMS2 en Borderless y en pantalla completa exclusiva.
