namespace StockRanker.Domain;

public interface IStockRankingCache
{
    Task<StockRankingSnapshot?> ReadAsync(CancellationToken cancellationToken = default);
    Task WriteAsync(StockRankingSnapshot snapshot, CancellationToken cancellationToken = default);
}
