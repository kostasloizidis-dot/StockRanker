namespace StockRanker.Infrastructure;

public sealed class FinnhubOptions
{
    public string ApiKey { get; set; } = "YOUR_FINNHUB_API_KEY";
    public string BaseUrl { get; set; } = "https://finnhub.io/api/v1";
}
