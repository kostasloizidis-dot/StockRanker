using System.Text.Json;
using Microsoft.Extensions.Options;
using StockRanker.Domain;

namespace StockRanker.Infrastructure;

public sealed class JsonStockPriceProvider : IStockPriceProvider
{
    private readonly JsonStockPriceProviderOptions _options;
    private readonly JsonSerializerOptions _serializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public JsonStockPriceProvider(IOptions<JsonStockPriceProviderOptions> options)
    {
        _options = (options ?? throw new ArgumentNullException(nameof(options))).Value;
    }

    public async Task<IReadOnlyList<StockCompany>> GetTrackedCompaniesAsync(CancellationToken cancellationToken = default)
    {
        var prices = await ReadPricesAsync(cancellationToken);
        return prices
            .Where(price => !string.IsNullOrWhiteSpace(price.Symbol))
            .Select(price => new StockCompany(price.Symbol, price.CompanyName))
            .ToList();
    }

    public async Task<StockDataFetchResult> GetStockDataAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var prices = await ReadPricesAsync(cancellationToken);
        var price = prices.FirstOrDefault(price => string.Equals(price.Symbol, symbol, StringComparison.OrdinalIgnoreCase));
        if (price is null)
        {
            return new StockDataFetchResult(
                new StockCompany(symbol, symbol),
                CurrentPrice: null,
                LatestClose: null,
                HistoricalCloses: Array.Empty<StockPricePoint>(),
                IsSuccess: false,
                ErrorMessage: "Symbol not found in JSON price file.");
        }

        var company = new StockCompany(price.Symbol, price.CompanyName);
        var currentPrice = price.CurrentPrice.GetValueOrDefault(price.PriceSixMonthsAgo);
        if (currentPrice <= 0 || price.PriceSixMonthsAgo <= 0)
        {
            return new StockDataFetchResult(company, null, null, Array.Empty<StockPricePoint>(), false, "Prices must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;
        var historicalCloses = new[]
        {
            new StockPricePoint(now.AddDays(-183), price.PriceSixMonthsAgo)
        };

        return new StockDataFetchResult(
            company,
            currentPrice,
            LatestClose: currentPrice,
            HistoricalCloses: historicalCloses,
            IsSuccess: true,
            ErrorMessage: null);
    }

    private async Task<IReadOnlyList<JsonStockPrice>> ReadPricesAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_options.FilePath))
        {
            return Array.Empty<JsonStockPrice>();
        }

        var json = await File.ReadAllTextAsync(_options.FilePath, cancellationToken);
        return JsonSerializer.Deserialize<IReadOnlyList<JsonStockPrice>>(json, _serializerOptions) ?? Array.Empty<JsonStockPrice>();
    }

    private sealed class JsonStockPrice
    {
        public string? SymbolValue { get; set; }
        public string? Ticker { get; set; }
        public string? CompanyNameValue { get; set; }
        public string? Company { get; set; }
        public decimal? CurrentPrice { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("six_month_low")]
        public decimal? SixMonthLow { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("price_6_months_ago")]
        public decimal? PriceSixMonthsAgoValue { get; set; }

        [System.Text.Json.Serialization.JsonPropertyName("symbol")]
        public string Symbol
        {
            get => SymbolValue ?? Ticker ?? string.Empty;
            set => SymbolValue = value;
        }

        [System.Text.Json.Serialization.JsonPropertyName("companyName")]
        public string CompanyName
        {
            get => CompanyNameValue ?? Company ?? Symbol;
            set => CompanyNameValue = value;
        }

        public decimal PriceSixMonthsAgo => PriceSixMonthsAgoValue ?? SixMonthLow ?? 0m;
    }
}
