using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using StockRanker.Domain;

namespace StockRanker.Infrastructure;

public sealed class FinnhubStockPriceProvider : IStockPriceProvider
{
    private const string MissingApiKeyMessage = "Finnhub API key is not configured. Set Finnhub:ApiKey to load live rankings.";

    private readonly HttpClient _httpClient;
    private readonly FinnhubOptions _options;
    private readonly JsonStockPriceProviderOptions _jsonOptions;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public FinnhubStockPriceProvider(
        HttpClient httpClient,
        IOptions<FinnhubOptions> options,
        IOptions<JsonStockPriceProviderOptions> jsonOptions)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
        _jsonOptions = (jsonOptions ?? throw new ArgumentNullException(nameof(jsonOptions))).Value;
    }

    public Task<IReadOnlyList<StockCompany>> GetTrackedCompaniesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Snp500Companies.List);

    public async Task<StockDataFetchResult> GetStockDataAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var company = Snp500Companies.List.FirstOrDefault(company => string.Equals(company.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (company is null)
        {
            return new StockDataFetchResult(
                new StockCompany(symbol, symbol),
                CurrentPrice: null,
                LatestClose: null,
                HistoricalCloses: Array.Empty<StockPricePoint>(),
                IsSuccess: false,
                ErrorMessage: "Symbol not tracked.");
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey) || _options.ApiKey == "YOUR_FINNHUB_API_KEY")
        {
            return new StockDataFetchResult(company, null, null, Array.Empty<StockPricePoint>(), false, MissingApiKeyMessage);
        }

        var quote = await GetQuoteAsync(symbol, cancellationToken);
        if (quote is null)
        {
            return new StockDataFetchResult(company, null, null, Array.Empty<StockPricePoint>(), false, "Failed to fetch quote.");
        }

        var closes = await GetHistoricalClosesAsync(symbol, cancellationToken);
        if (!closes.Any())
        {
            closes = await GetSeededHistoricalClosesAsync(symbol, quote.CurrentPrice, cancellationToken);
        }

        if (!closes.Any())
        {
            return new StockDataFetchResult(company, quote.CurrentPrice, quote.LatestClose, Array.Empty<StockPricePoint>(), false, "No historical close data.");
        }

        return new StockDataFetchResult(company, quote.CurrentPrice, quote.LatestClose, closes, true, null);
    }

    private async Task<QuoteResponse?> GetQuoteAsync(string symbol, CancellationToken cancellationToken)
    {
        var requestUrl = $"{_options.BaseUrl}/quote?symbol={Uri.EscapeDataString(symbol)}&token={_options.ApiKey}";
        try
        {
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var content = await response.Content.ReadFromJsonAsync<QuoteResponse>(cancellationToken: cancellationToken);
            if (content is null || (content.CurrentPrice <= 0m && content.LatestClose <= 0m))
            {
                return null;
            }

            return content;
        }
        catch
        {
            return null;
        }
    }

    private async Task<IReadOnlyList<StockPricePoint>> GetHistoricalClosesAsync(string symbol, CancellationToken cancellationToken)
    {
        var to = DateTimeOffset.UtcNow;
        var from = to.AddDays(-183);
        var fromEpoch = new DateTimeOffset(from.UtcDateTime).ToUnixTimeSeconds();
        var toEpoch = new DateTimeOffset(to.UtcDateTime).ToUnixTimeSeconds();
        var requestUrl = $"{_options.BaseUrl}/stock/candle?symbol={Uri.EscapeDataString(symbol)}&resolution=D&from={fromEpoch}&to={toEpoch}&token={_options.ApiKey}";

        try
        {
            var response = await _httpClient.GetAsync(requestUrl, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return Array.Empty<StockPricePoint>();
            }

            var content = await response.Content.ReadFromJsonAsync<CandleResponse>(cancellationToken: cancellationToken);
            if (content is null || content.Status != "ok" || content.ClosePrices == null || content.Timestamps == null)
            {
                return Array.Empty<StockPricePoint>();
            }

            var count = Math.Min(content.ClosePrices.Count, content.Timestamps.Count);
            var points = new List<StockPricePoint>(count);
            for (var index = 0; index < count; index++)
            {
                points.Add(new StockPricePoint(DateTimeOffset.FromUnixTimeSeconds(content.Timestamps[index]), content.ClosePrices[index]));
            }

            return points;
        }
        catch
        {
            return Array.Empty<StockPricePoint>();
        }
    }

    private async Task<IReadOnlyList<StockPricePoint>> GetSeededHistoricalClosesAsync(
        string symbol,
        decimal currentPrice,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(_jsonOptions.FilePath))
        {
            return Array.Empty<StockPricePoint>();
        }

        var json = await File.ReadAllTextAsync(_jsonOptions.FilePath, cancellationToken);
        var prices = JsonSerializer.Deserialize<IReadOnlyList<JsonStockPrice>>(json, _serializerOptions) ?? Array.Empty<JsonStockPrice>();
        var price = prices.FirstOrDefault(price => string.Equals(price.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (price is null || price.SixMonthLow <= 0m)
        {
            return Array.Empty<StockPricePoint>();
        }

        var now = DateTimeOffset.UtcNow;
        return new[]
        {
            new StockPricePoint(now.AddDays(-183), price.SixMonthLow),
            new StockPricePoint(now, currentPrice)
        };
    }

    private sealed class QuoteResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("c")]
        public decimal CurrentPrice { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("pc")]
        public decimal LatestClose { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("t")]
        public long Timestamp { get; set; }
    }

    private sealed class CandleResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("c")]
        public IReadOnlyList<decimal>? ClosePrices { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("t")]
        public IReadOnlyList<long>? Timestamps { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("s")]
        public string? Status { get; set; }
    }

    private sealed record JsonStockPrice(
        string Symbol,
        string CompanyName,
        decimal CurrentPrice,
        decimal SixMonthLow);
}
