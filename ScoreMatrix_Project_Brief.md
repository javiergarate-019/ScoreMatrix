# Proyecto: ScoreMatrix / GoalGrid / MatchLambda

## Objetivo general

Crear una aplicación desktop para calcular probabilidades y cuotas justas de resultados exactos en partidos de fútbol.

La aplicación debe permitir ingresar datos básicos del partido y generar una matriz de posibles marcadores, por ejemplo:

| Local \ Visitante | 0 | 1 | 2 | 3 | 4 | 5 |
|---|---:|---:|---:|---:|---:|---:|
| 0 | 0-0 | 0-1 | 0-2 | 0-3 | 0-4 | 0-5 |
| 1 | 1-0 | 1-1 | 1-2 | 1-3 | 1-4 | 1-5 |
| 2 | 2-0 | 2-1 | 2-2 | 2-3 | 2-4 | 2-5 |
| 3 | 3-0 | 3-1 | 3-2 | 3-3 | 3-4 | 3-5 |

Cada celda debe poder mostrar:

- Probabilidad del resultado exacto.
- Cuota justa equivalente, calculada como `1 / probabilidad`.

---

## Entradas principales

La aplicación debería permitir dos modos de entrada.

### Modo 1: desde odds 1X2

El usuario ingresa las cuotas del mercado principal:

- Cuota gana local.
- Cuota empate.
- Cuota gana visitante.

Opcionalmente:

- Nombre del equipo local.
- Nombre del equipo visitante.

A partir de esas cuotas, la aplicación debe:

1. Convertir las odds a probabilidades implícitas.
2. Corregir el margen del bookmaker, normalizando las probabilidades para que sumen 1.
3. Encontrar los valores esperados de goles de cada equipo:
   - `lambdaLocal`
   - `lambdaVisitante`

Estos valores deben obtenerse mediante optimización numérica.

La idea es encontrar `lambdaLocal` y `lambdaVisitante` tales que la matriz de resultados generada por el modelo produzca probabilidades agregadas similares a las del mercado 1X2:

- Probabilidad de victoria local.
- Probabilidad de empate.
- Probabilidad de victoria visitante.

Es decir:

```text
P(local gana)     = suma de celdas donde golesLocal > golesVisitante
P(empate)         = suma de celdas donde golesLocal = golesVisitante
P(visitante gana) = suma de celdas donde golesLocal < golesVisitante
```

La optimización debe minimizar el error entre esas tres probabilidades calculadas y las probabilidades derivadas de las odds 1X2.

Ejemplo de función de error:

```text
error =
    (pLocalModelo - pLocalMercado)^2
  + (pEmpateModelo - pEmpateMercado)^2
  + (pVisitanteModelo - pVisitanteMercado)^2
```

---

### Modo 2: desde goles esperados

El usuario ingresa directamente:

- Goles esperados del equipo local por partido.
- Goles esperados del equipo visitante por partido.

Estos valores se usan directamente como:

```text
lambdaLocal
lambdaVisitante
```

En este modo no hace falta optimización.

---

## Modelo base: Poisson simple

El primer modelo a implementar debe ser Poisson independiente.

Supuestos:

```text
GolesLocal     ~ Poisson(lambdaLocal)
GolesVisitante ~ Poisson(lambdaVisitante)
```

La probabilidad de un resultado exacto `i-j` se calcula como:

```text
P(i-j) = Poisson(i, lambdaLocal) * Poisson(j, lambdaVisitante)
```

La función de Poisson es:

```text
P(X = k) = exp(-lambda) * lambda^k / k!
```

Ejemplo:

```text
P(2-1) = Poisson(2, lambdaLocal) * Poisson(1, lambdaVisitante)
```

La matriz debería calcularse hasta una cantidad configurable de goles, por ejemplo:

- 5 goles.
- 6 goles.
- 10 goles.

