using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using StockRanker.Application;
using StockRanker.Domain;
using StockRanker.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});

builder.Services.Configure<JsonStockPriceProviderOptions>(options =>
{
    builder.Configuration.GetSection("StockPrices").Bind(options);
    if (!Path.IsPathRooted(options.FilePath))
    {
        options.FilePath = Path.Combine(builder.Environment.ContentRootPath, options.FilePath);
    }
});
builder.Services.Configure<FinnhubOptions>(builder.Configuration.GetSection("Finnhub"));
builder.Services.Configure<StockRankingCacheOptions>(builder.Configuration.GetSection("StockRankingCache"));

var stockPriceProvider = builder.Configuration.GetSection("StockPriceProvider").Get<StockPriceProviderOptions>() ?? new StockPriceProviderOptions();
var finnhubApiKey = builder.Configuration["Finnhub:ApiKey"];
var useFinnhub = string.Equals(stockPriceProvider.Provider, "Finnhub", StringComparison.OrdinalIgnoreCase)
    || (string.Equals(stockPriceProvider.Provider, "Auto", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(finnhubApiKey));

if (useFinnhub)
{
    builder.Services.AddHttpClient<IStockPriceProvider, FinnhubStockPriceProvider>();
}
else
{
    builder.Services.AddSingleton<IStockPriceProvider, JsonStockPriceProvider>();
}

builder.Services.AddSingleton<IStockRankingCache>(sp =>
{
    var options = sp.GetRequiredService<IOptions<StockRankingCacheOptions>>().Value;
    return new FileStockRankingCache(options.CacheFilePath);
});
builder.Services.AddSingleton<IStockRankingService, StockRankingService>();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseHttpsRedirection();

app.MapGet("/", () => Results.Ok(new
{
    Name = "StockRanker API",
    Swagger = "/swagger",
    Endpoints = new[]
    {
        new { Method = "GET", Path = "/api/stocks/rankings", Description = "Returns the latest cached stock ranking snapshot, refreshing it if no cache exists." },
        new { Method = "POST", Path = "/api/stocks/refresh", Description = "Refreshes stock data and returns a new ranking snapshot." }
    }
}))
    .WithName("GetApiInfo");

app.MapGet("/api/stocks/rankings", async (IStockRankingService ranker) => Results.Ok(await ranker.GetLatestRankingsAsync()))
    .WithName("GetStockRankings");

app.MapPost("/api/stocks/refresh", async (IStockRankingService ranker) => Results.Ok(await ranker.RefreshRankingsAsync()))
    .WithName("RefreshStockRankings");

app.Run();

public partial class Program { }
