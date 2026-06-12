using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class ScoreMatrixCalculator(
    OddsConverter oddsConverter,
    LambdaOptimizer lambdaOptimizer,
    PencaEvaluator pencaEvaluator)
{
    private const int SummaryAggregationGoals = 15;

    public ScoreMatrixResult Calculate(MatchInput input)
    {
        if (input.BookmakerMarginPercent < 0 || input.BookmakerMarginPercent > 50)
        {
            throw new ScoreMatrixValidationException("El margen debe estar entre 0% y 50%.");
        }

        var (lambdaHome, lambdaAway, dependence) = ResolveLambdas(input);

        IScoreModel model = input.ModelType switch
        {
            ScoreModelType.DixonColes => new DixonColesScoreModel(dependence),
            ScoreModelType.BivariatePoisson => new BivariatePoissonScoreModel(dependence),
            ScoreModelType.NegativeBinomial => new NegativeBinomialScoreModel(dependence),
            _ => new PoissonScoreModel()
        };

        var scores = model.Calculate(lambdaHome, lambdaAway, input.MaxGoals);
        var summaryScores = input.MaxGoals >= SummaryAggregationGoals
            ? scores
            : model.Calculate(lambdaHome, lambdaAway, SummaryAggregationGoals);
        var matrixMass = scores.Sum(score => score.Probability);
        var pencaRanking = pencaEvaluator.Rank(summaryScores, scores, input.PencaScoringRules);

        return new ScoreMatrixResult
        {
            HomeTeamName = NormalizeName(input.HomeTeamName, "Local"),
            AwayTeamName = NormalizeName(input.AwayTeamName, "Visitante"),
            LambdaHome = lambdaHome,
            LambdaAway = lambdaAway,
            EstimatedDependence = dependence,
            BookmakerMarginPercent = input.BookmakerMarginPercent,
            Scores = scores,
            HomeWinProbability = summaryScores.Where(score => score.HomeGoals > score.AwayGoals).Sum(score => score.Probability),
            DrawProbability = summaryScores.Where(score => score.HomeGoals == score.AwayGoals).Sum(score => score.Probability),
            AwayWinProbability = summaryScores.Where(score => score.HomeGoals < score.AwayGoals).Sum(score => score.Probability),
            OverUnderSummary = BuildOverUnder(summaryScores),
            BothTeamsToScoreYes = summaryScores.Where(score => score.HomeGoals > 0 && score.AwayGoals > 0).Sum(score => score.Probability),
            BothTeamsToScoreNo = summaryScores.Where(score => score.HomeGoals == 0 || score.AwayGoals == 0).Sum(score => score.Probability),
            MatrixProbabilityMass = matrixMass,
            ResidualProbability = Math.Max(0, 1 - matrixMass),
            MostLikelyScore = scores.MaxBy(score => score.Probability),
            PencaRanking = pencaRanking
        };
    }

    private (double Home, double Away, double Dependence) ResolveLambdas(MatchInput input)
    {
        if (input.InputMode == InputMode.ExpectedGoals)
        {
            if (input.HomeExpectedGoals <= 0 || input.AwayExpectedGoals <= 0)
            {
                throw new ScoreMatrixValidationException("Los goles esperados deben ser mayores que 0.");
            }

            var dep = input.AutoCalibrateDependence ? 0.0 : input.DixonColesRho;
            return (input.HomeExpectedGoals, input.AwayExpectedGoals, dep);
        }

        var probabilities = oddsConverter.ToNoMarginProbabilities(input.HomeOdds, input.DrawOdds, input.AwayOdds, input.DevigMethod);
        var totalGoalsTarget = input.UseOverUnderOdds
            ? oddsConverter.ToNoMarginOverUnderProbabilities(input.OverUnderLine, input.OverOdds, input.UnderOdds, input.DevigMethod)
            : null;
        var bothTeamsToScoreTarget = input.UseBothTeamsToScoreOdds
            ? oddsConverter.ToNoMarginBothTeamsToScoreProbabilities(input.BothTeamsToScoreYesOdds, input.BothTeamsToScoreNoOdds, input.DevigMethod)
            : null;

        var autoCalibrate = input.AutoCalibrateDependence
            && input.ModelType is ScoreModelType.DixonColes or ScoreModelType.BivariatePoisson
            && (totalGoalsTarget is not null || bothTeamsToScoreTarget is not null);

        var result = lambdaOptimizer.Optimize(
            probabilities,
            totalGoalsTarget,
            bothTeamsToScoreTarget,
            autoCalibrate,
            input.DixonColesRho,
            input.ModelType);

        return result;
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
