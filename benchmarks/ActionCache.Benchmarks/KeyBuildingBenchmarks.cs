using ActionCache.Common.Keys;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Routing;

namespace ActionCache.Benchmarks;

/// <summary>
/// Key construction runs on every cached request, hit or miss, so it is the one piece of
/// ActionCache that is never amortised.
/// </summary>
[MemoryDiagnoser]
public class KeyBuildingBenchmarks
{
    private readonly RouteValueDictionary _routeValues = new()
    {
        { "controller", "Accounts" },
        { "action", "Get" },
        { "id", "42" }
    };

    private readonly Dictionary<string, object?> _actionArguments = new()
    {
        { "id", 42 },
        { "includeArchived", false },
        { "page", 3 }
    };

    private readonly SortedDictionary<string, string?> _varyByValues = new()
    {
        { "user", "1a2b3c4d-5e6f-7890-abcd-ef1234567890" }
    };

    [Benchmark(Baseline = true)]
    public string Hashed() =>
        new ActionCacheKeyBuilder()
            .WithRouteValues(_routeValues)
            .WithActionArguments(_actionArguments)
            .Build();

    [Benchmark]
    public string HashedWithVaryBy() =>
        new ActionCacheKeyBuilder()
            .WithRouteValues(_routeValues)
            .WithActionArguments(_actionArguments)
            .WithVaryByValues(_varyByValues)
            .Build();

    [Benchmark]
    public string Plaintext() =>
        new ActionCacheKeyBuilder(usePlaintextKeys: true)
            .WithRouteValues(_routeValues)
            .WithActionArguments(_actionArguments)
            .Build();
}
