using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

/// <summary>
/// Negative Binomial score model.
/// Each team's goal count follows NegBin(r, p) instead of Poisson(lambda).
/// The dispersion parameter controls the variance-to-mean ratio:
/// - dispersion = 0 → Poisson (no overdispersion).
/// - dispersion > 0 → variance > mean (fat tail, more 0s and high scores).
/// The dispersion field in [-0.25, 0.25] is mapped to an overdispersion factor.
/// Teams remain independent (as in the base Poisson model).
/// </summary>
public sealed class NegativeBinomialScoreModel(double dispersionRaw) : IScoreModel
{
    public IReadOnlyList<ScoreProbability> Calculate(double lambdaHome, double lambdaAway, int maxGoals)
    {
        PoissonScoreModel.Validate(lambdaHome, lambdaAway, maxGoals);

        if (dispersionRaw is < -0.25 or > 0.25)
        {
            throw new ScoreMatrixValidationException("El parametro de dispersion debe estar entre -0.25 y 0.25.");
        }

        // Map dispersionRaw to overdispersion: we use only the positive range.
        // overdispersion phi >= 0; at phi=0 we recover Poisson.
        // phi = max(0, dispersionRaw) * 2  (so max phi = 0.5 at dispersionRaw=0.25)
        var phi = Math.Max(0, dispersionRaw) * 2.0;

        var scores = new List<ScoreProbability>((maxGoals + 1) * (maxGoals + 1));

        for (var i = 0; i <= maxGoals; i++)
        {
            var ph = NegBinProbability(i, lambdaHome, phi);
            for (var j = 0; j <= maxGoals; j++)
            {
                var pa = NegBinProbability(j, lambdaAway, phi);
                scores.Add(new ScoreProbability(i, j, ph * pa));
            }
        }

        var total = scores.Sum(s => s.Probability);
        if (total <= 0) return scores;

        // Normalise for truncation
        var baseMass = ComputeBaseMass(lambdaHome, lambdaAway, maxGoals, phi);
        return scores.Select(s => s with { Probability = s.Probability / total * baseMass }).ToArray();
    }

    /// <summary>
    /// Negative Binomial PMF parameterised by mean (mu) and overdispersion (phi >= 0).
    /// Var = mu + phi * mu^2. When phi = 0, this is Poisson(mu).
    /// Uses NegBin(r, p) where r = 1/phi and p = r/(r + mu).
    /// </summary>
    private static double NegBinProbability(int k, double mu, double phi)
    {
        if (phi < 1e-10)
        {
            return PoissonMath.Probability(k, mu);
        }

        var r = 1.0 / phi;
        var p = r / (r + mu);
        return NegBinPmf(k, r, p);
    }

    private static double NegBinPmf(int k, double r, double p)
    {
        // P(X=k) = C(k+r-1, k) * p^r * (1-p)^k  (using log for stability)
        var logProb = LogGammaRatioCombination(k, r) + r * Math.Log(p) + k * Math.Log(1 - p);
        return Math.Max(0, Math.Exp(logProb));
    }

    /// <summary>log[ Gamma(k+r) / (k! * Gamma(r)) ] = log C(k+r-1, k)</summary>
    private static double LogGammaRatioCombination(int k, double r)
    {
        var result = 0.0;
        for (var i = 0; i < k; i++)
        {
            result += Math.Log(r + i) - Math.Log(i + 1);
        }

        return result;
    }

    private static double ComputeBaseMass(double lambdaHome, double lambdaAway, int maxGoals, double phi)
    {
        var mass = 0.0;
        for (var i = 0; i <= maxGoals; i++)
        {
            var ph = NegBinProbability(i, lambdaHome, phi);
            for (var j = 0; j <= maxGoals; j++)
            {
                mass += ph * NegBinProbability(j, lambdaAway, phi);
            }
        }

        return mass;
    }
}
