# Carteles de frenada

El número grande del panel de Frenada es el **cartel** del circuito en el que hay que frenar (por ejemplo "200 m"), no el metro de la pista. Los metros que faltan hasta el punto de frenado quedan como dato chico y para la lógica de estados.

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
