using StockRanker.Application;
using StockRanker.Domain;

namespace StockRanker.Tests.Unit;

public class StockRankerTests
{
    [Fact]
    public async Task RefreshRankings_CalculatesScoreAndSortsByScoreThenName()
    {
        var companies = new[]
        {
            new StockCompany("AAA", "Beta"),
            new StockCompany("BBB", "Alpha")
        };

        var provider = new FakeStockPriceProvider(companies, new[]
        {
            CreateResult("AAA", "Beta", 100m, new[] { 50m, 80m, 70m }),
            CreateResult("BBB", "Alpha", 100m, new[] { 50m, 100m, 120m })
        });

        var cache = new InMemoryRankingCache();
        var ranker = new StockRankingService(provider, cache);

        var snapshot = await ranker.RefreshRankingsAsync();

        Assert.Equal(2, snapshot.Rankings.Count);
        Assert.Equal("Alpha", snapshot.Rankings[0].CompanyName);
        Assert.Equal(50m, snapshot.Rankings[0].Score);
        Assert.Equal(50m, snapshot.Rankings[1].Score);
    }

    [Fact]
    public async Task RefreshRankings_UsesCachedRankingWhenFetchFails()
    {
        var companies = new[]
        {
            new StockCompany("AAA", "Alpha")
        };

        var provider = new FakeStockPriceProvider(companies, Array.Empty<StockDataFetchResult>());
        var cachedRanking = new StockRanking(
            "AAA",
            "Alpha",
            CurrentPrice: 100m,
            SixMonthLow: 80m,
            Score: 80m,
            Rank: 1,
            Label: StockRankingLabel.BuyYesterday,
            IsIncomplete: false,
            Status: StockRefreshStatus.Fresh,
            PriceTimestamp: DateTimeOffset.UtcNow.AddMinutes(-10),
            GeneratedAt: DateTimeOffset.UtcNow.AddMinutes(-10));

        var cache = new InMemoryRankingCache(new StockRankingSnapshot(DateTimeOffset.UtcNow.AddMinutes(-10), new[] { cachedRanking }, false, new string[0]));
        var ranker = new StockRankingService(provider, cache);

        var snapshot = await ranker.RefreshRankingsAsync();

        Assert.Single(snapshot.Rankings);
        Assert.Equal(StockRefreshStatus.LatestFetchFailed, snapshot.Rankings[0].Status);
        Assert.True(snapshot.HasFetchErrors);
        Assert.Contains("Latest fetch failed", snapshot.Messages[0]);
    }

    [Fact]
    public async Task RefreshRankings_MarksShortHistoryAsIncomplete()
    {
        var companies = new[]
        {
            new StockCompany("AAA", "Alpha")
        };

        var provider = new FakeStockPriceProvider(companies, new[]
        {
            CreateResult("AAA", "Alpha", 100m, new[] { 100m, 98m }, daysAgo: 30)
        });

        var cache = new InMemoryRankingCache();
        var ranker = new StockRankingService(provider, cache);

        var snapshot = await ranker.RefreshRankingsAsync();

        Assert.Single(snapshot.Rankings);
        Assert.True(snapshot.Rankings[0].IsIncomplete);
    }

    private static StockDataFetchResult CreateResult(string symbol, string companyName, decimal currentPrice, decimal[] closes, int daysAgo = 200)
    {
        var date = DateTimeOffset.UtcNow.AddDays(-daysAgo);
        var points = closes.Select((close, index) => new StockPricePoint(date.AddDays(index), close)).ToArray();
        return new StockDataFetchResult(
            new StockCompany(symbol, companyName),
            CurrentPrice: currentPrice,
            LatestClose: closes.Last(),
            HistoricalCloses: points,
            IsSuccess: true,
            ErrorMessage: null);
    }

    private sealed class FakeStockPriceProvider : IStockPriceProvider
    {
        private readonly IReadOnlyList<StockCompany> _companies;
        private readonly IReadOnlyDictionary<string, StockDataFetchResult> _results;

        public FakeStockPriceProvider(IEnumerable<StockCompany> companies, IEnumerable<StockDataFetchResult> results)
        {
            _companies = companies.ToArray();
            _results = results.ToDictionary(result => result.Company.Symbol, StringComparer.OrdinalIgnoreCase);
        }

        public Task<IReadOnlyList<StockCompany>> GetTrackedCompaniesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_companies);

        public Task<StockDataFetchResult> GetStockDataAsync(string symbol, CancellationToken cancellationToken = default)
        {
            if (_results.TryGetValue(symbol, out var result))
            {
                return Task.FromResult(result);
            }

            return Task.FromResult(new StockDataFetchResult(
                new StockCompany(symbol, symbol),
                CurrentPrice: null,
                LatestClose: null,
                HistoricalCloses: Array.Empty<StockPricePoint>(),
                IsSuccess: false,
                ErrorMessage: "Not configured"));
        }
    }

    private sealed class InMemoryRankingCache : IStockRankingCache
    {
        private StockRankingSnapshot? _snapshot;

        public InMemoryRankingCache(StockRankingSnapshot? snapshot = null)
        {
            _snapshot = snapshot;
        }

        public Task<StockRankingSnapshot?> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);

        public Task WriteAsync(StockRankingSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _snapshot = snapshot;
            return Task.CompletedTask;
        }
    }
}
