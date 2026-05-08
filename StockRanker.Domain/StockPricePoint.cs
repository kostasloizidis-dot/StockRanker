namespace StockRanker.Domain;

public sealed record StockPricePoint(DateTimeOffset Date, decimal Close);
