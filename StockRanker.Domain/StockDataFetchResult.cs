namespace StockRanker.Domain;

public sealed record StockDataFetchResult(
    StockCompany Company,
    decimal? CurrentPrice,
    decimal? LatestClose,
    IReadOnlyList<StockPricePoint> HistoricalCloses,
    bool IsSuccess,
    string? ErrorMessage)
{
    public bool HasUsablePrice => CurrentPrice.HasValue || LatestClose.HasValue;
}
