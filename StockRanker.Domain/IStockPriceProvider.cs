namespace StockRanker.Domain;

public interface IStockPriceProvider
{
    Task<IReadOnlyList<StockCompany>> GetTrackedCompaniesAsync(CancellationToken cancellationToken = default);
    Task<StockDataFetchResult> GetStockDataAsync(string symbol, CancellationToken cancellationToken = default);
}
