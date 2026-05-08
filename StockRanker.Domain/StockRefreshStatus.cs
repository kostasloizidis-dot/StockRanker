namespace StockRanker.Domain;

public enum StockRefreshStatus
{
    Fresh,
    LatestFetchFailed,
    Cached,
    MissingData
}
