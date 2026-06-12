namespace ScoreMatrix.Application;

/// <summary>
/// Calibration metrics for probabilistic football forecasts.
/// All methods operate on per-match 1X2 probability vectors.
/// </summary>
public static class ForecastMetrics
{
    /// <summary>
    /// Ranked Probability Score (RPS) for a single 3-outcome match.
    /// Lower is better. Range: [0, 1].
    /// Uses the ordered outcomes: Home Win (0), Draw (1), Away Win (2).
    /// </summary>
    public static double RankedProbabilityScore(double[] forecast, int actualOutcome)
    {
        if (forecast.Length != 3)
        {
            throw new ArgumentException("RPS requires exactly 3 outcome probabilities.", nameof(forecast));
        }

        if (actualOutcome is < 0 or > 2)
        {
            throw new ArgumentException("actualOutcome must be 0 (home), 1 (draw), or 2 (away).", nameof(actualOutcome));
        }

        // Cumulative forecast and observation
        var rps = 0.0;
        var cumulativeForecast = 0.0;
        var cumulativeActual = 0.0;

        for (var i = 0; i < 2; i++)  // only 2 steps needed for 3 outcomes
        {
            cumulativeForecast += forecast[i];
            cumulativeActual += i <= actualOutcome - 1 ? 1.0 : 0.0;
            rps += Math.Pow(cumulativeForecast - cumulativeActual, 2);
        }

        return rps / 2.0;
    }

    /// <summary>
    /// Brier Score for a single binary event (e.g. "home wins").
    /// Lower is better. Range: [0, 1].
    /// </summary>
    public static double BrierScore(double forecast, bool actual)
        => Math.Pow(forecast - (actual ? 1.0 : 0.0), 2);

    /// <summary>
    /// Log-loss for a single event probability.
    /// Lower is better.
    /// </summary>
    public static double LogLoss(double forecast, bool actual)
    {
        forecast = Math.Clamp(forecast, 1e-15, 1 - 1e-15);
        return actual ? -Math.Log(forecast) : -Math.Log(1 - forecast);
    }

    /// <summary>
    /// Aggregates per-match RPS into mean RPS over the dataset.
    /// </summary>
    public static double MeanRps(IReadOnlyList<double> perMatchRps)
        => perMatchRps.Count == 0 ? double.NaN : perMatchRps.Average();

    /// <summary>
    /// Aggregates per-match Brier scores into a mean.
    /// </summary>
    public static double MeanBrier(IReadOnlyList<double> perMatchBrier)
        => perMatchBrier.Count == 0 ? double.NaN : perMatchBrier.Average();

    /// <summary>
    /// Aggregates per-match log-loss into a mean.
    /// </summary>
    public static double MeanLogLoss(IReadOnlyList<double> perMatchLogLoss)
        => perMatchLogLoss.Count == 0 ? double.NaN : perMatchLogLoss.Average();

    /// <summary>
    /// Builds a calibration curve by binning forecasts and comparing to observed frequencies.
    /// Returns a list of (midpoint, predicted, observed, count) for each bin.
    /// </summary>
    public static IReadOnlyList<CalibrationBin> CalibrationCurve(
        IReadOnlyList<double> forecasts,
        IReadOnlyList<bool> actuals,
        int bins = 10)
    {
        if (forecasts.Count != actuals.Count)
        {
            throw new ArgumentException("forecasts and actuals must have the same length.");
        }

        var result = new List<CalibrationBin>();

        for (var b = 0; b < bins; b++)
        {
            var low = (double)b / bins;
            var high = (double)(b + 1) / bins;
            var mid = (low + high) / 2;

            var inBin = forecasts
                .Zip(actuals, (f, a) => (f, a))
                .Where(x => x.f >= low && (x.f < high || (b == bins - 1 && x.f <= 1)))
                .ToList();

            if (inBin.Count == 0) continue;

            var avgForecast = inBin.Average(x => x.f);
            var observedFreq = inBin.Count(x => x.a) / (double)inBin.Count;
            result.Add(new CalibrationBin(mid, avgForecast, observedFreq, inBin.Count));
        }

        return result;
    }
}

public sealed record CalibrationBin(double Midpoint, double MeanForecast, double ObservedFrequency, int Count);
