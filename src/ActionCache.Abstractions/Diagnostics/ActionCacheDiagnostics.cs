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
            description: "Duration of a single cache-backend operation, tagged by outcome.");

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
    /// The values of the <c>outcome</c> tag on <see cref="OperationDuration"/>.
    /// </summary>
    /// <remarks>
    /// A backend that hangs until the operation timeout elapses is the case the histogram
    /// exists for, so failures and abandonments are sampled alongside successes rather than
    /// dropped. Without the tag a timeout would be indistinguishable from a slow success.
    /// </remarks>
    internal static class Outcomes
    {
        internal const string Ok = "ok";
        internal const string Error = "error";
        internal const string Cancelled = "cancelled";
    }

    /// <summary>
    /// Records a cache lookup outcome.
    /// </summary>
    /// <param name="namespace">The cache namespace template.</param>
    /// <param name="status">The outcome, such as hit or miss.</param>
    /// <remarks>
    /// Recorded by the filters, once per request, rather than by the backend decorator.
    /// A single logical lookup reads the backend more than once — single flight re-checks
    /// under the lock, and a layered chain reads every layer — so counting it per backend
    /// call made the published hit ratio a count of reads, not of requests.
    /// </remarks>
    internal static void RecordRequest(string @namespace, string status) =>
        Requests.Add(1, new KeyValuePair<string, object?>("namespace", @namespace),
                        new KeyValuePair<string, object?>("status", status));

    /// <summary>
    /// Records how long a backend operation took.
    /// </summary>
    /// <param name="namespace">The cache namespace template.</param>
    /// <param name="operation">The operation name.</param>
    /// <param name="outcome">One of <see cref="Outcomes"/>.</param>
    /// <param name="elapsed">How long it took.</param>
    internal static void RecordDuration(string @namespace, string operation, string outcome, TimeSpan elapsed) =>
        OperationDuration.Record(elapsed.TotalMilliseconds,
            new KeyValuePair<string, object?>("namespace", @namespace),
            new KeyValuePair<string, object?>("operation", operation),
            new KeyValuePair<string, object?>("outcome", outcome));

    /// <summary>
    /// Starts a span for a cache operation, or returns <see langword="null"/> when nothing
    /// is listening.
    /// </summary>
    /// <param name="operation">The operation name.</param>
    /// <param name="namespace">
    /// The cache namespace template, or <see langword="null"/> for an operation that does
    /// not belong to one.
    /// </param>
    /// <returns>The started activity, or <see langword="null"/>.</returns>
    /// <remarks>
    /// The namespace tag carries the unresolved template — <c>Account:{id}</c>, not
    /// <c>Account:42</c>. The resolved form is per-resource, and as a metric dimension or a
    /// span attribute that is unbounded cardinality. Callers with nothing to put here pass
    /// <see langword="null"/> rather than substituting a request path, which is unbounded
    /// in the same way and is not a namespace.
    /// </remarks>
    internal static Activity? StartOperation(string operation, string? @namespace = null)
    {
        var activity = ActivitySource.StartActivity($"ActionCache {operation}", ActivityKind.Internal);
        if (activity is null)
        {
            return null;
        }

        if (@namespace is not null)
        {
            activity.SetTag("actioncache.namespace", @namespace);
        }

        activity.SetTag("actioncache.operation", operation);
        return activity;
    }
}
