using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace ActionCache.Redis;

/// <summary>
/// A background service that listens to Redis key expiration events and processes expired keys.
/// </summary>
public class RedisExpiryService : BackgroundService
{
    /// <summary>
    /// Regular expression used to parse keys into their component parts.
    /// </summary>
    protected static readonly Regex KeyExpression = new Regex("^(.*):([^:]+)$");

    /// <summary>
    /// The Redis database instance used for cache operations.
    /// </summary>
    protected readonly IDatabase Cache;

    /// <summary>
    /// The Redis subscriber instance used to subscribe to key expiration notifications.
    /// </summary>
    protected readonly ISubscriber Subscriber;

    /// <summary>
    /// Records subscription failures so a Redis outage does not crash the host.
    /// </summary>
    private readonly ILogger<RedisExpiryService> _logger;

    /// <summary>
    /// The initial delay before retrying a failed keyspace subscription. Each subsequent
    /// failure backs off exponentially up to <see cref="MaxRetryDelay"/>. Defaults to 1 second.
    /// </summary>
    internal TimeSpan InitialRetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// The upper bound the exponential retry delay backs off to. Defaults to 30 seconds.
    /// </summary>
    internal TimeSpan MaxRetryDelay { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisExpiryService"/> class.
    /// </summary>
    /// <param name="connectionMultiplexer">
    /// The Redis connection multiplexer used to access the database and subscriber.
    /// </param>
    /// <param name="logger">The logger used to record subscription failures.</param>
    public RedisExpiryService(
        IConnectionMultiplexer connectionMultiplexer,
        ILogger<RedisExpiryService> logger)
    {
        Cache = connectionMultiplexer.GetDatabase();
        Subscriber = connectionMultiplexer.GetSubscriber();
        _logger = logger;
    }

    /// <summary>
    /// Executes the background service, subscribing to Redis key expiration events and handling them.
    /// </summary>
    /// <param name="stoppingToken">A token used to signal cancellation of the service.</param>
    /// <returns>A task that represents the execution of the service.</returns>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var channel = RedisChannel.Literal($"__keyevent@{Cache.Database}__:expired");
        var retryDelay = InitialRetryDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Subscriber.SubscribeAsync(channel, async (_, message) =>
                {
                    var key = (string?)message;
                    if (!string.IsNullOrWhiteSpace(key))
                    {
                        var match = KeyExpression.Match(key);
                        if (match.Success && match.Groups.Count == 3)
                        {
                            await Cache.SortedSetRemoveAsync(
                                match.Groups[1].Value,
                                match.Groups[2].Value
                            );
                        }
                    }
                });
            }
            catch (Exception exception)
            {
                // A backend outage at startup must not crash the host. StackExchange.Redis
                // re-establishes an existing subscription automatically after a reconnect, so
                // we only need to retry until the initial subscribe succeeds. Failing to reach
                // the backend is a tolerated, fail-open condition, so it is logged at Warning
                // (matching ResilientActionCache) and backs off exponentially so a sustained
                // outage does not flood the logs.
                _logger.LogWarning(
                    exception,
                    "ActionCache could not subscribe to Redis keyspace expiry notifications on database {Database}; " +
                    "retrying in {RetryDelay}. Until then, sliding-expiration index cleanup relies on lazy self-healing.",
                    Cache.Database,
                    retryDelay);

                await DelayQuietly(retryDelay, stoppingToken);
                retryDelay = NextRetryDelay(retryDelay);
                continue;
            }

            // Subscribed successfully; idle until the service is stopped.
            await DelayQuietly(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }
    }

    /// <summary>
    /// Doubles the current retry delay, capped at <see cref="MaxRetryDelay"/>.
    /// </summary>
    /// <param name="current">The delay just waited.</param>
    /// <returns>The next delay to wait, never exceeding <see cref="MaxRetryDelay"/>.</returns>
    internal TimeSpan NextRetryDelay(TimeSpan current)
    {
        var doubled = current + current;
        return doubled < MaxRetryDelay ? doubled : MaxRetryDelay;
    }

    /// <summary>
    /// Awaits a delay, treating cancellation as a normal, non-exceptional stop signal.
    /// </summary>
    /// <param name="delay">The delay to await.</param>
    /// <param name="stoppingToken">A token used to signal cancellation of the service.</param>
    private static async Task DelayQuietly(TimeSpan delay, CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(delay, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown — nothing to do.
        }
    }
}
