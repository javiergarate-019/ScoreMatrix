using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

/// <summary>
/// Represents a historical match with input odds and the actual final score.
/// </summary>
public sealed record MatchRecord
{
    public string HomeTeam { get; init; } = "Local";
    public string AwayTeam { get; init; } = "Visitante";
    public double HomeOdds { get; init; }
    public double DrawOdds { get; init; }
    public double AwayOdds { get; init; }
    public bool HasOverUnder { get; init; }
    public double OverUnderLine { get; init; } = 2.5;
    public double OverOdds { get; init; }
    public double UnderOdds { get; init; }
    public bool HasBtts { get; init; }
    public double BttsYesOdds { get; init; }
    public double BttsNoOdds { get; init; }
    public int ActualHomeGoals { get; init; }
    public int ActualAwayGoals { get; init; }

    /// <summary>0 = home win, 1 = draw, 2 = away win.</summary>
    public int Outcome => ActualHomeGoals > ActualAwayGoals ? 0 : ActualHomeGoals == ActualAwayGoals ? 1 : 2;

    public MatchInput ToMatchInput(ScoreModelType modelType, DevigMethod devigMethod, double rho, bool autoCalibrateRho)
        => new()
        {
            HomeTeamName = HomeTeam,
            AwayTeamName = AwayTeam,
            InputMode = InputMode.Odds1X2,
            HomeOdds = HomeOdds,
            DrawOdds = DrawOdds,
            AwayOdds = AwayOdds,
            UseOverUnderOdds = HasOverUnder,
            OverUnderLine = OverUnderLine,
            OverOdds = OverOdds > 1 ? OverOdds : 1.90,
            UnderOdds = UnderOdds > 1 ? UnderOdds : 1.90,
            UseBothTeamsToScoreOdds = HasBtts,
            BothTeamsToScoreYesOdds = BttsYesOdds > 1 ? BttsYesOdds : 1.90,
            BothTeamsToScoreNoOdds = BttsNoOdds > 1 ? BttsNoOdds : 1.90,
            ModelType = modelType,
            DevigMethod = devigMethod,
            DixonColesRho = rho,
            AutoCalibrateDependence = autoCalibrateRho,
            MaxGoals = 10
        };
}
