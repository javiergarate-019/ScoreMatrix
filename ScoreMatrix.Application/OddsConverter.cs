using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class OddsConverter
{
    public MarketProbabilities ToNoMarginProbabilities(
        double homeOdds, double drawOdds, double awayOdds,
        DevigMethod method = DevigMethod.Proportional)
    {
        if (homeOdds <= 1 || drawOdds <= 1 || awayOdds <= 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas 1X2 deben ser mayores que 1.");
        }

        var probs = Devigger.RemoveMargin([homeOdds, drawOdds, awayOdds], method);
        return new MarketProbabilities(probs[0], probs[1], probs[2]);
    }

    public OverUnderMarketProbabilities ToNoMarginOverUnderProbabilities(
        double line, double overOdds, double underOdds,
        DevigMethod method = DevigMethod.Proportional)
    {
        if (overOdds <= 1 || underOdds <= 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas Over/Under deben ser mayores que 1.");
        }

        var probs = Devigger.RemoveMargin([overOdds, underOdds], method);
        return new OverUnderMarketProbabilities(line, probs[0], probs[1]);
    }

    public BothTeamsToScoreMarketProbabilities ToNoMarginBothTeamsToScoreProbabilities(
        double yesOdds, double noOdds,
        DevigMethod method = DevigMethod.Proportional)
    {
        if (yesOdds <= 1 || noOdds <= 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas BTTS deben ser mayores que 1.");
        }

        var probs = Devigger.RemoveMargin([yesOdds, noOdds], method);
        return new BothTeamsToScoreMarketProbabilities(probs[0], probs[1]);
    }
}
