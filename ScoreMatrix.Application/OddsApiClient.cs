using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ScoreMatrix.Domain;

namespace ScoreMatrix.Application;

public sealed class OddsApiClient(HttpClient? httpClient = null)
{
    private const string BaseUrl = "https://api.the-odds-api.com/v4";
    private const string WorldCupSportKey = "soccer_fifa_world_cup";

    private readonly HttpClient _httpClient = httpClient ?? new HttpClient();

    public async Task<IReadOnlyList<OddsApiEvent>> GetWorldCupEventsAsync(
        string apiKey,
        string region,
        CancellationToken cancellationToken = default)
    {
        ValidateApiKey(apiKey);

        var url = $"{BaseUrl}/sports/{WorldCupSportKey}/events?apiKey={Uri.EscapeDataString(apiKey)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var events = await response.Content.ReadFromJsonAsync<OddsApiEventDto[]>(cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        return events
            .Select(item => new OddsApiEvent(
                item.Id ?? string.Empty,
                item.HomeTeam ?? string.Empty,
                item.AwayTeam ?? string.Empty,
                item.CommenceTime ?? DateTimeOffset.MinValue))
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .OrderBy(item => item.CommenceTime)
            .ToArray();
    }

    public async Task<OddsApiConsensusOdds> GetConsensusOddsAsync(
        string apiKey,
        string region,
        string eventId,
        CancellationToken cancellationToken = default)
    {
        ValidateApiKey(apiKey);

        if (string.IsNullOrWhiteSpace(eventId))
        {
            throw new ScoreMatrixValidationException("Debes seleccionar un partido del Mundial 2026.");
        }

        var featuredResponse = await FetchSportOddsAsync(apiKey, region, eventId, "h2h,totals", cancellationToken)
            .ConfigureAwait(false);
        var match = featuredResponse.Match
            ?? throw new ScoreMatrixValidationException("No se encontraron cuotas para el partido seleccionado.");

        var h2h = ParseH2hConsensus(match);
        if (h2h is null)
        {
            throw new ScoreMatrixValidationException("No se pudieron obtener cuotas 1X2 validas de ningun bookmaker para este partido.");
        }

        var totals = ParseTotalsConsensus(match);
        var bttsResponse = await TryFetchEventOddsAsync(apiKey, region, eventId, "btts", cancellationToken)
            .ConfigureAwait(false);
        var btts = bttsResponse?.Match is not null ? ParseBttsConsensus(bttsResponse.Match) : null;

        var requestsRemaining = bttsResponse?.RequestsRemaining ?? featuredResponse.RequestsRemaining;

        return new OddsApiConsensusOdds(
            match.HomeTeam ?? string.Empty,
            match.AwayTeam ?? string.Empty,
            h2h.HomeOdds,
            h2h.DrawOdds,
            h2h.AwayOdds,
            h2h.BookmakerCount,
            totals?.Line,
            totals?.OverOdds,
            totals?.UnderOdds,
            totals?.BookmakerCount,
            btts?.YesOdds,
            btts?.NoOdds,
            btts?.BookmakerCount,
            requestsRemaining);
    }

    private async Task<OddsApiFetchResult> FetchSportOddsAsync(
        string apiKey,
        string region,
        string eventId,
        string markets,
        CancellationToken cancellationToken)
    {
        var url =
            $"{BaseUrl}/sports/{WorldCupSportKey}/odds" +
            $"?apiKey={Uri.EscapeDataString(apiKey)}" +
            $"&regions={Uri.EscapeDataString(region)}" +
            $"&markets={Uri.EscapeDataString(markets)}" +
            $"&oddsFormat=decimal" +
            $"&eventIds={Uri.EscapeDataString(eventId)}";

        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        await EnsureSuccessAsync(response, cancellationToken).ConfigureAwait(false);

        var events = await response.Content.ReadFromJsonAsync<OddsApiOddsEventDto[]>(cancellationToken)
            .ConfigureAwait(false)
            ?? [];

        return new OddsApiFetchResult(
            events.FirstOrDefault(),
            GetRequestsRemaining(response));
    }

    private async Task<OddsApiFetchResult?> TryFetchEventOddsAsync(
        string apiKey,
        string region,
        string eventId,
        string markets,
        CancellationToken cancellationToken)
    {
        var url =
            $"{BaseUrl}/sports/{WorldCupSportKey}/events/{Uri.EscapeDataString(eventId)}/odds" +
            $"?apiKey={Uri.EscapeDataString(apiKey)}" +
            $"&regions={Uri.EscapeDataString(region)}" +
            $"&markets={Uri.EscapeDataString(markets)}" +
            $"&oddsFormat=decimal";

        using var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var match = await response.Content.ReadFromJsonAsync<OddsApiOddsEventDto>(cancellationToken)
            .ConfigureAwait(false);

        return new OddsApiFetchResult(match, GetRequestsRemaining(response));
    }

