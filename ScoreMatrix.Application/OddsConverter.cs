using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class OddsConverter
{
    public MarketProbabilities ToNoMarginProbabilities(double homeOdds, double drawOdds, double awayOdds)
    {
        if (homeOdds <= 1 || drawOdds <= 1 || awayOdds <= 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas 1X2 deben ser mayores que 1.");
        }

        var rawHome = 1.0 / homeOdds;
        var rawDraw = 1.0 / drawOdds;
        var rawAway = 1.0 / awayOdds;
        var total = rawHome + rawDraw + rawAway;

        if (total < 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas 1X2 implican margen negativo para el bookmaker. Revisa los valores antes de calcular.");
        }

        return new MarketProbabilities(rawHome / total, rawDraw / total, rawAway / total);
    }

    public OverUnderMarketProbabilities ToNoMarginOverUnderProbabilities(double line, double overOdds, double underOdds)
    {
        if (overOdds <= 1 || underOdds <= 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas Over/Under deben ser mayores que 1.");
        }

        var rawOver = 1.0 / overOdds;
        var rawUnder = 1.0 / underOdds;
        var total = rawOver + rawUnder;

        if (total < 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas Over/Under implican margen negativo para el bookmaker. Revisa los valores antes de calcular.");
        }

        return new OverUnderMarketProbabilities(line, rawOver / total, rawUnder / total);
    }

    public BothTeamsToScoreMarketProbabilities ToNoMarginBothTeamsToScoreProbabilities(double yesOdds, double noOdds)
    {
        if (yesOdds <= 1 || noOdds <= 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas BTTS deben ser mayores que 1.");
        }

        var rawYes = 1.0 / yesOdds;
        var rawNo = 1.0 / noOdds;
        var total = rawYes + rawNo;

        if (total < 1)
        {
            throw new ScoreMatrixValidationException("Las cuotas BTTS implican margen negativo para el bookmaker. Revisa los valores antes de calcular.");
        }

        return new BothTeamsToScoreMarketProbabilities(rawYes / total, rawNo / total);
    }
}
