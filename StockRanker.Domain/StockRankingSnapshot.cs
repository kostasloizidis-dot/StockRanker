namespace StockRanker.Domain;

public sealed record StockRankingSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<StockRanking> Rankings,
    bool HasFetchErrors,
    IReadOnlyList<string> Messages);
