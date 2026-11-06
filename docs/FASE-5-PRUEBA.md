# Fase 5, 5b, 5c y 5d: cómo probar el .exe con AMS2

Todo lo de la ventana, la bandeja, el atajo, el click-through, el aviso de frenada, el sonido, las fuentes y los cuadros de exportar e importar **compila pero nunca se ejecutó** (se desarrolló en Linux). Esta lista es para probarlo en Windows con AMS2. Anotá qué falla y con qué pantalla.

La **fase 5b** son los ajustes después de la primera prueba con AMS2: sin mini mapa, sin cartel grande, sin "Rojo suave", sin esquinas en el menú, aviso de frenada nuevo, pitido nuevo y respaldo de datos. El detalle del diseño está en [UI-SPEC.md](UI-SPEC.md) y qué se guarda en [DATOS.md](DATOS.md).

La **fase 5c** son los ajustes después de la segunda prueba (run #9): SIN DATOS con la causa real, pitido que no suena si ya estás frenando, símbolo que aparece gradualmente, pitido y símbolo solo con la referencia principal y el modo "Grabar solo vos". Probarlos en la sección 8.

**Antes:** `Options › System › Shared Memory = Project CARS 2`, `UDP Frequency = OFF`, `Graphics › Window Mode = Borderless`. El `.exe` sale del artifact `AMS2Frenado-win-x64` de GitHub Actions (rama `fase-5c-ajustes`). Para empezar de cero, borrá `%APPDATA%\AMS2AsistenteFrenado\config.json` (los campos viejos `mapaVisible`, `mapaCompacto` y `rojoSuave` de un `config.json` anterior se ignoran).

## 1. Arranque y bandeja

1. Ejecutá `AMS2Frenado.exe`. Debe aparecer un globo con el aviso de que el overlay está bloqueado, dónde está el ícono y el atajo. Mirar: si el globo se ve, y si el ícono (candado naranja sobre círculo oscuro) está en la bandeja o en la flecha `^` de Windows 11.
2. Sin AMS2 abierto el overlay dice **ESPERANDO AMS2** y cómo activar la memoria compartida.
3. Clic derecho en el ícono. Menú: Bloquear overlay, Modo (**Grabar todo (IA + vos)**, **Grabar solo vos**, **Mostrar**), Referencia principal, Tamaño del overlay, Opacidad del fondo, **Aviso de frenada (símbolo)**, **Probar aviso**, Tamaño del aviso, **Anticipación del aviso**, Duración del aviso después del punto, Pitido al frenar, **Probar pitido**, Atajo de bloqueo, Ocultar/Mostrar overlay, **Datos** (Abrir carpeta de datos, Abrir carpeta de capturas, Exportar datos…, Importar datos…), Diagnóstico…, Salir. Ya **no** están Posición, Mostrar mapa, Mapa compacto ni Rojo suave. Mirar: que cada marca refleje el estado.
4. Abrí **Diagnóstico…** y comprobá que sigue leyendo AMS2 como en la fase 2.
5. Abrí otra copia del `.exe`: debe avisar que ya está en ejecución.

## 2. Grabar

1. Con AMS2 en pista (cualquier clase con IA), bandeja › **Modo › Grabar todo (IA + vos)**. Debe verse el chip rojo **GRABANDO** en la cabecera y, mientras no haya guía, los contadores **IA / VOS** (ya no hay MAPA) con el texto "Parar de grabar: Bandeja › Modo › Mostrar". El mini mapa ya no existe: el grabador sigue armando `mapa.json` por detrás.
2. Da unas 10 vueltas limpias (sin boxes, sin invalidar). Mirar: que los contadores suban.
3. Bandeja › **Modo › Mostrar** (esto guarda). Bandeja › **Datos › Abrir carpeta de datos**: debe haber `<pista>\<variante>\mapa.json` y `<clase>.json`.

## 3. Mostrar (la guía)

1. En modo Mostrar, en la misma pista y clase, manejá una vuelta. Esperado: línea única "Próxima CURVA N · 640 m" (metros de a 10) → Aproximación (nombre de la curva, "248 → 96 km/h", leyenda "◆ IA · ● VOS", **barra de progreso** con los marcadores y los metros chicos que bajan hasta 0; **sin número grande**) → Inminente (los metros chicos en ámbar) → **FRENÁ** (franja roja arriba de 40 px, cuerpo oscuro con leyenda y velocidades, sin barra) → desaparece pasado el vértice. Toque y Levantá: franja gris azulada (nunca roja) con la velocidad mínima.
2. En una clase sin datos: **SIN DATOS** con el texto de qué hacer (las demás causas, en la sección 8).
3. Bandeja › **Referencia principal**: IA o Vos cambia cuál referencia manda en la guía (la barra y el cálculo de metros). Mirar que la barra muestre el rombo (IA) y el círculo (vos) y que se entienda la diferencia entre los dos puntos de frenado.
4. **Pitido** (apagado por defecto): activarlo; debe sonar una vez al llegar al punto de frenado (reglas de la 5c en la sección 8). Primero probá **Probar pitido**: tiene que sonar un tono corto de 880 Hz. Si no suena nada, anotá si salió el sonido de respaldo de Windows (`SystemSounds.Beep`).
5. El nombre de la pista en la cabecera ya no debe quedar pisado por el chip de la clase (se recorta con "…").

## 4. Aviso de frenada (nuevo)

1. Bandeja › **Probar aviso**: debe aparecer el símbolo rojo (círculo con "!" y dos arcos) unos 1,5 s, con fundido corto, y apagarse solo. Funciona sin estar en pista.
2. En pista, modo Mostrar, con datos: desde la 5c el símbolo **aparece gradualmente** (ver sección 8) y llega al 100 % en el punto de frenado; desaparece **al apretar el freno** (pedal > 15 %), a los 1,5 s después del punto si no frenás, o al pasar el vértice. No debe repetirse en la misma curva. Si ya venís frenando, no aparece.
3. Mirar: que el fundido de 80 ms no se vea como parpadeo; que el contorno oscuro deje ver el rojo sobre cielo, asfalto y nieve; que el símbolo no tape lo importante; que 120 px sea un buen tamaño.
4. **Desbloquear** (Ctrl+Alt+L o bandeja): el símbolo se ve siempre sobre un fondo oscuro con contorno azul y un chip "AVISO". Arrastrarlo con el botón izquierdo; cambiar el tamaño con la rueda o el agarre de la esquina (50 a 300 %). Bloquear de nuevo: **el mismo atajo bloquea las dos ventanas** (overlay y aviso). Cerrá y abrí la app: posición y tamaño del aviso se recuerdan por separado (`config.json`: `avisoPosiciones`, `avisoEscala`).
5. Desactivar "Aviso de frenada (símbolo)" en la bandeja: no debe aparecer en pista; "Probar aviso" sigue mostrándolo.
6. Con la ventana del aviso bloqueada, los clics y las teclas tienen que llegar a AMS2 y no debe robar el foco.

## 5. Bloqueo, atajo y mover

1. Bloqueado (por defecto): los clics y las teclas llegan a AMS2, el overlay no roba el foco ni sale en Alt-Tab, y sigue encima de AMS2 en Borderless sin PowerToys.
2. **Ctrl+Alt+L** (o bandeja › Bloquear overlay): se desbloquea. Debe verse contorno azul discontinuo, el chip "DESBLOQUEADO · arrastrá para mover", el fondo al 100 % y un agarre en la esquina inferior derecha.
3. Arrastrá la ventana con el botón izquierdo; cambiá el tamaño con la rueda o el agarre (escala 75–150 %, ancho 240–480 px). Volvé a bloquear con el atajo. **Ya no hay esquinas en el menú**: la posición se mueve a mano.
4. Cerrá y abrí la app: posición, tamaño y bloqueo se recuerdan (`config.json`).
5. Mirar que el overlay (ahora mucho más chico) no tape tiempos, posiciones ni retrovisor del HUD.
6. Si Ctrl+Alt+L ya lo usa otro programa (la base Moza, por ejemplo), debe salir un globo "Atajo no disponible" y la app seguir; probá otro atajo en bandeja › Atajo de bloqueo.

## 6. Respaldo de datos

1. Bandeja › **Datos › Exportar datos…**: abre un cuadro de guardar (comprobar que **no queda detrás de AMS2**). Guardá el `.zip`; debe salir un globo con la cantidad de archivos. Abrilo: tiene `config.json` y las carpetas de pistas, **sin** `capturas`, `.bak` ni `.tmp`.
2. Bandeja › **Datos › Importar datos…**: pide elegir un `.zip` y confirmar; con ese mismo `.zip` debe importar todo y dejar `.bak` junto a cada archivo que ya existía. En cualquier modo de grabación (Grabar todo o Grabar solo vos) debe negarse con un aviso.
3. Probá un `.zip` hecho a mano con una entrada `..\evil.json`: debe decir "ruta no permitida" y no importar nada.
4. Bandeja › **Datos › Abrir carpeta de capturas**: abre `%APPDATA%\AMS2AsistenteFrenado\capturas` (la crea si no existe). Las capturas solo aparecen si apretaste el botón en Diagnóstico.

## 7. Qué mirar en pantalla (para la segunda revisión de diseño)

- Que se vean **Barlow Condensed** (números y títulos) e **Inter** (textos chicos) y no el respaldo.
- Contraste con el fondo en 80 % y en 95 % (bandeja › Opacidad del fondo); el texto nunca debe atenuarse.
- Parpadeo: grabá 10 s de video pasando por Lejos, Aproximación, Inminente, FRENÁ y una chicane; las cifras no deben cambiar más de 10 veces por segundo, la franja de FRENÁ no debe pulsar y el aviso debe aparecer una sola vez por curva.
- Con 1080p, 1440p y 4K, y con escala de Windows distinta de 100 %.
- Daltonismo: forma (rombo / círculo) y palabra deben alcanzar.

## 8. Fase 5c: qué probar

Todo esto **compila pero nunca se ejecutó** (la lógica está probada en Linux con `dotnet test`; la ventana, el símbolo y el sonido no).

**SIN DATOS con la causa real**

1. Modo Mostrar, en una pista y clase **sin** archivo: "No hay curvas guardadas para <clase> en <pista · variante>." con "Grabar todo" en la línea 2.
2. Con datos buenos, empezá la sesión **en boxes** o en el garaje: debe decir "En boxes: la guía arranca al salir a pista." (y **no** "No hay curvas guardadas"). Al salir a pista aparece la guía.
3. Cambiale el largo a un `<clase>.json` (campo `largo`, por ejemplo 5000 en una pista de 5891): "Largo de pista distinto al guardado (guardado 5000 m, juego 5891 m)."
4. Estropeá un `.json` (borrá una llave) y borrá su `.bak`: "No se pudo leer el archivo de datos." Con un `.bak` bueno debe usar el respaldo y mostrar la guía.
5. Poné `"curvas": []`: "No hay curvas guardadas…". Poné una curva con `frenar_en` igual a `vertice`: "Datos sin curvas válidas."

**Símbolo gradual y pitido**

6. Pitido activado, modo Mostrar, **sin frenar** (dejá pasar el punto): el símbolo debe empezar a verse unos 2 s antes del punto, ir subiendo y estar **completo justo en el punto**, donde suena el pitido. Mirar si se ve fluido (sin saltos ni parpadeo) y si la escala leve (de 85 % a 100 %) se nota o molesta.
7. **Frenando a tiempo**: si apretás el freno (> 15 %) dentro de los últimos ~1 s o ~40 m antes del punto, o al llegar, **no debe sonar el pitido** y el símbolo debe apagarse (fundido corto) y no volver aunque sueltes. Si apretás el freno en el punto o después, el pitido ya sonó en el punto. Un toque de freno más lejos del punto (final de la frenada anterior en una chicane) solo oculta el símbolo mientras dura.
8. Bandeja › **Inicio de la rampa del símbolo** (antes "Anticipación del aviso"): 1,0 a 3,0 s. Cambia desde cuándo empieza a verse. Buscá el valor que te dé tiempo sin distraer.
9. Una chicane: cada curva avisa por separado y ninguna repite en la misma pasada.

**Referencia principal manda**

10. Bandeja › Referencia principal = **IA**. En una curva que la IA no frena y vos sí (curva con solo `vos` en el archivo, por ejemplo las Curva 8, 9 y 10 de Spa de `F-Ultimate_Gen2`): **no** debe sonar el pitido ni verse el símbolo en esa curva (el panel puede seguir mostrándola, con la etiqueta VOS). En las curvas con IA sí.
11. Cambiá a **Vos**: ahora esa curva sí avisa; y una curva con solo `ia` ya no.

**Grabar solo vos**

12. Bandeja › Modo › **Grabar solo vos**: chip rojo **GRABANDO · SOLO VOS** y, sin guía, contadores solo de VOS. La guía (lo ya guardado) se ve igual.
13. Antes de empezar, copiá `<clase>.json` aparte. Da unas vueltas en este modo y volvé a **Mostrar** (guarda). Comparando: los bloques `ia`, los nombres y las notas tienen que ser **idénticos**; las curvas que solo tenían IA siguen; solo cambian los bloques `vos`. `mapa.json` no debe cambiar (si no existía, se crea con tus vueltas). Pasar de "Grabar todo" a "solo vos" guarda lo juntado y empieza limpio.

## 9. Fase 5d: sincronía y anticipación

Todo esto **compila pero nunca se ejecutó** (la lógica está probada en Linux; ventana y sonido no). Detalle en UI-SPEC (T_aviso).

**Sincronía del símbolo y el pitido**

1. Pitido activado, anticipación 0 s, sin frenar: el símbolo debe terminar de aparecer (100 %) **justo cuando suena el pitido**, ni antes ni después. Mirá si se nota un desfase fijo (el símbolo llega un poco después o antes del sonido). Si lo hay, anotá cuánto (idealmente con video + audio) para calibrar `CompensacionLatenciaS`.
2. Repetí a distintas velocidades (frenada desde ~100, ~216 y ~300 km/h): el desfase percibido tiene que ser el mismo.
3. Probalo en **ventana** y en **pantalla completa sin bordes** (borderless): ¿la ventana transparente se compone igual?

**Deslizador provisorio (bandeja)**

4. Bandeja › **Anticipación del pitido y símbolo**: 0; 0,2; 0,4; 0,6; 0,8; 1,0; 1,5; 2,0 s. Debe quedar marcado el valor actual, también después de reiniciar la app (`avisoAnticipacionS` en `config.json`; un config viejo sin el campo arranca en 0 s).
5. **Probar aviso**: muestra la rampa completa (duración = "Inicio de la rampa del símbolo") y, con el pitido activado, suena al llegar al 100 %; el símbolo sigue pleno `anticipación + duración` más. **Probar pitido** hace lo mismo pero suena siempre (aunque el pitido esté apagado). Probalos con 0 y con 2,0 s.

**Anticipación en pista**

6. Con 1,0 s: el pitido y el 100 % del símbolo ocurren **1 s antes** del punto de frenado (a 216 km/h, a unos 60 m antes). Con 2,0 s, a ~120 m. El símbolo se mantiene pleno hasta que frenás, 1,5 s después del **punto** o el vértice.
7. Frená dentro de la zona inmediata de T_aviso (último 1 s o 40 m antes de T_aviso): no debe pitar ni verse. Frená después del pitido pero antes del punto: el símbolo se apaga y no vuelve.
8. Chicane: cada curva avisa una sola vez, también con anticipación alta (con 2 s y dos curvas pegadas, el símbolo de la primera puede seguir pleno cuando suena la segunda).
9. Anotá el valor de anticipación que te resulte cómodo para el próximo ajuste (valor por defecto y rango del futuro deslizador).

## Pendiente (previsto, no implementado)

Botón en el HUD para cambiar de modo, "✕" o menú contextual sobre el overlay desbloqueado para cerrarlo, y comprobar en Windows el nombre de pista largo. Ver UI-SPEC P16 a P18.

## Qué no cubre

No hay release por tag (fase 6). Las pruebas automáticas (`dotnet test`) cubren la lógica del Core —modelo de vista, anti-parpadeo, configuración, servicio con el simulador y con capturas reales opcionales, **aviso de frenada** (cuándo mostrar y ocultar, anti-repetición), **generador del WAV del pitido**, **zip de respaldo con protección zip-slip**, y desde la 5c el **decisor del aviso** (rampa, "ya frenando", referencia principal; en la 5d, T_aviso, anticipación y sincronía pitido/símbolo con el simulador y con capturas reales), las **causas del SIN DATOS** y el **modo Grabar solo vos**— pero **no** la ventana, el aviso dibujado, el sonido real, los cuadros de diálogo, la bandeja, el atajo ni el click-through.
