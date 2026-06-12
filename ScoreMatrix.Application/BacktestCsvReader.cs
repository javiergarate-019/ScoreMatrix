using System.Globalization;

namespace ScoreMatrix.Application;

/// <summary>
/// Reads historical match records from CSV.
/// Required columns (case-insensitive): home, away, homeOdds, drawOdds, awayOdds, actualHome, actualAway.
/// Optional columns: overOdds, underOdds, ouLine, bttsYes, bttsNo.
/// </summary>
public static class BacktestCsvReader
{
    public static IReadOnlyList<MatchRecord> Read(string csvContent)
    {
        var lines = csvContent
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0)
            .ToArray();

        if (lines.Length < 2)
        {
            throw new ScoreMatrixValidationException("El CSV debe tener al menos una fila de encabezado y una de datos.");
        }

        var headers = lines[0].Split(',').Select(h => h.Trim().ToLowerInvariant()).ToArray();

        int Idx(string name) => Array.IndexOf(headers, name);

        var homeIdx = Idx("home");
        var awayIdx = Idx("away");
        var homeOddsIdx = Idx("homeodds");
        var drawOddsIdx = Idx("drawodds");
        var awayOddsIdx = Idx("awayodds");
        var actualHomeIdx = Idx("actualhome");
        var actualAwayIdx = Idx("actualaway");

        if (new[] { homeOddsIdx, drawOddsIdx, awayOddsIdx, actualHomeIdx, actualAwayIdx }.Any(i => i < 0))
        {
            throw new ScoreMatrixValidationException(
                "El CSV debe tener columnas: homeOdds, drawOdds, awayOdds, actualHome, actualAway. " +
                "Opcionales: home, away, overOdds, underOdds, ouLine, bttsYes, bttsNo.");
        }

        var ouLineIdx = Idx("ouline");
        var overOddsIdx = Idx("overodds");
        var underOddsIdx = Idx("underodds");
        var bttsYesIdx = Idx("bttsyes");
        var bttsNoIdx = Idx("bttsno");

        var records = new List<MatchRecord>();

        for (var row = 1; row < lines.Length; row++)
        {
            var cols = lines[row].Split(',');

            double GetDouble(int idx, double fallback = 0)
            {
                if (idx < 0 || idx >= cols.Length) return fallback;
                return double.TryParse(cols[idx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : fallback;
            }

            int GetInt(int idx)
            {
                if (idx < 0 || idx >= cols.Length) return 0;
                return int.TryParse(cols[idx].Trim(), out var v) ? v : 0;
            }

            string GetStr(int idx, string fallback)
            {
                if (idx < 0 || idx >= cols.Length) return fallback;
                var s = cols[idx].Trim();
                return string.IsNullOrEmpty(s) ? fallback : s;
            }

            var homeOdds = GetDouble(homeOddsIdx);
            var drawOdds = GetDouble(drawOddsIdx);
            var awayOdds = GetDouble(awayOddsIdx);

            if (homeOdds <= 1 || drawOdds <= 1 || awayOdds <= 1) continue;

            var overOdds = GetDouble(overOddsIdx);
            var underOdds = GetDouble(underOddsIdx);
            var hasOU = overOdds > 1 && underOdds > 1;

            var bttsYes = GetDouble(bttsYesIdx);
            var bttsNo = GetDouble(bttsNoIdx);
            var hasBtts = bttsYes > 1 && bttsNo > 1;

            records.Add(new MatchRecord
            {
                HomeTeam = GetStr(homeIdx, "Local"),
                AwayTeam = GetStr(awayIdx, "Visitante"),
                HomeOdds = homeOdds,
                DrawOdds = drawOdds,
                AwayOdds = awayOdds,
                HasOverUnder = hasOU,
                OverUnderLine = GetDouble(ouLineIdx, 2.5),
                OverOdds = overOdds,
                UnderOdds = underOdds,
                HasBtts = hasBtts,
                BttsYesOdds = bttsYes,
                BttsNoOdds = bttsNo,
                ActualHomeGoals = GetInt(actualHomeIdx),
                ActualAwayGoals = GetInt(actualAwayIdx)
            });
        }

        if (records.Count == 0)
        {
            throw new ScoreMatrixValidationException("No se encontraron filas validas en el CSV. Verifica el formato y los valores de cuotas.");
        }

        return records;
    }
}