También debería contemplarse una fila/columna o resumen para “otros resultados” si se desea capturar la probabilidad restante fuera del rango mostrado.

---

## Modelo alternativo: Dixon-Coles

La aplicación debería ofrecer una opción para usar un ajuste tipo Dixon-Coles.

Este ajuste parte de la matriz Poisson simple, pero corrige la dependencia en marcadores bajos, especialmente:

- 0-0
- 1-0
- 0-1
- 1-1

El objetivo es que el modelo refleje mejor la realidad del fútbol, donde los goles de ambos equipos no son completamente independientes y los marcadores bajos suelen comportarse distinto a lo que predice una Poisson independiente pura.

La fórmula conceptual sería:

```text
P_DixonColes(i-j) =
    Poisson(i, lambdaLocal)
  * Poisson(j, lambdaVisitante)
  * ajusteDixonColes(i, j, rho)
```

Donde `rho` es un parámetro configurable.

La implementación puede empezar con un parámetro manual `rho`, por ejemplo:

```text
rho = -0.05
rho =  0.00
rho =  0.05
```

Más adelante se podría calibrar `rho` con datos históricos.

---

## Ajustes dinámicos opcionales

Además del modelo Poisson simple y Dixon-Coles, la aplicación debería permitir algunos ajustes opcionales para explorar modelos más realistas.

La idea es considerar que un gol cambia el estado del partido. Después de un gol, la probabilidad de que ocurra otro gol puede subir levemente, porque:

- El equipo que recibió el gol puede atacar más.
- El equipo que convirtió puede ganar confianza o encontrar espacios de contraataque.
- El partido puede abrirse tácticamente.

Estos ajustes no tienen que ser perfectos en la primera versión, pero deberían estar contemplados como parámetros opcionales.

### Ajuste 1: mejora del equipo que recibe un gol

Parámetro sugerido:

```text
factorAfterConceding
```

Ejemplo:

```text
factorAfterConceding = 1.05
```

Interpretación:

- Si un equipo recibe un gol, su intensidad ofensiva aumenta un 5%.
- Esto intenta modelar que el equipo que va perdiendo se abre o ataca más.

### Ajuste 2: mejora del equipo que convierte un gol

Parámetro sugerido:

```text
factorAfterScoring
```

Ejemplo:

```text
factorAfterScoring = 1.03
```

Interpretación:

- Si un equipo convierte un gol, su intensidad ofensiva aumenta un 3%.
- Esto intenta modelar momentum, confianza o posibilidad de contraataque.

### Ajuste 3: aumento general después de cualquier gol

Parámetro sugerido:

```text
factorPostGoalTotal
```

Ejemplo:

```text
factorPostGoalTotal = 1.04
```

Interpretación:

- Durante algunos minutos después de un gol, la intensidad total del partido aumenta.
- Esto modela que el partido se vuelve más abierto tras un gol.

### Duración del efecto post-gol

Parámetro sugerido:

```text
postGoalEffectMinutes = 5
```

Interpretación:

- El efecto del gol dura 5 minutos.
- Luego las intensidades vuelven gradualmente al valor base.

---

## Dos enfoques posibles para los ajustes dinámicos

### Enfoque A: modelo estático aproximado

Aplicar factores correctivos directamente sobre la matriz final.

Ventaja:

- Más simple.
- Más rápido.
- Más fácil de implementar en una primera versión.

Desventaja:

- Menos riguroso.
- Puede ser difícil conservar la coherencia matemática de las probabilidades.

### Enfoque B: simulación minuto a minuto

Simular miles de partidos minuto a minuto.

Para cada simulación:

1. Iniciar el partido 0-0.
2. Para cada minuto:
   - Calcular intensidad del local.
   - Calcular intensidad del visitante.
   - Aplicar factores según estado del partido:
     - Equipo va ganando.
     - Equipo va perdiendo.
     - Hubo gol reciente.
     - Equipo convirtió recientemente.
     - Equipo recibió recientemente.
   - Sortear si ocurre gol local o visitante en ese minuto.
