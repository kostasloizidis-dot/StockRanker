using System.Text.Json;
using StockRanker.Domain;

namespace StockRanker.Infrastructure;

public sealed class FileStockRankingCache : IStockRankingCache
{
    private readonly string _filePath;
    private readonly JsonSerializerOptions _options = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public FileStockRankingCache(string filePath)
    {
        _filePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
    }

    public async Task<StockRankingSnapshot?> ReadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_filePath))
        {
            return null;
        }

        var json = await File.ReadAllTextAsync(_filePath, cancellationToken);
        return JsonSerializer.Deserialize<StockRankingSnapshot>(json, _options);
    }

    public async Task WriteAsync(StockRankingSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(snapshot, _options);
        await File.WriteAllTextAsync(_filePath, json, cancellationToken);
    }
}
