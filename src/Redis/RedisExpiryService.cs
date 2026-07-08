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
    /// The delay between failed keyspace-subscription attempts. Defaults to 30 seconds.
    /// </summary>
    internal TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(30);

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
                // we only need to retry until the initial subscribe succeeds.
                _logger.LogError(
                    exception,
                    "ActionCache could not subscribe to Redis keyspace expiry notifications on database {Database}; " +
                    "retrying in {RetryDelay}. Until then, sliding-expiration index cleanup relies on lazy self-healing.",
                    Cache.Database,
                    RetryDelay);

                await DelayQuietly(RetryDelay, stoppingToken);
                continue;
            }

            // Subscribed successfully; idle until the service is stopped.
            await DelayQuietly(Timeout.InfiniteTimeSpan, stoppingToken);
            return;
        }
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
