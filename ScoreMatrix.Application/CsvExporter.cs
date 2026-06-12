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

        if (result.PencaRanking.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("PencaRanking");
            builder.AppendLine("HomeGoals,AwayGoals,ExpectedPoints,ExactProbability");

            foreach (var recommendation in result.PencaRanking)
            {
                builder.Append(recommendation.HomeGoals);
                builder.Append(',');
                builder.Append(recommendation.AwayGoals);
                builder.Append(',');
                builder.Append(recommendation.ExpectedPoints.ToString("0.######", CultureInfo.InvariantCulture));
                builder.Append(',');
                builder.AppendLine(recommendation.ExactProbability.ToString("0.########", CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    public string ExportBacktest(BacktestReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Modelo,{report.ModelType}");
        builder.AppendLine($"De-vig,{report.DevigMethod}");
        builder.AppendLine($"Rho,{report.Rho.ToString("0.####", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"AutoCalibrado,{report.AutoCalibratedRho}");
        builder.AppendLine($"RPS medio,{report.MeanRps.ToString("0.######", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Brier medio,{report.MeanBrier.ToString("0.######", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"LogLoss medio,{report.MeanLogLoss.ToString("0.######", CultureInfo.InvariantCulture)}");
        builder.AppendLine($"Partidos,{report.Matches.Count}");
        builder.AppendLine();

        builder.AppendLine("Home,Away,ProbHome,ProbEmpate,ProbAway,LambdaHome,LambdaAway,Dep,ResultadoReal,RPS,Brier");
        foreach (var m in report.Matches)
        {
            builder.Append(m.HomeTeam).Append(',');
            builder.Append(m.AwayTeam).Append(',');
            builder.Append(m.HomeWinProb.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(m.DrawProb.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(m.AwayWinProb.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(m.LambdaHome.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(m.LambdaAway.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(m.EstimatedDependence.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
            builder.Append(m.Score).Append(',');
            builder.Append(m.Rps.ToString("0.######", CultureInfo.InvariantCulture)).Append(',');
            builder.AppendLine(m.Brier.ToString("0.######", CultureInfo.InvariantCulture));
        }

        if (report.CalibrationCurve.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("CalibrationCurve");
            builder.AppendLine("Midpoint,MeanForecast,ObservedFrequency,Count");
            foreach (var bin in report.CalibrationCurve)
            {
                builder.Append(bin.Midpoint.ToString("0.##", CultureInfo.InvariantCulture)).Append(',');
                builder.Append(bin.MeanForecast.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
                builder.Append(bin.ObservedFrequency.ToString("0.####", CultureInfo.InvariantCulture)).Append(',');
                builder.AppendLine(bin.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        return builder.ToString();
    }

    private static double ApplyMargin(double probability, double marginPercent)
        => probability <= 0 ? double.PositiveInfinity : (1.0 / probability) / (1.0 + marginPercent / 100.0);
}
