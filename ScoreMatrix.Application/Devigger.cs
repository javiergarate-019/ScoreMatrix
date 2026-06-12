using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

/// <summary>
/// Removes bookmaker margin (vig) from quoted odds, returning fair probabilities that sum to 1.
/// Each method makes different assumptions about how the margin is distributed across outcomes.
/// </summary>
public static class Devigger
{
    private const double NegativeMarginTolerance = 0.005;
    private const int BisectionIterations = 120;

    /// <summary>
    /// Removes margin from an array of decimal odds using the specified method.
    /// All odds must be greater than 1. Returns fair probabilities summing to 1.
    /// </summary>
    public static double[] RemoveMargin(double[] odds, DevigMethod method)
    {
        if (odds.Length < 2)
        {
            throw new ScoreMatrixValidationException("Se necesitan al menos 2 cuotas para remover el margen.");
        }

        foreach (var o in odds)
        {
            if (o <= 1)
            {
                throw new ScoreMatrixValidationException("Todas las cuotas deben ser mayores que 1.");
            }
        }

        var raw = odds.Select(o => 1.0 / o).ToArray();
        var overround = raw.Sum();

        if (overround < 1 - NegativeMarginTolerance)
        {
            throw new ScoreMatrixValidationException("Las cuotas implican margen negativo para el bookmaker. Revisa los valores antes de calcular.");
        }

        return method switch
        {
            DevigMethod.Power => Power(raw, overround),
            DevigMethod.Shin => Shin(raw, overround),
            DevigMethod.Additive => Additive(raw, overround),
            _ => Proportional(raw, overround)
        };
    }

    /// <summary>
    /// Proportional (standard): divides each implied probability by the overround.
    /// Fastest but slightly overestimates favorites due to favorite-longshot bias.
    /// </summary>
    private static double[] Proportional(double[] raw, double overround)
        => raw.Select(r => r / overround).ToArray();

    /// <summary>
    /// Power (multiplicative): finds exponent k > 1 such that sum((1/o_i)^k) = 1 via bisection.
    /// Reduces the margin proportionally in probability space;
    /// corrects favorite-longshot bias better than proportional.
    /// </summary>
    private static double[] Power(double[] raw, double overround)
    {
        // At k=1, sum = overround > 1; as k -> inf, sum -> 0.
        // Find k in (1, inf) where sum(raw_i^k) = 1.
        var lo = 1.0;
        var hi = 500.0;

        for (var i = 0; i < BisectionIterations; i++)
        {
            var mid = (lo + hi) / 2;
            if (raw.Sum(r => Math.Pow(r, mid)) > 1.0)
                lo = mid;
            else
                hi = mid;
        }

        var k = (lo + hi) / 2;
        var probs = raw.Select(r => Math.Pow(r, k)).ToArray();
        var total = probs.Sum();
        return probs.Select(p => p / total).ToArray();
    }

    /// <summary>
    /// Shin (nonlinear): uses the Shin (1992) insider-trading model.
    /// The formula p_i = (sqrt(z^2 + 4(1-z)*r_i^2/S) - z) / (2(1-z)) corrects
    /// for favorite-longshot bias by assuming z fraction of bets are informed.
    /// z is found via bisection so that sum(p_i) = 1.
    /// </summary>
    private static double[] Shin(double[] raw, double overround)
    {
        // At z=0: sum = sqrt(S) > 1 for S > 1.
        // As z -> 1: sum -> sum(r_i^2)/S which is typically < 1.
        // Bisect on z in (0, 1).
        var lo = 0.0;
        var hi = 1.0 - 1e-10;

        for (var i = 0; i < BisectionIterations; i++)
        {
            var z = (lo + hi) / 2;
            if (ShinSum(raw, overround, z) > 1.0)
                lo = z;
            else
                hi = z;
        }

        var zBest = (lo + hi) / 2;
        var probs = raw.Select(r => ShinProbability(r, overround, zBest)).ToArray();
        var total = probs.Sum();
        return probs.Select(p => p / total).ToArray();
    }

    private static double ShinProbability(double r, double overround, double z)
        => (Math.Sqrt(z * z + 4.0 * (1.0 - z) * r * r / overround) - z) / (2.0 * (1.0 - z));

    private static double ShinSum(double[] raw, double overround, double z)
        => raw.Sum(r => ShinProbability(r, overround, z));

    /// <summary>
    /// Additive: subtracts an equal share of the margin from each implied probability.
    /// p_i = (1/o_i) - (S-1)/n. Preserves absolute differences between outcomes.
    /// Can yield small negatives for big underdogs; clamped to a minimum.
    /// </summary>
    private static double[] Additive(double[] raw, double overround)
    {
        var margin = overround - 1.0;
        var equalShare = margin / raw.Length;
        var probs = raw.Select(r => Math.Max(1e-6, r - equalShare)).ToArray();
        var total = probs.Sum();
        return probs.Select(p => p / total).ToArray();
    }
}