    private static H2hConsensus? ParseH2hConsensus(OddsApiOddsEventDto match)
    {
        var bookmakerProbabilities = new List<MarketProbabilities>();

        foreach (var bookmaker in match.Bookmakers ?? [])
        {
            var market = bookmaker.Markets?.FirstOrDefault(item => item.Key == "h2h");
            if (market?.Outcomes is null || market.Outcomes.Count == 0)
            {
                continue;
            }

            var homeOutcome = market.Outcomes.FirstOrDefault(item => item.Name == match.HomeTeam);
            var awayOutcome = market.Outcomes.FirstOrDefault(item => item.Name == match.AwayTeam);
            var drawOutcome = market.Outcomes.FirstOrDefault(item => item.Name == "Draw");

            if (homeOutcome?.Price is null or <= 1 ||
                awayOutcome?.Price is null or <= 1 ||
                drawOutcome?.Price is null or <= 1)
            {
                continue;
            }

            try
            {
                var probs = Devigger.RemoveMargin(
                    [homeOutcome.Price.Value, drawOutcome.Price.Value, awayOutcome.Price.Value],
                    DevigMethod.Proportional);
                bookmakerProbabilities.Add(new MarketProbabilities(probs[0], probs[1], probs[2]));
            }
            catch (ScoreMatrixValidationException)
            {
                continue;
            }
        }

        if (bookmakerProbabilities.Count == 0)
        {
            return null;
        }

        var average = new MarketProbabilities(
            bookmakerProbabilities.Average(item => item.HomeWin),
            bookmakerProbabilities.Average(item => item.Draw),
            bookmakerProbabilities.Average(item => item.AwayWin));

        return new H2hConsensus(
            1.0 / average.HomeWin,
            1.0 / average.Draw,
            1.0 / average.AwayWin,
            bookmakerProbabilities.Count);
    }

    private static TotalsConsensus? ParseTotalsConsensus(OddsApiOddsEventDto match)
    {
        var totalsByLine = new Dictionary<double, List<(double Over, double Under)>>();

        foreach (var bookmaker in match.Bookmakers ?? [])
        {
            foreach (var market in bookmaker.Markets ?? [])
            {
                if (market.Key != "totals")
                {
                    continue;
                }

                var lineEntry = TryParseTotalsMarket(market);
                if (lineEntry is null)
                {
                    continue;
                }

                if (!totalsByLine.TryGetValue(lineEntry.Value.Line, out var entries))
                {
                    entries = [];
                    totalsByLine[lineEntry.Value.Line] = entries;
                }

                entries.Add((lineEntry.Value.Over, lineEntry.Value.Under));
            }
        }

        if (totalsByLine.Count == 0)
        {
            return null;
        }

        var modalLine = totalsByLine
            .OrderByDescending(entry => entry.Value.Count)
            .ThenBy(entry => entry.Key)
            .First();

        var overAverage = modalLine.Value.Average(item => item.Over);
        var underAverage = modalLine.Value.Average(item => item.Under);

        return new TotalsConsensus(
            modalLine.Key,
            1.0 / overAverage,
            1.0 / underAverage,
            modalLine.Value.Count);
    }

    private static BttsConsensus? ParseBttsConsensus(OddsApiOddsEventDto match)
    {
        var bttsProbabilities = new List<(double Yes, double No)>();

        foreach (var bookmaker in match.Bookmakers ?? [])
        {
            foreach (var market in bookmaker.Markets ?? [])
            {
                if (market.Key != "btts")
                {
                    continue;
                }

                var bttsEntry = TryParseTwoWayMarket(market, "Yes", "No");
                if (bttsEntry is not null)
                {
                    bttsProbabilities.Add(bttsEntry.Value);
                }
            }
        }

        if (bttsProbabilities.Count == 0)
        {
            return null;
        }

        var yesAverage = bttsProbabilities.Average(item => item.Yes);
        var noAverage = bttsProbabilities.Average(item => item.No);

        return new BttsConsensus(
            1.0 / yesAverage,
            1.0 / noAverage,
            bttsProbabilities.Count);
    }

    private static (double Line, double Over, double Under)? TryParseTotalsMarket(OddsApiMarketDto market)
    {
        if (market.Outcomes is null || market.Outcomes.Count == 0)
        {
            return null;
        }

        var overOutcome = market.Outcomes.FirstOrDefault(item => item.Name == "Over");
        var underOutcome = market.Outcomes.FirstOrDefault(item => item.Name == "Under");

        if (overOutcome?.Price is null or <= 1 ||
            underOutcome?.Price is null or <= 1 ||
            overOutcome.Point is null ||
            underOutcome.Point is null ||
            Math.Abs(overOutcome.Point.Value - underOutcome.Point.Value) > 0.001)
        {
            return null;
        }

        try
        {
            var probs = Devigger.RemoveMargin([overOutcome.Price.Value, underOutcome.Price.Value], DevigMethod.Proportional);
            return (overOutcome.Point.Value, probs[0], probs[1]);
        }
        catch (ScoreMatrixValidationException)
        {
            return null;
        }
    }

