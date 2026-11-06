# Qué guarda la app y dónde

Todo vive en `%APPDATA%\AMS2AsistenteFrenado\` (normalmente `C:\Users\<vos>\AppData\Roaming\AMS2AsistenteFrenado\`). Desde la bandeja: **Datos › Abrir carpeta de datos** y **Datos › Abrir carpeta de capturas**.

| Qué | Dónde | Cuándo se crea | Tamaño real |
|---|---|---|---|
| **Mapa de la pista** | `<pista>\<variante>\mapa.json` | Al grabar (modo Grabar), una vez por pista y variante. Se sigue guardando aunque el overlay ya no lo dibuje | **~73 KB** (un punto cada 5 m; Watkins Glen GP) |
| **Puntos de frenada** | `<pista>\<variante>\<clase>.json` (por ejemplo `GT3_Gen2.json`) | Al grabar, uno por clase de auto | **5 a 7 KB** (8 curvas de frenada más las extras tuyas) |
| **Configuración** | `config.json` | Al abrir la app y cada vez que cambiás algo | **< 1 KB** |
| **Copias de seguridad** | `*.json.bak` al lado de cada archivo | Automáticas al reemplazar un archivo | Igual que el original |
| **Capturas crudas** | `capturas\captura-<fecha>.ams2cap` | **Solo** al apretar "Empezar captura cruda" en la ventana de **Diagnóstico**. Nunca solas | **~1,2 a 2 MB por segundo**: 240 MB (la primera) a 410 MB (la segunda) en nuestras pruebas, unos 400 MB en 8 minutos |

**Tamaño medido.** Se grabaron las dos capturas reales (Watkins Glen GP, GT3_Gen2) con el servicio de la app y se midieron los archivos: `GT3_Gen2.json` 5 401 y 6 946 bytes; `mapa.json` 73 418 y 73 446 bytes; total sin `.bak`: **78 819 y 80 392 bytes (~80 KB por pista y clase)**. Cada clase nueva en la misma pista suma 5 a 7 KB. Con 20 pistas y 3 clases cada una, son unos 2 MB: el respaldo cabe en un mail.

## Las capturas no son automáticas

Verificado en el código (`GrabadorCaptura.Crear` tiene un único llamador): ninguna otra parte de la app escribe capturas. Ni el modo Grabar, ni el overlay, ni el lector. El modo **Grabar** guarda solo los JSON de la tabla. Las capturas se crean únicamente al apretar el botón de la ventana de Diagnóstico (y se cierran al apretarlo de nuevo o al cerrar esa ventana). Pesan cientos de MB: borralas cuando no las necesites. **No entran en el respaldo.**

## Respaldar tus puntos de frenada

Bandeja › **Datos › Exportar datos…** crea un `.zip` con todo `%APPDATA%\AMS2AsistenteFrenado\` **menos** `capturas\`, los `.bak` y los `.tmp`. Es para pasar tus datos a otra PC o guardarlos antes de reinstalar.

Bandeja › **Datos › Importar datos…** lee ese `.zip`:

- Antes de tocar nada valida todo el `.zip`: solo acepta rutas relativas dentro de la carpeta de datos (se rechazan `..`, rutas absolutas, `C:\…` y similares: protección *zip-slip*) y solo archivos `.json` que se puedan leer. Si algo falla, **no se importa nada**.
- De cada archivo que ya existía deja una copia `.bak` al lado y recién después lo reemplaza.
- No toca `capturas\`, ni `config.json` si el `.zip` no lo trae.
- Hay que estar en **Modo › Mostrar** (en Grabar el programa podría pisar lo importado).

*Los cuadros de diálogo de exportar e importar compilan pero no se ejecutaron nunca en Windows. La lógica del `.zip` (incluido el zip-slip) tiene pruebas en Linux.*
