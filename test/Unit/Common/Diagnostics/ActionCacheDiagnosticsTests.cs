using System.Diagnostics.Metrics;
using ActionCache;
using ActionCache.Common.Diagnostics;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Diagnostics;

[TestFixture]
public class ActionCacheDiagnosticsTests
{
    private IActionCacheFactory _factory = null!;

    [SetUp]
    public void SetUp() => _factory = MemoryActionCacheFactoryBuilder.Build();

    /// <summary>
    /// Collects one instrument from the ActionCache meter.
    /// </summary>
    private sealed class Collector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<(long Value, string? Status)> Measurements { get; } = [];

        public Collector(string instrumentName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ActionCacheDiagnostics.MeterName &&
                    instrument.Name == instrumentName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
            {
                string? status = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "status")
                    {
                        status = tag.Value?.ToString();
                    }
                }

                Measurements.Add((value, status));
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    [Test]
    public async Task GetAsync_RecordsAMissThenAHit()
    {
        using var collector = new Collector("actioncache.requests");

        // The resilient decorator is where instrumentation lives, since it wraps every
        // backend call — so the cache must be decorated to be observed.
        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(GetAsync_RecordsAMissThenAHit))!);

        await cache.GetAsync<string>("Key");
        await cache.SetAsync("Key", "Value");
        await cache.GetAsync<string>("Key");

        collector.Measurements.Select(measurement => measurement.Status)
            .Should().Equal("miss", "hit");
    }

    [Test]
    public async Task RemoveAsync_RecordsAnEviction()
    {
        using var collector = new Collector("actioncache.evictions");

        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(RemoveAsync_RecordsAnEviction))!);

        await cache.RemoveAsync();

        collector.Measurements.Should().ContainSingle();
    }

    [Test]
    public void MeterAndActivitySourceNames_AreStable()
    {
        // These are what a consumer wires into OpenTelemetry; renaming them silently
        // breaks every dashboard built on them.
        ActionCacheDiagnostics.MeterName.Should().Be("ActionCache");
        ActionCacheDiagnostics.ActivitySourceName.Should().Be("ActionCache");
    }
}
