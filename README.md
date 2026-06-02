# ScoreMatrix

ScoreMatrix is a Windows desktop app for estimating football exact-score probabilities and fair odds from a simple mathematical model.

The app can start from either 1X2 market odds or manually entered expected goals, then builds a score matrix such as `0-0`, `1-0`, `2-1`, etc. Each cell can be viewed as either a probability or the equivalent fair decimal odd.

Current version: `0.01`

## Features

- WinForms desktop UI for Windows.
- Input from 1X2 decimal odds: home win, draw, away win.
- Input from manual expected goals: home lambda and away lambda.
- Proportional no-margin conversion for 1X2 odds.
- Lambda inference from 1X2 probabilities.
- Poisson exact-score model.
- Optional Dixon-Coles adjustment for low scores.
- Configurable visible score range from 3 to 15 goals.
- Probability and fair-odds matrix views.
- Aggregated summary:
  - Home win, draw, away win.
  - Over/Under 0.5, 1.5, 2.5, 3.5.
  - Both Teams To Score: Yes/No.
  - 0-0 and 1-1.
  - Most likely score.
  - Probability covered by the visible grid.
  - Probability outside the visible grid.
- CSV export.
- Built-in Help window explaining the model and terminology.

## Requirements

- Windows.
- .NET 10 SDK.

The WinForms project targets:

```text
net10.0-windows
```

## Build And Run

From the repository root:

```powershell
dotnet build
dotnet run --project ScoreMatrix.WinForms\ScoreMatrix.WinForms.csproj
```

You can also run the built executable after building:

```powershell
ScoreMatrix.WinForms\bin\Debug\net10.0-windows\ScoreMatrix.WinForms.exe
```

## How It Works

### 1. Odds To No-Margin Probabilities

When using 1X2 odds, ScoreMatrix first converts decimal odds into implied probabilities:

```text
pHomeRaw = 1 / homeOdds
pDrawRaw = 1 / drawOdds
pAwayRaw = 1 / awayOdds
```

Then it removes bookmaker margin with proportional normalization:

```text
sum = pHomeRaw + pDrawRaw + pAwayRaw

pHome = pHomeRaw / sum
pDraw = pDrawRaw / sum
pAway = pAwayRaw / sum
```

If the raw implied probabilities sum to less than `1`, the app blocks the calculation because that would imply a negative bookmaker margin.

### 2. 1X2 Probabilities To Lambdas

The app infers:

```text
lambdaHome
lambdaAway
```

These represent expected goals for the home and away teams.

The optimizer tests lambda pairs, builds an internal Poisson score matrix for each pair, then aggregates it into:

```text
P(home win) = sum of scores where home goals > away goals
P(draw) = sum of scores where home goals == away goals
P(away win) = sum of scores where home goals < away goals
```

It chooses the pair that minimizes:

```text
error =
  (homeModel - homeMarket)^2
+ (drawModel - drawMarket)^2
+ (awayModel - awayMarket)^2
```

The current optimizer uses a broad grid search followed by local refinement.

### 3. Poisson Score Matrix

The base model assumes independent Poisson goal distributions:

```text
Home goals ~ Poisson(lambdaHome)
Away goals ~ Poisson(lambdaAway)
```

Exact-score probability:

```text
P(i-j) = Poisson(i, lambdaHome) * Poisson(j, lambdaAway)
```

Fair odds are calculated as:

```text
fairOdds = 1 / probability
```

### 4. Dixon-Coles Adjustment

Dixon-Coles starts from the Poisson matrix and adjusts low-score outcomes:

```text
0-0
1-0
0-1
1-1
```

The manual `rho` parameter controls the direction and strength of the adjustment.

- `rho = 0`: same as simple Poisson.
- `rho < 0`: usually increases `0-0`, `1-0`, `0-1` and decreases `1-1`.
- `rho > 0`: usually decreases `0-0`, `1-0`, `0-1` and increases `1-1`.

## Important Limitations

ScoreMatrix is a modeling tool, not a betting system.

1X2 odds alone do not fully define the expected total goals. Different matches can have similar 1X2 probabilities but different Over/Under profiles. For stronger calibration, future versions should include optional markets such as:

- Over/Under 2.5.
- Both Teams To Score.
- Asian total goals.
- Correct-score market prices.

The fair odds shown by ScoreMatrix do not include bookmaker margin, liquidity effects, market bias, or commercial pricing adjustments.

## Project Structure

```text
ScoreMatrix.Domain
  Shared models and enums.

ScoreMatrix.Application
  Odds conversion, lambda optimization, score models, calculator, CSV export.

ScoreMatrix.WinForms
  Windows desktop UI, help window, icon assets.
```

## Repository Notes

- `ScoreMatrix_Project_Brief.md` contains the original project brief and future ideas.
- Version `0.01` is the first committed desktop version.

