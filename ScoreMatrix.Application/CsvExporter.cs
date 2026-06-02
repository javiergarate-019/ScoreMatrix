using System.Globalization;
using System.Text;
using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class CsvExporter
{
    public string Export(ScoreMatrixResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("HomeGoals,AwayGoals,Probability,FairOdds");

        foreach (var score in result.Scores.OrderBy(score => score.HomeGoals).ThenBy(score => score.AwayGoals))
        {
            builder.Append(score.HomeGoals);
            builder.Append(',');
            builder.Append(score.AwayGoals);
            builder.Append(',');
            builder.Append(score.Probability.ToString("0.########", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(score.FairOdds.ToString("0.####", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
