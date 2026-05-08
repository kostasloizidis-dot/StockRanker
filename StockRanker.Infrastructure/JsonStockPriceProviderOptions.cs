namespace StockRanker.Infrastructure;

public sealed class JsonStockPriceProviderOptions
{
    public string FilePath { get; set; } = "data/stocks.json";
}
