using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class PoissonScoreModel : IScoreModel
{
    public IReadOnlyList<ScoreProbability> Calculate(double lambdaHome, double lambdaAway, int maxGoals)
    {
        Validate(lambdaHome, lambdaAway, maxGoals);

        var home = Enumerable.Range(0, maxGoals + 1)
            .Select(goal => PoissonMath.Probability(goal, lambdaHome))
            .ToArray();
        var away = Enumerable.Range(0, maxGoals + 1)
            .Select(goal => PoissonMath.Probability(goal, lambdaAway))
            .ToArray();

        var scores = new List<ScoreProbability>((maxGoals + 1) * (maxGoals + 1));
        for (var i = 0; i <= maxGoals; i++)
        {
            for (var j = 0; j <= maxGoals; j++)
            {
                scores.Add(new ScoreProbability(i, j, home[i] * away[j]));
            }
        }

        return scores;
    }

    public static void Validate(double lambdaHome, double lambdaAway, int maxGoals)
    {
        if (lambdaHome <= 0 || lambdaAway <= 0)
        {
            throw new ScoreMatrixValidationException("Los goles esperados deben ser mayores que 0.");
        }

        if (lambdaHome > 6 || lambdaAway > 6)
        {
            throw new ScoreMatrixValidationException("Los goles esperados no pueden ser mayores que 6.");
        }

        if (maxGoals is < 3 or > 15)
        {
            throw new ScoreMatrixValidationException("MaxGoals debe estar entre 3 y 15.");
        }
    }
}
