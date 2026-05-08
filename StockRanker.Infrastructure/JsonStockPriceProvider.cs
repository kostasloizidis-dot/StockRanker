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
        if (price.CurrentPrice <= 0 || price.SixMonthLow <= 0)
        {
            return new StockDataFetchResult(company, null, null, Array.Empty<StockPricePoint>(), false, "Prices must be greater than zero.");
        }

        var now = DateTimeOffset.UtcNow;
        var historicalCloses = new[]
        {
            new StockPricePoint(now.AddDays(-183), price.SixMonthLow),
            new StockPricePoint(now, price.CurrentPrice)
        };

        return new StockDataFetchResult(
            company,
            price.CurrentPrice,
            LatestClose: price.CurrentPrice,
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

    private sealed record JsonStockPrice(
        string Symbol,
        string CompanyName,
        decimal CurrentPrice,
        decimal SixMonthLow);
}
