namespace StockRanker.Infrastructure;

public sealed class StockRankingCacheOptions
{
    public string CacheFilePath { get; set; } = "cache/stockRankingSnapshot.json";
}