3. Al terminar el partido, registrar el resultado exacto.
4. Repetir muchas veces.
5. Construir la matriz de probabilidades por frecuencia observada.

Ventaja:

- Mucho más flexible.
- Permite modelar efectos dependientes del estado del partido.

Desventaja:

- Requiere simulación Monte Carlo.
- El resultado tiene ruido estadístico.
- Necesita muchas simulaciones para obtener estabilidad.

Para primera versión, se puede implementar Poisson simple y Dixon-Coles. Los ajustes dinámicos pueden quedar como segunda etapa.

---

## Salida de la aplicación

La aplicación debe mostrar una matriz de resultados exactos.

Debe permitir alternar entre dos vistas:

### Vista de probabilidades

Ejemplo:

| Local \ Visitante | 0 | 1 | 2 | 3 |
|---|---:|---:|---:|---:|
| 0 | 8.20% | 9.02% | 4.96% | 1.82% |
| 1 | 11.48% | 12.63% | 6.95% | 2.55% |
| 2 | 8.04% | 8.84% | 4.86% | 1.78% |
| 3 | 3.75% | 4.12% | 2.27% | 0.83% |

### Vista de odds justas

Ejemplo:

| Local \ Visitante | 0 | 1 | 2 | 3 |
|---|---:|---:|---:|---:|
| 0 | 12.20 | 11.09 | 20.16 | 54.95 |
| 1 | 8.71 | 7.92 | 14.39 | 39.22 |
| 2 | 12.44 | 11.31 | 20.58 | 56.18 |
| 3 | 26.67 | 24.27 | 44.05 | 120.48 |

También sería útil mostrar resumen agregado:

- Probabilidad victoria local.
- Probabilidad empate.
- Probabilidad victoria visitante.
- Probabilidad Over 0.5, 1.5, 2.5, 3.5.
- Probabilidad Under 0.5, 1.5, 2.5, 3.5.
- Probabilidad Both Teams To Score: Sí / No.
- Probabilidad de 0-0.
- Probabilidad de 1-1.
- Probabilidad de score más probable.

---

## Consideraciones sobre odds

La cuota justa se calcula como:

```text
odds = 1 / probability
```

Donde `probability` debe estar en formato decimal.

Ejemplo:

```text
probability = 0.125
odds = 8.00
```

La aplicación debería dejar claro que estas son odds justas, sin margen de bookmaker.

Opcionalmente, se podría permitir aplicar un margen:

```text
oddsConMargen = odds / (1 + margen)
```

o distribuir un overround sobre la matriz.

---

## Optimización desde odds 1X2

Cuando el usuario ingresa odds 1X2:

```text
oddsLocal
oddsEmpate
oddsVisitante
```

Primero calcular probabilidades implícitas:

```text
pLocalBruta     = 1 / oddsLocal
pEmpateBruta    = 1 / oddsEmpate
pVisitanteBruta = 1 / oddsVisitante
```

Luego normalizar:

```text
suma = pLocalBruta + pEmpateBruta + pVisitanteBruta

pLocalMercado     = pLocalBruta / suma
pEmpateMercado    = pEmpateBruta / suma
pVisitanteMercado = pVisitanteBruta / suma
```

Después buscar `lambdaLocal` y `lambdaVisitante`.

Restricciones sugeridas:

```text
lambdaLocal > 0
lambdaVisitante > 0
lambdaLocal <= 6
lambdaVisitante <= 6
```

Valores iniciales sugeridos:

```text
lambdaLocal = 1.4
lambdaVisitante = 1.1
```

o algún cálculo aproximado basado en favoritismo.

La optimización puede hacerse con:

- Nelder-Mead.
- Levenberg-Marquardt.
- BFGS.
- Cualquier optimizador no lineal disponible.

La función objetivo debe minimizar el error entre el mercado 1X2 y el modelo.

