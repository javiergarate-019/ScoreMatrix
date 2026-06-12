using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class LambdaOptimizer
{
    private const double MinLambda = 0.05;
    private const double MaxLambda = 6.0;
    private const double MinDependence = -0.25;
    private const double MaxDependence = 0.25;
    private const int AggregationGoals = 18;

    /// <summary>
    /// Finds the (lambdaHome, lambdaAway, dependence) triple that best fits the market targets.
    /// When autoCalibrateDependence is true the dependence parameter (rho or lambda3) is also
    /// optimised; otherwise it is fixed to fixedDependence.
    /// Returns (Home, Away, Dependence).
    /// </summary>
    public (double Home, double Away, double Dependence) Optimize(
        MarketProbabilities target,
        OverUnderMarketProbabilities? totalGoalsTarget = null,
        BothTeamsToScoreMarketProbabilities? bothTeamsToScoreTarget = null,
        bool autoCalibrateDependence = false,
        double fixedDependence = 0.0,
        ScoreModelType modelType = ScoreModelType.Poisson)
    {
        ValidateInputs(target, totalGoalsTarget, bothTeamsToScoreTarget);

        // Grid search seed: step 0.05 over lambda space
        var seedHome = 1.4;
        var seedAway = 1.1;
        var seedDep = fixedDependence;
        var bestError = KlError(seedHome, seedAway, seedDep, target, totalGoalsTarget, bothTeamsToScoreTarget);

        for (var home = MinLambda; home <= MaxLambda; home += 0.05)
        {
            for (var away = MinLambda; away <= MaxLambda; away += 0.05)
            {
                var dep = autoCalibrateDependence ? 0.0 : fixedDependence;
                var error = KlError(home, away, dep, target, totalGoalsTarget, bothTeamsToScoreTarget);
                if (error < bestError)
                {
                    bestError = error;
                    seedHome = home;
                    seedAway = away;
                    seedDep = dep;
                }
            }
        }

        // Nelder-Mead refinement
        return autoCalibrateDependence
            ? NelderMead3D(seedHome, seedAway, seedDep, target, totalGoalsTarget, bothTeamsToScoreTarget)
            : NelderMead2D(seedHome, seedAway, fixedDependence, target, totalGoalsTarget, bothTeamsToScoreTarget);
    }

    // ── Nelder-Mead 2D (lambda only, dependence fixed) ───────────────────────

    private static (double Home, double Away, double Dependence) NelderMead2D(
        double initHome, double initAway, double dep,
        MarketProbabilities target,
        OverUnderMarketProbabilities? ouTarget,
        BothTeamsToScoreMarketProbabilities? bttsTarget)
    {
        double F(double h, double a) => KlError(h, a, dep, target, ouTarget, bttsTarget);

        // Initial simplex: 3 points
        double[] x0 = [initHome, initAway];
        double[] x1 = [ClampLambda(initHome + 0.1), initAway];
        double[] x2 = [initHome, ClampLambda(initAway + 0.1)];

        var simplex = new (double[] p, double f)[]
        {
            (x0, F(x0[0], x0[1])),
            (x1, F(x1[0], x1[1])),
            (x2, F(x2[0], x2[1]))
        };

        for (var iter = 0; iter < 2000; iter++)
        {
            Array.Sort(simplex, (a, b) => a.f.CompareTo(b.f));

            // Centroid of best n points (excluding worst)
            var cx = (simplex[0].p[0] + simplex[1].p[0]) / 2;
            var cy = (simplex[0].p[1] + simplex[1].p[1]) / 2;

            // Reflect
            var rx = ClampLambda(2 * cx - simplex[2].p[0]);
            var ry = ClampLambda(2 * cy - simplex[2].p[1]);
            var fr = F(rx, ry);

            if (fr < simplex[0].f)
            {
                // Expand
                var ex = ClampLambda(3 * cx - 2 * simplex[2].p[0]);
                var ey = ClampLambda(3 * cy - 2 * simplex[2].p[1]);
                var fe = F(ex, ey);
                simplex[2] = fe < fr ? ([ex, ey], fe) : ([rx, ry], fr);
            }
            else if (fr < simplex[1].f)
            {
                simplex[2] = ([rx, ry], fr);
            }
            else
            {
                // Contract
                var contracting = fr < simplex[2].f;
                var bx = ClampLambda(contracting ? (cx + rx) / 2 : (cx + simplex[2].p[0]) / 2);
                var by = ClampLambda(contracting ? (cy + ry) / 2 : (cy + simplex[2].p[1]) / 2);
                var fb = F(bx, by);

                if (fb < simplex[2].f)
                {
                    simplex[2] = ([bx, by], fb);
                }
                else
                {
                    // Shrink
                    simplex[1] = ([ClampLambda((simplex[0].p[0] + simplex[1].p[0]) / 2), ClampLambda((simplex[0].p[1] + simplex[1].p[1]) / 2)], F((simplex[0].p[0] + simplex[1].p[0]) / 2, (simplex[0].p[1] + simplex[1].p[1]) / 2));
                    simplex[2] = ([ClampLambda((simplex[0].p[0] + simplex[2].p[0]) / 2), ClampLambda((simplex[0].p[1] + simplex[2].p[1]) / 2)], F((simplex[0].p[0] + simplex[2].p[0]) / 2, (simplex[0].p[1] + simplex[2].p[1]) / 2));
                }
            }

            if (SimplexSize2D(simplex) < 1e-9) break;
        }

        Array.Sort(simplex, (a, b) => a.f.CompareTo(b.f));
        return (simplex[0].p[0], simplex[0].p[1], dep);
    }

    // ── Nelder-Mead 3D (lambda + dependence) ─────────────────────────────────

    private static (double Home, double Away, double Dependence) NelderMead3D(
        double initHome, double initAway, double initDep,
        MarketProbabilities target,
        OverUnderMarketProbabilities? ouTarget,
        BothTeamsToScoreMarketProbabilities? bttsTarget)
    {
        double F(double h, double a, double d) => KlError(h, a, d, target, ouTarget, bttsTarget);

        double[] p0 = [initHome, initAway, initDep];
        double[] p1 = [ClampLambda(initHome + 0.1), initAway, initDep];
        double[] p2 = [initHome, ClampLambda(initAway + 0.1), initDep];
        double[] p3 = [initHome, initAway, ClampDep(initDep + 0.02)];

        var simplex = new (double[] p, double f)[]
        {
            (p0, F(p0[0], p0[1], p0[2])),
            (p1, F(p1[0], p1[1], p1[2])),
            (p2, F(p2[0], p2[1], p2[2])),
            (p3, F(p3[0], p3[1], p3[2]))
        };

        for (var iter = 0; iter < 3000; iter++)
        {
            Array.Sort(simplex, (a, b) => a.f.CompareTo(b.f));

            // Centroid of best n points
            var cx = (simplex[0].p[0] + simplex[1].p[0] + simplex[2].p[0]) / 3;
            var cy = (simplex[0].p[1] + simplex[1].p[1] + simplex[2].p[1]) / 3;
            var cz = (simplex[0].p[2] + simplex[1].p[2] + simplex[2].p[2]) / 3;

            // Reflect
            var rx = ClampLambda(2 * cx - simplex[3].p[0]);
            var ry = ClampLambda(2 * cy - simplex[3].p[1]);
            var rz = ClampDep(2 * cz - simplex[3].p[2]);
            var fr = F(rx, ry, rz);

            if (fr < simplex[0].f)
            {
                // Expand
                var ex = ClampLambda(3 * cx - 2 * simplex[3].p[0]);
                var ey = ClampLambda(3 * cy - 2 * simplex[3].p[1]);
                var ez = ClampDep(3 * cz - 2 * simplex[3].p[2]);
                var fe = F(ex, ey, ez);
                simplex[3] = fe < fr ? ([ex, ey, ez], fe) : ([rx, ry, rz], fr);
            }
            else if (fr < simplex[2].f)
            {
                simplex[3] = ([rx, ry, rz], fr);
            }
            else
            {
                var contracting = fr < simplex[3].f;
                var bx = ClampLambda(contracting ? (cx + rx) / 2 : (cx + simplex[3].p[0]) / 2);
                var by = ClampLambda(contracting ? (cy + ry) / 2 : (cy + simplex[3].p[1]) / 2);
                var bz = ClampDep(contracting ? (cz + rz) / 2 : (cz + simplex[3].p[2]) / 2);
                var fb = F(bx, by, bz);

                if (fb < simplex[3].f)
                {
                    simplex[3] = ([bx, by, bz], fb);
                }
                else
                {
                    // Shrink toward best
                    for (var i = 1; i < simplex.Length; i++)
                    {
                        var sh = ClampLambda((simplex[0].p[0] + simplex[i].p[0]) / 2);
                        var sa = ClampLambda((simplex[0].p[1] + simplex[i].p[1]) / 2);
                        var sd = ClampDep((simplex[0].p[2] + simplex[i].p[2]) / 2);
                        simplex[i] = ([sh, sa, sd], F(sh, sa, sd));
                    }
                }
            }

            if (SimplexSize3D(simplex) < 1e-10) break;
        }

        Array.Sort(simplex, (a, b) => a.f.CompareTo(b.f));
        return (simplex[0].p[0], simplex[0].p[1], simplex[0].p[2]);
    }

    // ── Objective: KL divergence (sum of p_market * log(p_market / p_model)) ─

    private static double KlError(
        double lambdaHome, double lambdaAway, double dependence,
        MarketProbabilities target,
        OverUnderMarketProbabilities? ouTarget,
        BothTeamsToScoreMarketProbabilities? bttsTarget)
    {
        var model = Aggregate1X2(lambdaHome, lambdaAway);
        var error = KlTerm(target.HomeWin, model.HomeWin)
            + KlTerm(target.Draw, model.Draw)
            + KlTerm(target.AwayWin, model.AwayWin);

        if (ouTarget is not null)
        {
            var ouModel = AggregateOverUnder(lambdaHome, lambdaAway, ouTarget.Line);
            error += KlTerm(ouTarget.Over, ouModel.Over) + KlTerm(ouTarget.Under, ouModel.Under);
        }

        if (bttsTarget is not null)
        {
            var bttsModel = AggregateBothTeamsToScore(lambdaHome, lambdaAway);
            error += KlTerm(bttsTarget.Yes, bttsModel.Yes) + KlTerm(bttsTarget.No, bttsModel.No);
        }

        return error;
    }

    /// <summary>KL term: p * log(p/q) + (1-p) * log((1-p)/(1-q)), symmetric penalty.</summary>
    private static double KlTerm(double p, double q)
    {
        q = Math.Clamp(q, 1e-12, 1 - 1e-12);
        p = Math.Clamp(p, 1e-12, 1 - 1e-12);
        return p * Math.Log(p / q) + (1 - p) * Math.Log((1 - p) / (1 - q));
    }

    // ── Market aggregation ────────────────────────────────────────────────────

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
                if (homeGoals > awayGoals) homeWin += probability;
                else if (homeGoals == awayGoals) draw += probability;
                else awayWin += probability;
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
                if (homeGoals + awayGoals > line) over += probability;
                else under += probability;
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
                if (homeGoals > 0 && awayGoals > 0) yes += probability;
                else no += probability;
            }
        }

        var total = yes + no;
        return new BothTeamsToScoreMarketProbabilities(yes / total, no / total);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static double ClampLambda(double v) => Math.Clamp(v, MinLambda, MaxLambda);
    private static double ClampDep(double v) => Math.Clamp(v, MinDependence, MaxDependence);

    private static double SimplexSize2D((double[] p, double f)[] s)
    {
        var maxDist = 0.0;
        for (var i = 1; i < s.Length; i++)
        {
            var d = Math.Sqrt(Math.Pow(s[i].p[0] - s[0].p[0], 2) + Math.Pow(s[i].p[1] - s[0].p[1], 2));
            if (d > maxDist) maxDist = d;
        }

        return maxDist;
    }

    private static double SimplexSize3D((double[] p, double f)[] s)
    {
        var maxDist = 0.0;
        for (var i = 1; i < s.Length; i++)
        {
            var d = Math.Sqrt(
                Math.Pow(s[i].p[0] - s[0].p[0], 2) +
                Math.Pow(s[i].p[1] - s[0].p[1], 2) +
                Math.Pow(s[i].p[2] - s[0].p[2], 2));
            if (d > maxDist) maxDist = d;
        }

        return maxDist;
    }

    private static void ValidateInputs(
        MarketProbabilities target,
        OverUnderMarketProbabilities? ouTarget,
        BothTeamsToScoreMarketProbabilities? bttsTarget)
    {
        if (Math.Abs(target.Sum - 1) > 0.0001)
        {
            throw new ScoreMatrixValidationException("Las probabilidades de mercado deben sumar 1.");
        }

        if (ouTarget is not null && Math.Abs(ouTarget.Sum - 1) > 0.0001)
        {
            throw new ScoreMatrixValidationException("Las probabilidades Over/Under de mercado deben sumar 1.");
        }

        if (bttsTarget is not null && Math.Abs(bttsTarget.Sum - 1) > 0.0001)
        {
            throw new ScoreMatrixValidationException("Las probabilidades BTTS de mercado deben sumar 1.");
        }
    }
}
