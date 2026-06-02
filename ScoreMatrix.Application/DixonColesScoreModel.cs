using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class DixonColesScoreModel(double rho) : IScoreModel
{
    public IReadOnlyList<ScoreProbability> Calculate(double lambdaHome, double lambdaAway, int maxGoals)
    {
        PoissonScoreModel.Validate(lambdaHome, lambdaAway, maxGoals);

        if (rho is < -0.25 or > 0.25)
        {
            throw new ScoreMatrixValidationException("Rho Dixon-Coles debe estar entre -0.25 y 0.25.");
        }

        var scores = new List<ScoreProbability>((maxGoals + 1) * (maxGoals + 1));
        var total = 0.0;

        for (var i = 0; i <= maxGoals; i++)
        {
            for (var j = 0; j <= maxGoals; j++)
            {
                var poisson = PoissonMath.Probability(i, lambdaHome) * PoissonMath.Probability(j, lambdaAway);
                var adjusted = Math.Max(0, poisson * Tau(i, j, lambdaHome, lambdaAway, rho));
                scores.Add(new ScoreProbability(i, j, adjusted));
                total += adjusted;
            }
        }

        var baseMass = scores.Sum(score => BaseProbability(score.HomeGoals, score.AwayGoals, lambdaHome, lambdaAway));
        return total <= 0
            ? scores
            : scores.Select(score => score with { Probability = score.Probability / total * baseMass }).ToArray();
    }

    private static double BaseProbability(int homeGoals, int awayGoals, double lambdaHome, double lambdaAway)
        => PoissonMath.Probability(homeGoals, lambdaHome) * PoissonMath.Probability(awayGoals, lambdaAway);

    private static double Tau(int homeGoals, int awayGoals, double lambdaHome, double lambdaAway, double rho)
        => (homeGoals, awayGoals) switch
        {
            (0, 0) => 1 - lambdaHome * lambdaAway * rho,
            (0, 1) => 1 + lambdaHome * rho,
            (1, 0) => 1 + lambdaAway * rho,
            (1, 1) => 1 - rho,
            _ => 1
        };
}
