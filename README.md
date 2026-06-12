# ScoreMatrix

ScoreMatrix es una aplicacion desktop para Windows que estima probabilidades de resultados exactos de futbol y sus cuotas justas equivalentes a partir de un modelo matematico.

La app puede partir de cuotas 1X2 de mercado o de goles esperados ingresados manualmente. Luego construye una matriz de marcadores como `0-0`, `1-0`, `2-1`, etc. Cada celda puede verse como probabilidad o como cuota decimal justa.

Version actual: `0.05`

## Funcionalidades

- UI desktop WinForms para Windows.
- Entrada desde cuotas decimales 1X2: victoria local, empate, victoria visitante.
- Importacion opcional de cuotas del Mundial 2026 desde [The Odds API](https://the-odds-api.com/liveapi/guides/v4/): 1X2, Over/Under y BTTS cuando esten disponibles (consenso sin margen entre bookmakers).
- Calibracion opcional con cuotas Over/Under en linea configurable (default 2.5).
- Calibracion opcional con cuotas Both Teams To Score: Si/No.
- Entrada desde goles esperados manuales: lambda local y lambda visitante.
- **Cuatro metodos de de-vig**: Proporcional (default), Power (corrige sesgo favorito-longshot), Shin (modelo de insiders, mas preciso), Aditivo (resta igual margen).
- Inferencia de lambdas con **optimizador Nelder-Mead** y objetivo **KL divergence** (mas preciso que minimos cuadrados).
- **Cuatro modelos de score**:
  - Poisson: modelo base con distribuciones independientes.
  - Dixon-Coles: ajusta marcadores bajos con parametro rho.
  - Bivariado Poisson: captura correlacion positiva entre goles de ambos equipos.
  - Binomial Negativa: marginales sobredispersas con colas mas gruesas.
- **Auto-calibracion del parametro de dependencia** (rho/lambda3): cuando hay O/U o BTTS, el optimizador estima el parametro de dependencia en lugar de usar el valor manual.
- Ajuste Dixon-Coles opcional para marcadores bajos.
- Rango visible configurable entre 3 y 15 goles.
- Vista de matriz en probabilidades o cuotas justas.
- Vista de cuotas con margen comercial configurable.
- Ranking Penca por valor esperado de puntaje con reglas editables (default: exacto 7, resultado 2, goles 1).
- Resumen agregado:
  - Victoria local, empate, victoria visitante.
  - Over/Under 0.5, 1.5, 2.5 y 3.5.
  - Both Teams To Score: Si/No.
  - 0-0 y 1-1.
  - Marcador mas probable.
  - Probabilidad cubierta por la grilla.
  - Probabilidad fuera de la grilla.
- Exportacion a CSV.
- **Modulo de Backtest**: evalua el modelo sobre un CSV de partidos historicos y calcula **RPS** (Ranked Probability Score), **Brier Score**, **Log-Loss** y **curva de calibracion**. Permite comparar configuraciones para tunear el modelo.
- Ventana de ayuda integrada con explicacion del modelo y los conceptos.

## Requisitos

- Windows.
- .NET 10 SDK.

El proyecto WinForms usa:

```text
net10.0-windows
```

## Compilar Y Ejecutar

Desde la raiz del repositorio:

```powershell
dotnet build
dotnet run --project ScoreMatrix.WinForms\ScoreMatrix.WinForms.csproj
```

Tambien se puede ejecutar el binario generado despues de compilar:

```powershell
ScoreMatrix.WinForms\bin\Debug\net10.0-windows\ScoreMatrix.WinForms.exe
```

## Como Funciona

### 1. De cuotas a probabilidades sin margen

Cuando se usan cuotas 1X2, ScoreMatrix convierte primero cada cuota decimal en probabilidad implicita:

```text
pLocalBruta = 1 / cuotaLocal
pEmpateBruta = 1 / cuotaEmpate
pVisitanteBruta = 1 / cuotaVisitante
```

Luego quita el margen del bookmaker con normalizacion proporcional:

```text
suma = pLocalBruta + pEmpateBruta + pVisitanteBruta

pLocal = pLocalBruta / suma
pEmpate = pEmpateBruta / suma
pVisitante = pVisitanteBruta / suma
```

Si las probabilidades implicitas brutas suman menos que `1`, la app bloquea el calculo porque eso implicaria margen negativo para el bookmaker.

### 2. De probabilidades 1X2 a lambdas

La app infiere:

```text
lambdaLocal
lambdaVisitante
```

Estos valores representan los goles esperados del equipo local y visitante.

El optimizador usa un **algoritmo Nelder-Mead** (simplex) sembrado desde una busqueda en grilla. Su objetivo es minimizar la **KL divergence** entre las probabilidades del mercado y las del modelo Poisson:

```text
error = KL(mercado || modelo) = sum_i p_mercado_i * log(p_mercado_i / p_modelo_i)
```

A diferencia del error cuadratico, el KL divergence es la medida teoricamente correcta para comparar distribuciones de probabilidad y penaliza mejor los desvios lejanos al mercado.

Si se activa `Usar O/U`, la app tambien incluye la KL divergence del mercado Over/Under al objetivo. Si se activa `Usar BTTS`, tambien incluye el mercado BTTS.

Cuando el modelo no es Poisson y se activa `Auto-calibrar`, el optimizador tambien estima el parametro de dependencia (rho para Dixon-Coles, lambda3 para Bivariado) como un tercer parametro libre.

### 3. Matriz Poisson

El modelo base asume distribuciones Poisson independientes:

```text
Goles local ~ Poisson(lambdaLocal)
Goles visitante ~ Poisson(lambdaVisitante)
```

La probabilidad de un marcador exacto se calcula asi:

```text
P(i-j) = Poisson(i, lambdaLocal) * Poisson(j, lambdaVisitante)
```

La cuota justa se calcula como:

```text
cuotaJusta = 1 / probabilidad
```

Tambien se puede aplicar un margen comercial configurable a la cuota justa:

```text
cuotaConMargen = cuotaJusta / (1 + margen / 100)
```

Este margen solo cambia la visualizacion y la exportacion; no altera las probabilidades ni los lambdas del modelo.

### 4. Ajuste Dixon-Coles

Dixon-Coles parte de la matriz Poisson y ajusta marcadores bajos:

```text
0-0
1-0
0-1
1-1
```

El parametro manual `rho` controla la direccion y fuerza del ajuste.

- `rho = 0`: igual que Poisson simple.
- `rho < 0`: segun el factor aplicado, sube `0-0` y `1-1`, y baja `1-0` y `0-1`.
- `rho > 0`: segun el factor aplicado, baja `0-0` y `1-1`, y sube `1-0` y `0-1`.

Luego ScoreMatrix renormaliza la matriz para conservar la masa de probabilidad base, asi que el efecto visible puede variar levemente en la grilla completa.

## Limitaciones Importantes

ScoreMatrix es una herramienta de modelado, no un sistema para garantizar apuestas ganadoras.

Las cuotas 1X2 por si solas no definen completamente el total esperado de goles. Dos partidos pueden tener probabilidades 1X2 parecidas pero perfiles de Over/Under o BTTS distintos. La app permite usar Over/Under (linea configurable) y BTTS para mejorar esa calibracion. Futuras versiones podrian sumar mercados adicionales como:

- Totales asiaticos.
- Precios de mercado de resultado exacto.

Las cuotas justas mostradas por ScoreMatrix no incluyen margen de bookmaker, liquidez, sesgos de mercado ni ajustes comerciales.

## Estructura Del Proyecto

```text
ScoreMatrix.Domain
  Modelos y enums compartidos.

ScoreMatrix.Application
  Conversion de odds, optimizacion de lambdas, modelos de score, calculadora y exportacion CSV.

ScoreMatrix.WinForms
  UI desktop, ventana de ayuda e iconos.
```

## Notas Del Repositorio

- La version `0.05` agrega cuatro metodos de de-vig (Proporcional, Power, Shin, Aditivo), optimizador Nelder-Mead con objetivo KL divergence, dos nuevos modelos (Bivariado Poisson, Binomial Negativa), auto-calibracion del parametro de dependencia desde mercados O/U y BTTS, y el modulo de Backtest con metricas RPS/Brier/LogLoss y curva de calibracion.
- La version `0.04` agrega importacion opcional de O/U y BTTS desde The Odds API y linea Over/Under configurable en la calibracion.
- La version `0.03` agrega importacion opcional de cuotas 1X2 del Mundial 2026 desde The Odds API y ranking Penca por valor esperado de puntaje con reglas editables.
- La version `0.02` agrega calibracion opcional con Over/Under 2.5 y BTTS, margen comercial para odds y mejoras visuales.
