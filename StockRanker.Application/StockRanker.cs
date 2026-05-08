using StockRanker.Domain;

namespace StockRanker.Application;

public sealed class StockRankingService : IStockRankingService
{
    private readonly IStockPriceProvider _priceProvider;
    private readonly IStockRankingCache _cache;

    public StockRankingService(IStockPriceProvider priceProvider, IStockRankingCache cache)
    {
        _priceProvider = priceProvider ?? throw new ArgumentNullException(nameof(priceProvider));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<StockRankingSnapshot> GetLatestRankingsAsync(CancellationToken cancellationToken = default)
    {
        var cached = await _cache.ReadAsync(cancellationToken);
        if (cached is not null && cached.Rankings.Count > 0)
        {
            return cached;
        }

        return await RefreshRankingsAsync(cancellationToken);
    }

    public async Task<StockRankingSnapshot> RefreshRankingsAsync(CancellationToken cancellationToken = default)
    {
        var companies = await _priceProvider.GetTrackedCompaniesAsync(cancellationToken);
        var cachedSnapshot = await _cache.ReadAsync(cancellationToken);

        var fetchTasks = companies.Select(company => FetchDataAsync(company, cancellationToken)).ToArray();
        var fetchResults = await Task.WhenAll(fetchTasks);

        var rankingItems = new List<StockRanking>();
        var messages = new List<string>();

        foreach (var fetchResult in fetchResults)
        {
            if (fetchResult.IsSuccess && TryCreateRanking(fetchResult, out var ranking))
            {
                rankingItems.Add(ranking);
                continue;
            }

            var cachedItem = cachedSnapshot?.Rankings.FirstOrDefault(item => item.Symbol == fetchResult.Company.Symbol);
            if (cachedItem is not null)
            {
                rankingItems.Add(cachedItem with { Status = StockRefreshStatus.LatestFetchFailed, GeneratedAt = DateTimeOffset.UtcNow });
                messages.Add($"Latest fetch failed for {fetchResult.Company.Symbol}; using cached snapshot.");
                continue;
            }

            if (!fetchResult.IsSuccess)
            {
                var reason = string.IsNullOrWhiteSpace(fetchResult.ErrorMessage)
                    ? string.Empty
                    : $" {fetchResult.ErrorMessage}";
                messages.Add($"Latest fetch failed for {fetchResult.Company.Symbol} and no cached data is available.{reason}");
            }
        }

        messages = CondenseMessages(messages);

        if (rankingItems.Count == 0 && cachedSnapshot is not null && cachedSnapshot.Rankings.Count > 0)
        {
            var staleCache = cachedSnapshot with { GeneratedAt = DateTimeOffset.UtcNow, HasFetchErrors = true, Messages = CondenseMessages(cachedSnapshot.Messages.Concat(new[] { "Refresh failed; returning cached snapshot." })) };
            await _cache.WriteAsync(staleCache, cancellationToken);
            return staleCache;
        }

        var ranked = SortAndLabel(rankingItems);
        var snapshot = new StockRankingSnapshot(DateTimeOffset.UtcNow, ranked, messages.Any(), messages);
        await _cache.WriteAsync(snapshot, cancellationToken);
        return snapshot;
    }

    private async Task<StockDataFetchResult> FetchDataAsync(StockCompany company, CancellationToken cancellationToken)
        => await _priceProvider.GetStockDataAsync(company.Symbol, cancellationToken);

    private static bool TryCreateRanking(StockDataFetchResult fetchResult, out StockRanking ranking)
    {
        ranking = default!;

        if (!fetchResult.HistoricalCloses.Any())
        {
            return false;
        }

        var price = fetchResult.CurrentPrice ?? fetchResult.LatestClose;
        if (!price.HasValue)
        {
            return false;
        }

        var low = fetchResult.HistoricalCloses.Min(point => point.Close);
        if (low <= 0m)
        {
            return false;
        }

        var score = Math.Clamp(100m * low / price.Value, 0m, 100m);
        var isIncomplete = !HasSixMonthsOfHistory(fetchResult.HistoricalCloses);
        ranking = new StockRanking(
            fetchResult.Company.Symbol,
            fetchResult.Company.Name,
            price.Value,
            low,
            score,
            Rank: 0,
            Label: StockRankingLabel.Nahhh,
            IsIncomplete: isIncomplete,
            Status: StockRefreshStatus.Fresh,
            PriceTimestamp: DateTimeOffset.UtcNow,
            GeneratedAt: DateTimeOffset.UtcNow);

        return true;
    }

    private static bool HasSixMonthsOfHistory(IReadOnlyList<StockPricePoint> historicalCloses)
    {
        if (!historicalCloses.Any())
        {
            return false;
        }

        var oldest = historicalCloses.Min(point => point.Date);
        return oldest <= DateTimeOffset.UtcNow.AddDays(-183);
    }

    private static IReadOnlyList<StockRanking> SortAndLabel(IEnumerable<StockRanking> rankingItems)
    {
        var sorted = rankingItems
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.CompanyName, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = sorted.Count;
        if (total == 0)
        {
            return sorted;
        }

        var buyYesterdayCount = (int)Math.Floor(total * 0.15m);
        var maybeCount = (int)Math.Floor(total * 0.35m);

        for (var index = 0; index < total; index++)
        {
            var label = index < buyYesterdayCount
                ? StockRankingLabel.BuyYesterday
                : index < buyYesterdayCount + maybeCount
                    ? StockRankingLabel.Maybe
                    : StockRankingLabel.Nahhh;

            sorted[index] = sorted[index] with
            {
                Rank = index + 1,
                Label = label
            };
        }

        return sorted;
    }

    private static List<string> CondenseMessages(IEnumerable<string> messages)
    {
        var distinctMessages = messages
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (distinctMessages.Count > 1
            && distinctMessages.All(message => message.Contains("Finnhub API key is not configured", StringComparison.Ordinal)))
        {
            return new List<string> { "Finnhub API key is not configured. Set Finnhub:ApiKey to load live rankings." };
        }

        return distinctMessages;
    }
}
