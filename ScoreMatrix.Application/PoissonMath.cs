namespace ScoreMatrix.Application;

public static class PoissonMath
{
    public static double Probability(int k, double lambda)
    {
        if (k < 0)
        {
            return 0;
        }

        if (lambda <= 0)
        {
            throw new ScoreMatrixValidationException("Los goles esperados deben ser mayores que 0.");
        }

        var probability = Math.Exp(-lambda);
        for (var i = 1; i <= k; i++)
        {
            probability *= lambda / i;
        }

        return probability;
    }
}
