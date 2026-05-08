namespace StockRanker.Domain;

public sealed record StockRanking(
    string Symbol,
    string CompanyName,
    decimal CurrentPrice,
    decimal SixMonthLow,
    decimal Score,
    int Rank,
    StockRankingLabel Label,
    bool IsIncomplete,
    StockRefreshStatus Status,
    DateTimeOffset PriceTimestamp,
    DateTimeOffset GeneratedAt
);
