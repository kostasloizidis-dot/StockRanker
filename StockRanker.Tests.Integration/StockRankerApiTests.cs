using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using StockRanker.Domain;

namespace StockRanker.Tests.Integration;

public class StockRankerApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public StockRankerApiTests(WebApplicationFactory<Program> factory)
    {
        var appFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IStockPriceProvider>(new FakeStockPriceProvider());
                services.AddSingleton<IStockRankingCache>(new InMemoryRankingCache());
            });
        });

        _client = appFactory.CreateClient();
    }

    [Fact]
    public async Task GetRankings_ReturnsOkAndSnapshot()
    {
        var response = await _client.GetAsync("/api/stocks/rankings");
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<StockRankingSnapshot>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        });
        Assert.NotNull(snapshot);
        Assert.NotNull(snapshot.Rankings);
    }

    [Fact]
    public async Task Refresh_ReturnsUpdatedSnapshot()
    {
        var response = await _client.PostAsync("/api/stocks/refresh", null);
        response.EnsureSuccessStatusCode();

        var snapshot = await response.Content.ReadFromJsonAsync<StockRankingSnapshot>(new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        });
        Assert.NotNull(snapshot);
        Assert.NotEmpty(snapshot.Rankings);
    }

    private sealed class FakeStockPriceProvider : IStockPriceProvider
    {
        public Task<IReadOnlyList<StockCompany>> GetTrackedCompaniesAsync(CancellationToken cancellationToken = default)
            => Task.FromResult((IReadOnlyList<StockCompany>)new[] { new StockCompany("AAA", "Alpha") });

        public Task<StockDataFetchResult> GetStockDataAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var closes = new[]
            {
                new StockPricePoint(DateTimeOffset.UtcNow.AddDays(-180), 90m),
                new StockPricePoint(DateTimeOffset.UtcNow.AddDays(-150), 92m),
                new StockPricePoint(DateTimeOffset.UtcNow.AddDays(-30), 95m)
            };

            return Task.FromResult(new StockDataFetchResult(
                new StockCompany(symbol, "Alpha"),
                CurrentPrice: 100m,
                LatestClose: 98m,
                HistoricalCloses: closes,
                IsSuccess: true,
                ErrorMessage: null));
        }
    }

    private sealed class InMemoryRankingCache : IStockRankingCache
    {
        private StockRankingSnapshot? _snapshot;

        public Task<StockRankingSnapshot?> ReadAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(_snapshot);

        public Task WriteAsync(StockRankingSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            _snapshot = snapshot;
            return Task.CompletedTask;
        }
    }
}
