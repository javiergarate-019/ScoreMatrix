using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public interface IScoreModel
{
    IReadOnlyList<ScoreProbability> Calculate(double lambdaHome, double lambdaAway, int maxGoals);
}
