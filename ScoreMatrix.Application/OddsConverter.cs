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
}
