using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace ActionCache.Common.Diagnostics;

/// <summary>
/// The <see cref="Meter"/> and <see cref="ActivitySource"/> ActionCache publishes.
/// </summary>
/// <remarks>
/// Both are inert until something subscribes, so there is nothing to enable and no opt-out
/// to configure. Point OpenTelemetry at the <c>ActionCache</c> meter and activity source:
/// <code>
/// builder.Services.AddOpenTelemetry()
///     .WithMetrics(metrics => metrics.AddMeter(ActionCacheDiagnostics.MeterName))
///     .WithTracing(tracing => tracing.AddSource(ActionCacheDiagnostics.ActivitySourceName));
/// </code>
/// </remarks>
public static class ActionCacheDiagnostics
{
    /// <summary>
    /// The name of the meter ActionCache publishes instruments on.
    /// </summary>
    public const string MeterName = "ActionCache";

    /// <summary>
    /// The name of the activity source ActionCache publishes spans on.
    /// </summary>
    public const string ActivitySourceName = "ActionCache";

    internal static readonly Meter Meter = new(MeterName);

    /// <summary>
    /// The activity source used for cache and refresh spans.
    /// </summary>
    internal static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    /// <summary>
    /// Counts cache lookups by outcome.
    /// </summary>
    internal static readonly Counter<long> Requests =
        Meter.CreateCounter<long>("actioncache.requests", unit: "{request}",
            description: "Cache lookups, tagged by namespace and outcome.");

    /// <summary>
    /// Times individual backend operations.
    /// </summary>
    internal static readonly Histogram<double> OperationDuration =
        Meter.CreateHistogram<double>("actioncache.operation.duration", unit: "ms",
            description: "Duration of a single cache-backend operation.");

    /// <summary>
    /// Counts namespace evictions.
    /// </summary>
    internal static readonly Counter<long> Evictions =
        Meter.CreateCounter<long>("actioncache.evictions", unit: "{eviction}",
            description: "Namespace evictions.");

    /// <summary>
    /// Counts requests that were coalesced onto another request's in-flight execution.
    /// </summary>
    internal static readonly Counter<long> SingleFlightCoalesced =
        Meter.CreateCounter<long>("actioncache.single_flight.coalesced", unit: "{request}",
            description: "Requests served by another request's in-flight execution.");

    /// <summary>
    /// Records a cache lookup outcome.
    /// </summary>
    /// <param name="namespace">The cache namespace.</param>
    /// <param name="status">The outcome, such as hit or miss.</param>
    internal static void RecordRequest(string @namespace, string status) =>
        Requests.Add(1, new KeyValuePair<string, object?>("namespace", @namespace),
                        new KeyValuePair<string, object?>("status", status));

    /// <summary>
    /// Records how long a backend operation took.
    /// </summary>
    /// <param name="namespace">The cache namespace.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="elapsed">How long it took.</param>
    internal static void RecordDuration(string @namespace, string operation, TimeSpan elapsed) =>
        OperationDuration.Record(elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("namespace", @namespace),
            new KeyValuePair<string, object?>("operation", operation));

    /// <summary>
    /// Starts a span for a cache operation, or returns <see langword="null"/> when nothing
    /// is listening.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="namespace">The cache namespace.</param>
    /// <returns>The started activity, or <see langword="null"/>.</returns>
    internal static Activity? StartOperation(string operation, string @namespace)
    {
        var activity = ActivitySource.StartActivity($"ActionCache {operation}", ActivityKind.Internal);
        activity?.SetTag("actioncache.namespace", @namespace);
        activity?.SetTag("actioncache.operation", operation);
        return activity;
    }
}
