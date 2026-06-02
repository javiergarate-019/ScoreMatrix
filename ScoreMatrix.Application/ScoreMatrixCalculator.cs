using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class ScoreMatrixCalculator(
    OddsConverter oddsConverter,
    LambdaOptimizer lambdaOptimizer)
{
    private const int SummaryAggregationGoals = 15;

    public ScoreMatrixResult Calculate(MatchInput input)
    {
        var lambda = ResolveLambdas(input);
        IScoreModel model = input.ModelType == ScoreModelType.DixonColes
            ? new DixonColesScoreModel(input.DixonColesRho)
            : new PoissonScoreModel();

        var scores = model.Calculate(lambda.Home, lambda.Away, input.MaxGoals);
        var summaryScores = input.MaxGoals >= SummaryAggregationGoals
            ? scores
            : model.Calculate(lambda.Home, lambda.Away, SummaryAggregationGoals);
        var matrixMass = scores.Sum(score => score.Probability);

        return new ScoreMatrixResult
        {
            HomeTeamName = NormalizeName(input.HomeTeamName, "Local"),
            AwayTeamName = NormalizeName(input.AwayTeamName, "Visitante"),
            LambdaHome = lambda.Home,
            LambdaAway = lambda.Away,
            Scores = scores,
            HomeWinProbability = summaryScores.Where(score => score.HomeGoals > score.AwayGoals).Sum(score => score.Probability),
            DrawProbability = summaryScores.Where(score => score.HomeGoals == score.AwayGoals).Sum(score => score.Probability),
            AwayWinProbability = summaryScores.Where(score => score.HomeGoals < score.AwayGoals).Sum(score => score.Probability),
            OverUnderSummary = BuildOverUnder(summaryScores),
            BothTeamsToScoreYes = summaryScores.Where(score => score.HomeGoals > 0 && score.AwayGoals > 0).Sum(score => score.Probability),
            BothTeamsToScoreNo = summaryScores.Where(score => score.HomeGoals == 0 || score.AwayGoals == 0).Sum(score => score.Probability),
            MatrixProbabilityMass = matrixMass,
            ResidualProbability = Math.Max(0, 1 - matrixMass),
            MostLikelyScore = scores.MaxBy(score => score.Probability)
        };
    }

    private (double Home, double Away) ResolveLambdas(MatchInput input)
    {
        if (input.InputMode == InputMode.ExpectedGoals)
        {
            if (input.HomeExpectedGoals <= 0 || input.AwayExpectedGoals <= 0)
            {
                throw new ScoreMatrixValidationException("Los goles esperados deben ser mayores que 0.");
            }

            return (input.HomeExpectedGoals, input.AwayExpectedGoals);
        }

        var probabilities = oddsConverter.ToNoMarginProbabilities(input.HomeOdds, input.DrawOdds, input.AwayOdds);
        return lambdaOptimizer.Optimize(probabilities);
    }

    private static IReadOnlyList<OverUnderProbability> BuildOverUnder(IReadOnlyList<ScoreProbability> scores)
    {
        double[] lines = [0.5, 1.5, 2.5, 3.5];
        return lines
            .Select(line =>
            {
                var over = scores.Where(score => score.HomeGoals + score.AwayGoals > line).Sum(score => score.Probability);
                var under = scores.Where(score => score.HomeGoals + score.AwayGoals < line).Sum(score => score.Probability);
                return new OverUnderProbability(line, over, under);
            })
            .ToArray();
    }

    private static string NormalizeName(string? value, string fallback)
        => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
}
