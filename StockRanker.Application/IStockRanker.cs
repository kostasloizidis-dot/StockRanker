using StockRanker.Domain;

namespace StockRanker.Application;

public interface IStockRankingService
{
    Task<StockRankingSnapshot> GetLatestRankingsAsync(CancellationToken cancellationToken = default);
    Task<StockRankingSnapshot> RefreshRankingsAsync(CancellationToken cancellationToken = default);
}
