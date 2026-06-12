using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class PencaEvaluator
{
    public IReadOnlyList<PencaRecommendation> Rank(
        IReadOnlyList<ScoreProbability> distribution,
        IReadOnlyList<ScoreProbability> candidates,
        PencaScoringRules rules)
    {
        ValidateRules(rules);

        return candidates
            .Select(candidate =>
            {
                var expectedPoints = distribution.Sum(outcome =>
                    outcome.Probability * ScorePoints(
                        candidate.HomeGoals,
                        candidate.AwayGoals,
                        outcome.HomeGoals,
                        outcome.AwayGoals,
                        rules));

                var exactProbability = distribution
                    .FirstOrDefault(outcome =>
                        outcome.HomeGoals == candidate.HomeGoals &&
                        outcome.AwayGoals == candidate.AwayGoals)
                    ?.Probability ?? 0;

                return new PencaRecommendation(
                    candidate.HomeGoals,
                    candidate.AwayGoals,
                    expectedPoints,
                    exactProbability);
            })
            .OrderByDescending(recommendation => recommendation.ExpectedPoints)
            .ThenByDescending(recommendation => recommendation.ExactProbability)
            .ThenBy(recommendation => recommendation.HomeGoals)
            .ThenBy(recommendation => recommendation.AwayGoals)
            .ToArray();
    }

    public static int ScorePoints(
        int predictedHome,
        int predictedAway,
        int actualHome,
        int actualAway,
        PencaScoringRules rules)
    {
        if (predictedHome == actualHome && predictedAway == actualAway)
        {
            return rules.Exact;
        }

        var points = 0;

        if (Math.Sign(predictedHome - predictedAway) == Math.Sign(actualHome - actualAway))
        {
            points += rules.Result;
        }

        if (predictedHome == actualHome || predictedAway == actualAway)
        {
            points += rules.Goals;
        }

        return points;
    }

    private static void ValidateRules(PencaScoringRules rules)
    {
        if (rules.Exact < 0 || rules.Result < 0 || rules.Goals < 0)
        {
            throw new ScoreMatrixValidationException("Los puntos de la penca deben ser mayores o iguales a 0.");
        }
    }
}