---

## Arquitectura sugerida

Separar la aplicación en capas simples:

```text
UI Desktop
    |
    v
Application / Services
    |
    v
Domain / Math Engine
```

### Clases sugeridas

```text
MatchInput
```

Propiedades:

```text
HomeTeamName
AwayTeamName
InputMode
HomeOdds
DrawOdds
AwayOdds
HomeExpectedGoals
AwayExpectedGoals
ModelType
MaxGoals
DisplayMode
UseDixonColes
DixonColesRho
UseDynamicAdjustments
FactorAfterScoring
FactorAfterConceding
FactorPostGoalTotal
PostGoalEffectMinutes
```

```text
ScoreProbability
```

Propiedades:

```text
HomeGoals
AwayGoals
Probability
FairOdds
```

```text
ScoreMatrixResult
```

Propiedades:

```text
HomeTeamName
AwayTeamName
LambdaHome
LambdaAway
Scores
HomeWinProbability
DrawProbability
AwayWinProbability
OverUnderSummary
BothTeamsToScoreYes
BothTeamsToScoreNo
MostLikelyScore
```

### Servicios sugeridos

```text
OddsConverter
```

Responsabilidad:

- Convertir odds a probabilidades implícitas.
- Quitar margen normalizando.

```text
PoissonScoreModel
```

Responsabilidad:

- Calcular matriz Poisson simple.

```text
DixonColesScoreModel
```

Responsabilidad:

- Calcular matriz con ajuste Dixon-Coles.

```text
LambdaOptimizer
```

Responsabilidad:

- Inferir `lambdaLocal` y `lambdaVisitante` desde odds 1X2.

```text
ScoreMatrixCalculator
```

Responsabilidad:

- Coordinar el cálculo completo.
- Devolver `ScoreMatrixResult`.

```text
MonteCarloMatchSimulator
```

Responsabilidad futura:

- Simular partidos minuto a minuto con ajustes dinámicos.

---

## Validaciones

Validar que:

- Las odds sean mayores que 1.
- Los lambdas sean mayores que 0.
- `MaxGoals` sea razonable, por ejemplo entre 3 y 15.
- Los factores dinámicos sean positivos.
- Las probabilidades de la matriz sumen aproximadamente 1, o mostrar probabilidad residual si se trunca la matriz.
- Las odds calculadas no dividan por cero.

---

## Primera versión recomendada

Para una primera versión, implementar:

1. UI desktop simple.
2. Entrada por odds 1X2.
3. Entrada por lambdas manuales.
4. Conversión de odds a probabilidades sin margen.
5. Optimización de `lambdaLocal` y `lambdaVisitante`.
6. Matriz Poisson simple.
7. Vista de probabilidad / odds justas.
8. Resumen 1X2 derivado de la matriz.
9. Over/Under 2.5.
10. Both Teams To Score.
11. Opción de exportar a CSV.

Luego agregar:

1. Dixon-Coles.
2. Ajustes dinámicos.
3. Simulación Monte Carlo.
4. Comparación contra odds reales de resultado exacto.
5. Detección de posible valor esperado positivo.

---

## Notas importantes

- El objetivo no es garantizar apuestas ganadoras.
- El objetivo es calcular probabilidades justas según un modelo matemático.
- Las cuotas de bookmakers incluyen margen.
- Los mercados de resultado exacto suelen tener márgenes altos.
- Los resultados de baja probabilidad tienen varianza muy alta.
- El modelo debe mostrar claramente la diferencia entre probabilidad estimada y cuota justa.

---

## Posibles nombres del proyecto

Opciones:

- ScoreMatrix
- GoalGrid
- MatchLambda
- ExactScoreLab
- FairScore
- OddsMatrix
- PoissonPitch

Nombre sugerido inicialmente:

```text
ScoreMatrix
```

Porque el núcleo de la aplicación es una matriz de probabilidades de resultado exacto.
