namespace ScoreMatrix.Domain;

public enum InputMode
{
    Odds1X2,
    ExpectedGoals
}

public enum ScoreModelType
{
    Poisson,
    DixonColes
}

public enum DisplayMode
{
    Probabilities,
    FairOdds
}

public sealed record MatchInput
{
    public string HomeTeamName { get; init; } = "Local";
    public string AwayTeamName { get; init; } = "Visitante";
    public InputMode InputMode { get; init; } = InputMode.Odds1X2;
    public double HomeOdds { get; init; } = 2.10;
    public double DrawOdds { get; init; } = 3.25;
    public double AwayOdds { get; init; } = 3.60;
    public double HomeExpectedGoals { get; init; } = 1.40;
    public double AwayExpectedGoals { get; init; } = 1.10;
    public ScoreModelType ModelType { get; init; } = ScoreModelType.Poisson;
    public int MaxGoals { get; init; } = 6;
    public DisplayMode DisplayMode { get; init; } = DisplayMode.Probabilities;
    public double DixonColesRho { get; init; } = -0.05;
}

public sealed record MarketProbabilities(double HomeWin, double Draw, double AwayWin)
{
    public double Sum => HomeWin + Draw + AwayWin;
}

public sealed record ScoreProbability(int HomeGoals, int AwayGoals, double Probability)
{
    public double FairOdds => Probability <= 0 ? double.PositiveInfinity : 1.0 / Probability;
    public string ScoreLabel => $"{HomeGoals}-{AwayGoals}";
}

public sealed record OverUnderProbability(double Line, double Over, double Under);

public sealed record ScoreMatrixResult
{
    public string HomeTeamName { get; init; } = "Local";
    public string AwayTeamName { get; init; } = "Visitante";
    public double LambdaHome { get; init; }
    public double LambdaAway { get; init; }
    public IReadOnlyList<ScoreProbability> Scores { get; init; } = [];
    public double HomeWinProbability { get; init; }
    public double DrawProbability { get; init; }
    public double AwayWinProbability { get; init; }
    public IReadOnlyList<OverUnderProbability> OverUnderSummary { get; init; } = [];
    public double BothTeamsToScoreYes { get; init; }
    public double BothTeamsToScoreNo { get; init; }
    public double MatrixProbabilityMass { get; init; }
    public double ResidualProbability { get; init; }
    public ScoreProbability? MostLikelyScore { get; init; }
}
