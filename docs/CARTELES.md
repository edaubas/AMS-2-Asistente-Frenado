# Carteles de frenada

> **Desactivado en la UI por decisión del usuario (fase 5b): se lo leyó como cuenta regresiva.** El overlay ya no muestra el cartel ("200 m"). El concepto sigue vivo en `AMS2Frenado.Core` sin cambios y con sus pruebas: `Carteles.cs`, `OpcionesGuia.DesfaseCartelM` / `CartelesPorDefecto`, el campo `carteles` de `mapa.json` y los campos `CartelIa`, `CartelVos`, `CartelPrincipal` y `CartelSecundario` de `PanelGuia`. Solo dejó de dibujarse. Lo que se ve ahora en la frenada es la barra de progreso hasta el punto de frenado, la comparación IA (rombo naranja) / vos (círculo azul), el nombre de la curva, las velocidades entrada → vértice y los metros que faltan como dato chico y decreciente hasta 0 (ver [UI-SPEC.md](UI-SPEC.md)). Si más adelante se quiere volver a mostrar, el cambio es solo de interfaz (`ConstructorVista`).

El texto de abajo describe cómo se calcula el cartel en el Core; sigue valiendo para quien lo use.

Antes (fase 4 y 5): el número grande del panel de Frenada era el **cartel** del circuito en el que hay que frenar (por ejemplo "200 m"), no el metro de la pista. Los metros que faltan hasta el punto de frenado quedan como dato chico y para la lógica de estados.

## Cálculo

Para cada referencia (IA o vos), con su propio bloque:

1. `distancia = (vértice − frenar_en)` con módulo del largo de pista (sirve cuando la curva cruza la línea de meta), menos `DesfaseCartelM`.
2. Se redondea al cartel más cercano del conjunto permitido.
3. **Empate exacto** (por ejemplo 150 entre 100 y 200): gana el cartel **mayor**, el más conservador (frenar antes). Con el conjunto por defecto: 150 → 200, 250 → 300, 350 → 400, 75 → 100. Por debajo del menor da el menor y por encima del mayor da el mayor.

Ejemplos de Watkins Glen GP / GT3_Gen2 (distancia frenar → vértice): 199 → 200, 302 → 300, 208 → 200, 193 → 200, 196 → 200, 131 → 100, 135 → 100, 147 → 100 (a 47 m de 100 y a 53 m de 200; no es empate).

Si la IA y vos caen en el mismo cartel se muestra uno solo. La referencia principal manda el número grande y la otra va de secundaria (naranja = IA, azul = vos). Si la curva solo tiene una referencia se usa esa.

## Conjunto de carteles por pista

Por defecto `{400, 300, 200, 100, 50}`. Cada pista puede definir los suyos con el campo opcional `carteles` de su `mapa.json` (se edita a mano y el grabador no lo pisa al regrabar el mapa):

```json
{ "formato": 1, "pista": "Watkins Glen", "variante": "GP", "largo": 5440.3, "carteles": [300, 200, 100, 50], "puntos": [] }
```

Se ignoran repetidos y valores no positivos; vacío o ausente usa el conjunto por defecto. El conjunto por defecto de todas las pistas también se cambia con `OpcionesGuia.CartelesPorDefecto`.

## Desfase

`OpcionesGuia.DesfaseCartelM` (por defecto 0). El cartel del juego mide hasta la entrada de la curva y no hasta el vértice, así que probablemente haga falta un desfase; **hay que calibrarlo con una captura del usuario** (sin probar en AMS2). Un valor positivo achica la distancia y uno negativo la agranda.
