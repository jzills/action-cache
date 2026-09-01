using System.Text.Json;
using ActionCache.Common.Responses;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Mvc;

namespace ActionCache.Benchmarks;

/// <summary>
/// Envelope construction on a miss, and reconstruction on a hit — the two halves of what
/// the cache adds to a request beyond the backend round-trip.
/// </summary>
[MemoryDiagnoser]
public class ResponseSerializationBenchmarks
{
    private sealed record Forecast(DateOnly Date, int TemperatureC, string Summary);

    private readonly CachedResponseFactory _factory = new(JsonSerializerOptions.Default);
    private OkObjectResult _result = null!;
    private CachedResponse _cached = null!;

    [Params(1, 50)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var forecasts = Enumerable.Range(0, ItemCount)
            .Select(index => new Forecast(
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(index)), 20 + index, "Sunny"))
            .ToArray();

        _result = new OkObjectResult(forecasts);
        _factory.TryCreate(_result, request: null, variesByRequest: false, out var cached);
        _cached = cached!;
    }

    [Benchmark]
    public CachedResponse? BuildEnvelope()
    {
        _factory.TryCreate(_result, request: null, variesByRequest: false, out var cached);
        return cached;
    }

    [Benchmark]
    public IActionResult RebuildResult() => CachedResponseFactory.ToActionResult(_cached);
}
