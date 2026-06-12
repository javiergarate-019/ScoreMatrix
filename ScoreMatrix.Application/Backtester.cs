using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class Backtester(ScoreMatrixCalculator calculator)
{
    /// <summary>
    /// Runs the full prediction pipeline on each match record and computes calibration metrics.
    /// </summary>
    public BacktestReport Run(
        IReadOnlyList<MatchRecord> records,
        ScoreModelType modelType,
        DevigMethod devigMethod,
        double rho = 0.0,
        bool autoCalibrateRho = false)
    {
        var perMatchRps = new List<double>();
        var perMatchBrier = new List<double>();
        var perMatchLogLoss = new List<double>();
        var forecastedHomeWin = new List<double>();
        var actualHomeWin = new List<bool>();
        var matchResults = new List<BacktestMatchResult>();

        foreach (var record in records)
        {
            ScoreMatrixResult result;
            try
            {
                var input = record.ToMatchInput(modelType, devigMethod, rho, autoCalibrateRho);
                result = calculator.Calculate(input);
            }
            catch
            {
                continue;
            }

            var forecast = new[] { result.HomeWinProbability, result.DrawProbability, result.AwayWinProbability };
            var rps = ForecastMetrics.RankedProbabilityScore(forecast, record.Outcome);
            var brier = ForecastMetrics.BrierScore(result.HomeWinProbability, record.Outcome == 0);
            var logLoss = ForecastMetrics.LogLoss(result.HomeWinProbability, record.Outcome == 0);

            perMatchRps.Add(rps);
            perMatchBrier.Add(brier);
            perMatchLogLoss.Add(logLoss);
            forecastedHomeWin.Add(result.HomeWinProbability);
            actualHomeWin.Add(record.Outcome == 0);

            matchResults.Add(new BacktestMatchResult(
                record.HomeTeam,
                record.AwayTeam,
                result.HomeWinProbability,
                result.DrawProbability,
                result.AwayWinProbability,
                result.LambdaHome,
                result.LambdaAway,
                result.EstimatedDependence,
                record.ActualHomeGoals,
                record.ActualAwayGoals,
                rps,
                brier));
        }

        var calibration = forecastedHomeWin.Count >= 10
            ? ForecastMetrics.CalibrationCurve(forecastedHomeWin, actualHomeWin)
            : [];

        return new BacktestReport(
            matchResults,
            ForecastMetrics.MeanRps(perMatchRps),
            ForecastMetrics.MeanBrier(perMatchBrier),
            ForecastMetrics.MeanLogLoss(perMatchLogLoss),
            calibration,
            modelType,
            devigMethod,
            rho,
            autoCalibrateRho);
    }
}

public sealed record BacktestMatchResult(
    string HomeTeam,
    string AwayTeam,
    double HomeWinProb,
    double DrawProb,
    double AwayWinProb,
    double LambdaHome,
    double LambdaAway,
    double EstimatedDependence,
    int ActualHomeGoals,
    int ActualAwayGoals,
    double Rps,
    double Brier)
{
    public string Score => $"{ActualHomeGoals}-{ActualAwayGoals}";
}

public sealed record BacktestReport(
    IReadOnlyList<BacktestMatchResult> Matches,
    double MeanRps,
    double MeanBrier,
    double MeanLogLoss,
    IReadOnlyList<CalibrationBin> CalibrationCurve,
    ScoreModelType ModelType,
    DevigMethod DevigMethod,
    double Rho,
    bool AutoCalibratedRho);
