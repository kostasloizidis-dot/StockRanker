using StockRanker.Domain;

namespace StockRanker.Infrastructure;

public static class Snp500Companies
{
    public static readonly IReadOnlyList<StockCompany> List = new[]
    {
        new StockCompany("AAPL", "Apple Inc."),
        new StockCompany("MSFT", "Microsoft Corporation"),
        new StockCompany("AMZN", "Amazon.com, Inc."),
        new StockCompany("GOOGL", "Alphabet Inc. (Class A)"),
        new StockCompany("META", "Meta Platforms, Inc."),
        new StockCompany("NVDA", "NVIDIA Corporation"),
        new StockCompany("TSLA", "Tesla, Inc."),
        new StockCompany("BRK.B", "Berkshire Hathaway Inc. (Class B)"),
        new StockCompany("JNJ", "Johnson & Johnson"),
        new StockCompany("V", "Visa Inc."),
        new StockCompany("JPM", "JPMorgan Chase & Co."),
        new StockCompany("PG", "The Procter & Gamble Company"),
        new StockCompany("UNH", "UnitedHealth Group Incorporated"),
        new StockCompany("HD", "The Home Depot, Inc."),
        new StockCompany("MA", "Mastercard Incorporated"),
        new StockCompany("KO", "The Coca-Cola Company"),
        new StockCompany("PEP", "PepsiCo, Inc."),
        new StockCompany("DIS", "The Walt Disney Company"),
        new StockCompany("XOM", "Exxon Mobil Corporation"),
        new StockCompany("BAC", "Bank of America Corporation")
    };
}