    private static (double First, double Second)? TryParseTwoWayMarket(
        OddsApiMarketDto market,
        string firstName,
        string secondName)
    {
        if (market.Outcomes is null || market.Outcomes.Count == 0)
        {
            return null;
        }

        var firstOutcome = market.Outcomes.FirstOrDefault(item => item.Name == firstName);
        var secondOutcome = market.Outcomes.FirstOrDefault(item => item.Name == secondName);

        if (firstOutcome?.Price is null or <= 1 || secondOutcome?.Price is null or <= 1)
        {
            return null;
        }

        try
        {
            var probs = Devigger.RemoveMargin([firstOutcome.Price.Value, secondOutcome.Price.Value], DevigMethod.Proportional);
            return (probs[0], probs[1]);
        }
        catch (ScoreMatrixValidationException)
        {
            return null;
        }
    }

    private static string? GetRequestsRemaining(HttpResponseMessage response)
        => response.Headers.TryGetValues("x-requests-remaining", out var remainingValues)
            ? remainingValues.FirstOrDefault()
            : null;

    private static void ValidateApiKey(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ScoreMatrixValidationException("Debes ingresar una API key de The Odds API.");
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var message = response.StatusCode switch
        {
            System.Net.HttpStatusCode.Unauthorized =>
                "API key invalida o no autorizada para The Odds API.",
            System.Net.HttpStatusCode.UnprocessableEntity =>
                "La solicitud a The Odds API no es valida. Verifica la region y el partido seleccionado.",
            System.Net.HttpStatusCode.TooManyRequests =>
                "Se alcanzo el limite de solicitudes de The Odds API. Intenta nuevamente en unos segundos.",
            _ => $"The Odds API respondio con error {(int)response.StatusCode}."
        };

        if (!string.IsNullOrWhiteSpace(body) && body.Length <= 240)
        {
            message += $" Detalle: {body}";
        }

        throw new ScoreMatrixValidationException(message);
    }

    private sealed record OddsApiFetchResult(OddsApiOddsEventDto? Match, string? RequestsRemaining);
    private sealed record H2hConsensus(double HomeOdds, double DrawOdds, double AwayOdds, int BookmakerCount);
    private sealed record TotalsConsensus(double Line, double OverOdds, double UnderOdds, int BookmakerCount);
    private sealed record BttsConsensus(double YesOdds, double NoOdds, int BookmakerCount);

    private sealed class OddsApiEventDto
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("home_team")]
        public string? HomeTeam { get; init; }

        [JsonPropertyName("away_team")]
        public string? AwayTeam { get; init; }

        [JsonPropertyName("commence_time")]
        public DateTimeOffset? CommenceTime { get; init; }
    }

    private sealed class OddsApiOddsEventDto
    {
        [JsonPropertyName("home_team")]
        public string? HomeTeam { get; init; }

        [JsonPropertyName("away_team")]
        public string? AwayTeam { get; init; }

        [JsonPropertyName("bookmakers")]
        public List<OddsApiBookmakerDto>? Bookmakers { get; init; }
    }

    private sealed class OddsApiBookmakerDto
    {
        [JsonPropertyName("markets")]
        public List<OddsApiMarketDto>? Markets { get; init; }
    }

    private sealed class OddsApiMarketDto
    {
        [JsonPropertyName("key")]
        public string? Key { get; init; }

        [JsonPropertyName("outcomes")]
        public List<OddsApiOutcomeDto>? Outcomes { get; init; }
    }

    private sealed class OddsApiOutcomeDto
    {
        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("price")]
        public double? Price { get; init; }

        [JsonPropertyName("point")]
        public double? Point { get; init; }
    }
}

public sealed record OddsApiEvent(
    string Id,
    string HomeTeam,
    string AwayTeam,
    DateTimeOffset CommenceTime);

public sealed record OddsApiConsensusOdds(
    string HomeTeam,
    string AwayTeam,
    double HomeOdds,
    double DrawOdds,
    double AwayOdds,
    int BookmakerCount,
    double? OverUnderLine,
    double? OverOdds,
    double? UnderOdds,
    int? OverUnderBookmakerCount,
    double? BttsYesOdds,
    double? BttsNoOdds,
    int? BttsBookmakerCount,
    string? RequestsRemaining);
