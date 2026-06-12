namespace ScoreMatrix.Domain;

public enum InputMode
{
    Odds1X2,
    ExpectedGoals
}

public enum ScoreModelType
{
    Poisson,
    DixonColes,
    BivariatePoisson,
    NegativeBinomial
}

public enum DisplayMode
{
    Probabilities,
    FairOdds,
    OddsWithMargin
}

public enum DevigMethod
{
    Proportional,
    Power,
    Shin,
    Additive
}

public sealed record MatchInput
{
    public string HomeTeamName { get; init; } = "Local";
    public string AwayTeamName { get; init; } = "Visitante";
    public InputMode InputMode { get; init; } = InputMode.Odds1X2;
    public double HomeOdds { get; init; } = 2.10;
    public double DrawOdds { get; init; } = 3.25;
    public double AwayOdds { get; init; } = 3.60;
    public bool UseOverUnderOdds { get; init; }
    public double OverUnderLine { get; init; } = 2.5;
    public double OverOdds { get; init; } = 1.90;
    public double UnderOdds { get; init; } = 1.90;
    public bool UseBothTeamsToScoreOdds { get; init; }
    public double BothTeamsToScoreYesOdds { get; init; } = 1.90;
    public double BothTeamsToScoreNoOdds { get; init; } = 1.90;
    public double HomeExpectedGoals { get; init; } = 1.40;
    public double AwayExpectedGoals { get; init; } = 1.10;
    public ScoreModelType ModelType { get; init; } = ScoreModelType.Poisson;
    public int MaxGoals { get; init; } = 6;
    public DisplayMode DisplayMode { get; init; } = DisplayMode.Probabilities;
    public double BookmakerMarginPercent { get; init; }
    public double DixonColesRho { get; init; } = -0.05;
    public bool AutoCalibrateDependence { get; init; }
    public DevigMethod DevigMethod { get; init; } = DevigMethod.Proportional;
    public PencaScoringRules PencaScoringRules { get; init; } = PencaScoringRules.Default;
}

public sealed record PencaScoringRules(int Exact, int Result, int Goals)
{
    public static PencaScoringRules Default => new(7, 2, 1);
}

public sealed record PencaRecommendation(int HomeGoals, int AwayGoals, double ExpectedPoints, double ExactProbability)
{
    public string ScoreLabel => $"{HomeGoals}-{AwayGoals}";
}

public sealed record MarketProbabilities(double HomeWin, double Draw, double AwayWin)
{
    public double Sum => HomeWin + Draw + AwayWin;
}

public sealed record OverUnderMarketProbabilities(double Line, double Over, double Under)
{
    public double Sum => Over + Under;
}

public sealed record BothTeamsToScoreMarketProbabilities(double Yes, double No)
{
    public double Sum => Yes + No;
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
    public double EstimatedDependence { get; init; }
    public double BookmakerMarginPercent { get; init; }
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
    public IReadOnlyList<PencaRecommendation> PencaRanking { get; init; } = [];
}
