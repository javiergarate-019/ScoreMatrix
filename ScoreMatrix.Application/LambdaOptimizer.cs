using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class LambdaOptimizer
{
    private const double MinLambda = 0.05;
    private const double MaxLambda = 6.0;
    private const int AggregationGoals = 18;

    public (double Home, double Away) Optimize(
        MarketProbabilities target,
        OverUnderMarketProbabilities? totalGoalsTarget = null,
        BothTeamsToScoreMarketProbabilities? bothTeamsToScoreTarget = null)
    {
        if (Math.Abs(target.Sum - 1) > 0.0001)
        {
            throw new ScoreMatrixValidationException("Las probabilidades de mercado deben sumar 1.");
        }

        if (totalGoalsTarget is not null && Math.Abs(totalGoalsTarget.Sum - 1) > 0.0001)
        {
            throw new ScoreMatrixValidationException("Las probabilidades Over/Under de mercado deben sumar 1.");
        }

        if (bothTeamsToScoreTarget is not null && Math.Abs(bothTeamsToScoreTarget.Sum - 1) > 0.0001)
        {
            throw new ScoreMatrixValidationException("Las probabilidades BTTS de mercado deben sumar 1.");
        }

        var bestHome = 1.4;
        var bestAway = 1.1;
        var bestError = Error(bestHome, bestAway, target, totalGoalsTarget, bothTeamsToScoreTarget);

        for (var home = MinLambda; home <= MaxLambda; home += 0.05)
        {
            for (var away = MinLambda; away <= MaxLambda; away += 0.05)
            {
                var error = Error(home, away, target, totalGoalsTarget, bothTeamsToScoreTarget);
                if (error < bestError)
                {
                    bestError = error;
                    bestHome = home;
                    bestAway = away;
                }
            }
        }

        var step = 0.025;
        while (step >= 0.0005)
        {
            var improved = false;
            foreach (var (candidateHome, candidateAway) in Neighbors(bestHome, bestAway, step))
            {
                var error = Error(candidateHome, candidateAway, target, totalGoalsTarget, bothTeamsToScoreTarget);
                if (error >= bestError)
                {
                    continue;
                }

                bestError = error;
                bestHome = candidateHome;
                bestAway = candidateAway;
                improved = true;
            }

            if (!improved)
            {
                step /= 2;
            }
        }

        return (bestHome, bestAway);
    }

    private static IEnumerable<(double Home, double Away)> Neighbors(double home, double away, double step)
    {
        for (var dh = -1; dh <= 1; dh++)
        {
            for (var da = -1; da <= 1; da++)
            {
                if (dh == 0 && da == 0)
                {
                    continue;
                }

                yield return (Clamp(home + dh * step), Clamp(away + da * step));
            }
        }
    }

    private static double Clamp(double value) => Math.Clamp(value, MinLambda, MaxLambda);

    private static double Error(
        double lambdaHome,
        double lambdaAway,
        MarketProbabilities target,
        OverUnderMarketProbabilities? totalGoalsTarget,
        BothTeamsToScoreMarketProbabilities? bothTeamsToScoreTarget)
    {
        var model = Aggregate1X2(lambdaHome, lambdaAway);
        var error = Math.Pow(model.HomeWin - target.HomeWin, 2)
            + Math.Pow(model.Draw - target.Draw, 2)
            + Math.Pow(model.AwayWin - target.AwayWin, 2);

        if (totalGoalsTarget is not null)
        {
            var totalGoalsModel = AggregateOverUnder(lambdaHome, lambdaAway, totalGoalsTarget.Line);
            error += Math.Pow(totalGoalsModel.Over - totalGoalsTarget.Over, 2)
                + Math.Pow(totalGoalsModel.Under - totalGoalsTarget.Under, 2);
        }

        if (bothTeamsToScoreTarget is not null)
        {
            var bothTeamsToScoreModel = AggregateBothTeamsToScore(lambdaHome, lambdaAway);
            error += Math.Pow(bothTeamsToScoreModel.Yes - bothTeamsToScoreTarget.Yes, 2)
                + Math.Pow(bothTeamsToScoreModel.No - bothTeamsToScoreTarget.No, 2);
        }

        return error;
    }

    private static MarketProbabilities Aggregate1X2(double lambdaHome, double lambdaAway)
    {
        var homeWin = 0.0;
        var draw = 0.0;
        var awayWin = 0.0;

        for (var homeGoals = 0; homeGoals <= AggregationGoals; homeGoals++)
        {
            var homeProbability = PoissonMath.Probability(homeGoals, lambdaHome);
            for (var awayGoals = 0; awayGoals <= AggregationGoals; awayGoals++)
            {
                var probability = homeProbability * PoissonMath.Probability(awayGoals, lambdaAway);
                if (homeGoals > awayGoals)
                {
                    homeWin += probability;
                }
                else if (homeGoals == awayGoals)
                {
                    draw += probability;
                }
                else
                {
                    awayWin += probability;
                }
            }
        }

        var total = homeWin + draw + awayWin;
        return new MarketProbabilities(homeWin / total, draw / total, awayWin / total);
    }

    private static OverUnderMarketProbabilities AggregateOverUnder(double lambdaHome, double lambdaAway, double line)
    {
        var over = 0.0;
        var under = 0.0;

        for (var homeGoals = 0; homeGoals <= AggregationGoals; homeGoals++)
        {
            var homeProbability = PoissonMath.Probability(homeGoals, lambdaHome);
            for (var awayGoals = 0; awayGoals <= AggregationGoals; awayGoals++)
            {
                var probability = homeProbability * PoissonMath.Probability(awayGoals, lambdaAway);
                if (homeGoals + awayGoals > line)
                {
                    over += probability;
                }
                else
                {
                    under += probability;
                }
            }
        }

        var total = over + under;
        return new OverUnderMarketProbabilities(line, over / total, under / total);
    }

    private static BothTeamsToScoreMarketProbabilities AggregateBothTeamsToScore(double lambdaHome, double lambdaAway)
    {
        var yes = 0.0;
        var no = 0.0;

        for (var homeGoals = 0; homeGoals <= AggregationGoals; homeGoals++)
        {
            var homeProbability = PoissonMath.Probability(homeGoals, lambdaHome);
            for (var awayGoals = 0; awayGoals <= AggregationGoals; awayGoals++)
            {
                var probability = homeProbability * PoissonMath.Probability(awayGoals, lambdaAway);
                if (homeGoals > 0 && awayGoals > 0)
                {
                    yes += probability;
                }
                else
                {
                    no += probability;
                }
            }
        }

        var total = yes + no;
        return new BothTeamsToScoreMarketProbabilities(yes / total, no / total);
    }
}
