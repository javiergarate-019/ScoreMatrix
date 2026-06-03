using System.Globalization;
using System.Text;
using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class CsvExporter
{
    public string Export(ScoreMatrixResult result)
    {
        var builder = new StringBuilder();
        builder.AppendLine("HomeGoals,AwayGoals,Probability,FairOdds,OddsWithMargin");

        foreach (var score in result.Scores.OrderBy(score => score.HomeGoals).ThenBy(score => score.AwayGoals))
        {
            builder.Append(score.HomeGoals);
            builder.Append(',');
            builder.Append(score.AwayGoals);
            builder.Append(',');
            builder.Append(score.Probability.ToString("0.########", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.Append(score.FairOdds.ToString("0.####", CultureInfo.InvariantCulture));
            builder.Append(',');
            builder.AppendLine(ApplyMargin(score.Probability, result.BookmakerMarginPercent).ToString("0.####", CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }

    private static double ApplyMargin(double probability, double marginPercent)
        => probability <= 0 ? double.PositiveInfinity : (1.0 / probability) / (1.0 + marginPercent / 100.0);
}
