using System.Diagnostics.Metrics;
using ActionCache;
using ActionCache.Common.Caching;
using ActionCache.Common.Diagnostics;
using ActionCache.Utilities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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

        public List<(double Value, string? Operation, string? Outcome)> Durations { get; } = [];

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

            _listener.SetMeasurementEventCallback<double>((_, value, tags, _) =>
            {
                string? operation = null;
                string? outcome = null;
                foreach (var tag in tags)
                {
                    if (tag.Key == "operation")
                    {
                        operation = tag.Value?.ToString();
                    }
                    else if (tag.Key == "outcome")
                    {
                        outcome = tag.Value?.ToString();
                    }
                }

                Durations.Add((value, operation, outcome));
            });

            _listener.Start();
        }

        public void Dispose() => _listener.Dispose();
    }

    [Test]
    public async Task GetAsync_OnTheDecorator_RecordsNoRequestOutcome()
    {
        // One logical lookup reads the backend more than once — single flight re-checks
        // under the lock, and a layered chain reads every layer. Counting outcomes here
        // made actioncache.requests a count of backend reads, so the published hit ratio
        // was not the hit ratio. The filters record it once per request instead.
        using var collector = new Collector("actioncache.requests");

        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(GetAsync_OnTheDecorator_RecordsNoRequestOutcome))!);

        await cache.GetAsync<string>("Key");
        await cache.SetAsync("Key", "Value");
        await cache.GetAsync<string>("Key");

        collector.Measurements.Should().BeEmpty();
    }

    [Test]
    public async Task EveryOperation_RecordsItsDuration()
    {
        // The histogram used to be recorded on GetAsync's success path alone.
        using var collector = new Collector("actioncache.operation.duration");

        var cache = ResilientCacheBuilder.Decorate(
            _factory.Create(nameof(EveryOperation_RecordsItsDuration))!);

        await cache.GetAsync<string>("Key");
        await cache.SetAsync("Key", "Value");
        await cache.RemoveAsync("Key");
        await cache.GetKeysAsync();
        await cache.RemoveAsync();

        collector.Durations.Select(duration => duration.Operation).Should().BeEquivalentTo(
            ["GetAsync", "SetAsync", "RemoveKey", "GetKeysAsync", "EvictNamespace"]);
        collector.Durations.Should().OnlyContain(duration => duration.Outcome == "ok");
    }

    [Test]
    public async Task AnOperationThatFails_StillRecordsItsDuration()
    {
        // A backend that hangs or throws is exactly what the histogram exists to show; a
        // failure that contributes no sample makes the latency look healthy.
        using var collector = new Collector("actioncache.operation.duration");

        var inner = new Mock<IActionCache>();
        inner.Setup(cache => cache.GetNamespace()).Returns(new Namespace("Test"));
        inner.Setup(cache => cache.GetAsync<string>(It.IsAny<string>(), It.IsAny<CancellationToken>()))
             .ThrowsAsync(new InvalidOperationException("backend down"));

        var cache = new ResilientActionCache(inner.Object, NullLogger.Instance, failClosed: false);

        await cache.GetAsync<string>("Key");

        collector.Durations.Should().ContainSingle()
            .Which.Outcome.Should().Be("error");
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
