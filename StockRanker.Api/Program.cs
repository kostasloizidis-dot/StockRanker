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
builder.Services.Configure<StockRankingCacheOptions>(builder.Configuration.GetSection("StockRankingCache"));

builder.Services.AddSingleton<IStockPriceProvider, JsonStockPriceProvider>();
builder.Services.AddSingleton<IStockRankingCache>(sp =>
{
    var options = sp.GetRequiredService<IOptions<StockRankingCacheOptions>>().Value;
    return new FileStockRankingCache(options.CacheFilePath);
});
builder.Services.AddSingleton<IStockRankingService, StockRankingService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.MapGet("/api/stocks/rankings", async (IStockRankingService ranker) => Results.Ok(await ranker.GetLatestRankingsAsync()))
    .WithName("GetStockRankings");

app.MapPost("/api/stocks/refresh", async (IStockRankingService ranker) => Results.Ok(await ranker.RefreshRankingsAsync()))
    .WithName("RefreshStockRankings");

app.Run();

public partial class Program { }
