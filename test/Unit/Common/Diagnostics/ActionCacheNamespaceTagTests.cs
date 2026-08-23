using System.Diagnostics.Metrics;
using ActionCache.Common.Diagnostics;
using ActionCache.Utilities;
using Unit.TestUtilities.Builders;

namespace Unit.Common.Diagnostics;

/// <summary>
/// Every ActionCache instrument tags the <b>unresolved</b> namespace template.
/// </summary>
/// <remarks>
/// A templated namespace resolves per resource — <c>Account:42</c> — so tagging the resolved
/// form mints a metric time series per id. The resolved form also carries the
/// <c>ActionCache:</c> assembly prefix that <see cref="Namespace.Value"/> does not, so a
/// dashboard could not group one instrument against another even without a template.
/// </remarks>
[TestFixture]
public class ActionCacheNamespaceTagTests
{
    /// <summary>
    /// Collects the <c>namespace</c> tag from one instrument on the ActionCache meter.
    /// </summary>
    private sealed class NamespaceTagCollector : IDisposable
    {
        private readonly MeterListener _listener = new();

        public List<string?> Namespaces { get; } = [];

        public NamespaceTagCollector(string instrumentName)
        {
            _listener.InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == ActionCacheDiagnostics.MeterName &&
                    instrument.Name == instrumentName)
                {
                    listener.EnableMeasurementEvents(instrument);
                }
            };

            _listener.SetMeasurementEventCallback<long>((_, _, tags, _) => Capture(tags));
            _listener.SetMeasurementEventCallback<double>((_, _, tags, _) => Capture(tags));
            _listener.Start();
        }

        private void Capture(ReadOnlySpan<KeyValuePair<string, object?>> tags)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "namespace")
                {
                    Namespaces.Add(tag.Value?.ToString());
                }
            }
        }

        public void Dispose() => _listener.Dispose();
    }

    /// <summary>
    /// A namespace whose route template tokens have already been bound for this request.
    /// </summary>
    private static Namespace ResolvedNamespace()
    {
        var @namespace = new Namespace("Account:{id}");
        @namespace.ValueWithRouteTemplateParameters = "Account:42";
        return @namespace;
    }

    [Test]
    public async Task SingleFlightCoalesced_TagsTheTemplate_NotTheResolvedNamespace()
    {
        // This instrument was the one the cardinality fix missed: it tagged
        // "ActionCache:Account:42", one series per account.
        using var collector = new NamespaceTagCollector("actioncache.single_flight.coalesced");

        var @namespace = ResolvedNamespace();
        var singleFlight = SingleFlightBuilder.Build();

        var result = await singleFlight.GetOrCreateAsync<string>(
            @namespace,
            "Key",
            cacheReader: () => Task.FromResult<string?>("cached"),
            valueFactory: () => Task.FromResult<string?>("fresh"));

        result.WasCoalesced.Should().BeTrue("the reader found an entry, so nothing was executed");
        collector.Namespaces.Should().ContainSingle().Which.Should().Be("Account:{id}");
    }

    [Test]
    public async Task OperationDuration_TagsTheTemplate_NotTheResolvedNamespace()
    {
        using var collector = new NamespaceTagCollector("actioncache.operation.duration");

        var factory = MemoryActionCacheFactoryBuilder.Build();
        var cache = ResilientCacheBuilder.Decorate(factory.Create(ResolvedNamespace())!);

        await cache.GetAsync<string>("Key");

        collector.Namespaces.Should().NotBeEmpty();
        collector.Namespaces.Should().OnlyContain(value => value == "Account:{id}");
    }
}
