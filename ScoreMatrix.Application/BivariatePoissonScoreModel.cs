using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

/// <summary>
/// Bivariate Poisson score model (Maher 1982 / Karlis & Ntzoufras 2003).
/// Models correlated goal distributions using a covariance parameter lambda3.
/// P(X=i, Y=j) = exp(-(λ1+λ2+λ3)) * (λ1^i/i!) * (λ2^j/j!) * sum_k C(i,k)*C(j,k)*k!(λ3/(λ1*λ2))^k
/// Reduces to independent Poisson when lambda3=0.
/// </summary>
public sealed class BivariatePoissonScoreModel(double lambda3Raw) : IScoreModel
{
    // lambda3 is expressed as a fraction of sqrt(lambda1*lambda2) for intuitive scale [-0.25, 0.25]
    // The actual covariance term lambda3 = |lambda3Raw| * sqrt(lambda1 * lambda2)
    // lambda3Raw < 0 is mapped to 0 (negative covariance not supported; use DixonColes for that)

    public IReadOnlyList<ScoreProbability> Calculate(double lambdaHome, double lambdaAway, int maxGoals)
    {
        PoissonScoreModel.Validate(lambdaHome, lambdaAway, maxGoals);

        if (lambda3Raw is < -0.25 or > 0.25)
        {
            throw new ScoreMatrixValidationException("El parametro de dependencia Bivariado debe estar entre -0.25 y 0.25.");
        }

        // Covariance = lambda3Raw * sqrt(lambdaHome * lambdaAway), clamped to non-negative
        var lambda3 = Math.Max(0, lambda3Raw) * Math.Sqrt(lambdaHome * lambdaAway);
        var lambda1 = lambdaHome - lambda3;
        var lambda2 = lambdaAway - lambda3;

        // If lambda3 pushes lambda1 or lambda2 negative, clamp and reduce lambda3
        if (lambda1 < 0.001 || lambda2 < 0.001)
        {
            lambda3 = Math.Min(lambdaHome - 0.001, lambdaAway - 0.001);
            lambda3 = Math.Max(0, lambda3);
            lambda1 = lambdaHome - lambda3;
            lambda2 = lambdaAway - lambda3;
        }

        var scores = new List<ScoreProbability>((maxGoals + 1) * (maxGoals + 1));
        var expTerm = Math.Exp(-(lambda1 + lambda2 + lambda3));

        for (var i = 0; i <= maxGoals; i++)
        {
            var p1 = PoissonMath.Probability(i, lambda1);
            for (var j = 0; j <= maxGoals; j++)
            {
                var p2 = PoissonMath.Probability(j, lambda2);
                var convolution = BivariateConvolution(i, j, lambda1, lambda2, lambda3);
                var prob = Math.Max(0, expTerm * convolution);
                scores.Add(new ScoreProbability(i, j, prob));
            }
        }

        var total = scores.Sum(s => s.Probability);
        if (total <= 0) return scores;

        // Normalise to account for truncation at maxGoals
        var baseMass = PoissonBaseMass(lambdaHome, lambdaAway, maxGoals);
        return scores.Select(s => s with { Probability = s.Probability / total * baseMass }).ToArray();
    }

    /// <summary>
    /// Computes the bivariate Poisson probability mass (without the exp term):
    /// sum_{k=0}^{min(i,j)} C(i,k)*C(j,k)*k! * (lambda3/(lambda1*lambda2))^k * lambda1^i/i! * lambda2^j/j!
    /// </summary>
    private static double BivariateConvolution(int i, int j, double lambda1, double lambda2, double lambda3)
    {
        if (lambda3 <= 0)
        {
            return PoissonMath.UnnormalizedProbability(i, lambda1) * PoissonMath.UnnormalizedProbability(j, lambda2);
        }

        var ratio = lambda3 / (lambda1 * lambda2);
        var pi = PoissonMath.UnnormalizedProbability(i, lambda1);
        var pj = PoissonMath.UnnormalizedProbability(j, lambda2);
        var sum = 0.0;
        var ratioK = 1.0;
        var kFact = 1.0;
        var cik = 1.0;
        var cjk = 1.0;

        for (var k = 0; k <= Math.Min(i, j); k++)
        {
            if (k > 0)
            {
                cik *= (i - k + 1.0) / k;
                cjk *= (j - k + 1.0) / k;
                ratioK *= ratio;
                kFact *= k;
            }

            sum += cik * cjk * kFact * ratioK;
        }

        return pi * pj * sum;
    }

    private static double PoissonBaseMass(double lambdaHome, double lambdaAway, int maxGoals)
    {
        var mass = 0.0;
        for (var i = 0; i <= maxGoals; i++)
        {
            var ph = PoissonMath.Probability(i, lambdaHome);
            for (var j = 0; j <= maxGoals; j++)
            {
                mass += ph * PoissonMath.Probability(j, lambdaAway);
            }
        }

        return mass;
    }
}
