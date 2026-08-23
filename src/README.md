
# ActionCache

[![NuGet Version](https://img.shields.io/nuget/v/ActionCache.svg)](https://www.nuget.org/packages/ActionCache/) [![NuGet Downloads](https://img.shields.io/nuget/dt/ActionCache.svg)](https://www.nuget.org/packages/ActionCache/)

- [Quickstart](#quickstart)
    * [Register with MemoryCache](#register-with-imemorycache)
    * [Register with Redis](#register-with-redis)
    * [Register with SqlServer](#register-with-sqlserver)
    * [Register with Azure Cosmos](#register-with-azure-cosmos)
    * [Register Multiple Cache Stores](#register-multiple-cache-stores)
    * [Resilience](#resilience-fail-open-by-default)
    * [Basic Usage](#basic-usage)
    * [Cache Key Creation](#cache-key-creation)
    * [Cache Eviction](#cache-eviction)
    * [Cache Refresh](#cache-refresh)
    * [Route Templates for Namespaces](#route-templates-for-namespaces)

## Register with MemoryCache

Use the `AddActionCache` extension method to register `IMemoryCache` as a cache store. The configuration for `MemoryCacheOptions` is exposed as a parameter to `UseMemoryCache`.

    builder.Services.AddActionCache(options => 
    {
        options.UseMemoryCache(...);
    });

## Register with Redis

Use the `AddActionCache` extension method to register `RedisCache` as a cache store. The configuration for `RedisCacheOptions` is exposed as a parameter to `UseRedisCache`.

    builder.Services.AddActionCache(options => 
    {
        options.UseRedisCache(...);
    });

> **Keyspace notifications:** ActionCache's Redis backend cleans up its sliding-
> expiration index from Redis key-expired events. For that cleanup to run, the Redis
> server must have keyspace event notifications enabled with the `Ex` flags:
>
>     redis-cli config set notify-keyspace-events Ex
>
> (or `notify-keyspace-events Ex` in `redis.conf`). The expiry listener targets the
> database configured in your connection string. Without `Ex` the index self-heals
> lazily on access instead — nothing breaks, cleanup is just deferred.

## Register with SqlServer

Use the `AddActionCache` extension method to register `SqlServerCache` as a cache store. The configuration for `SqlServerCacheOptions` is exposed as a parameter to `UseSqlServerCache`.

    builder.Services.AddActionCache(options => 
    {
        options.UseSqlServerCache(...);
    });

## Register with Azure Cosmos

Use the `AddActionCache` extension method to register `CosmosClient` as a cache store. The configuration for `AzureCosmosCacheOptions` is exposed as a parameter to `UseAzureCosmosCache`.

    builder.Services.AddActionCache(options => 
    {
        options.UseAzureCosmosCache(options =>
        {
            options.DatabaseId = "MyDatabase";
            options.ConnectionString =
                configuration.GetValue<string>("CosmosDb:ConnectionString");
        });
    });

> [!NOTE]
> Both a *DatabaseId* and *ConnectionString* are required. The only requirement within Azure is to create an Azure Cosmos DB account and use that primary connection string in the above configuration. A database and container will be created automatically if they don't already exist.

## Register Multiple Cache Stores

Two or more cache stores can be combined. 

    builder.Services.AddActionCache(options => 
    {
        options.UseMemoryCache(...);
        options.UseRedisCache(...);
        options.UseSqlServerCache(...);
    });

## Resilience (Fail-Open by Default)

By default ActionCache **fails open**: if a cache backend (Redis, SQL Server, Cosmos)
throws while serving a request, the error is logged at `Warning` and the operation
degrades to a cache miss (reads) or a no-op (writes/eviction/refresh) so the request
still succeeds without caching. Caching is an enhancement, not a hard dependency.

To make backend failures propagate to the caller instead, opt in to **fail-closed**:

    builder.Services.AddActionCache(options =>
    {
        options.UseRedisCache(...);
        options.FailClosed();
    });

## Cache Stampede Protection

When a hot entry expires, every concurrent request misses at once and they all execute the
action. ActionCache coalesces them: the first request executes while the rest wait, then
re-read the cache and reuse what it stored. A test issuing 20 concurrent requests to one
uncached endpoint invokes the action **once**.

This is **on by default**. Opt out per endpoint when the action has per-request side
effects that must not be skipped:

    [HttpGet("forecasts")]
    [ActionCache(Namespace = "Forecasts", SingleFlight = false)]
    public IActionResult Get() => Ok(_forecasts);

A waiter blocks for at most `ActionCacheEntryOptions.LockTimeout` (default 10 seconds); if
the lock cannot be acquired in that time the request executes uncoalesced rather than
failing, consistent with the fail-open stance above.

By default coalescing is **per process**, matching .NET's `HybridCache`. With several
instances running, each one executes the action once. To coalesce across every instance,
opt in to the distributed lock:

    builder.Services.AddActionCache(options =>
    {
        options.UseRedisCache(...);
        options.UseDistributedSingleFlight();
    });

This requires Redis or SQL Server (Redis is preferred when both are configured) and throws
at startup if neither is present. Note that every cache miss then costs a lock round-trip
to the backend, which is why it is not the default.

## Basic Usage

Add an `ActionCacheAttribute` to any controller actions that should be cached. There is a mandatory parameter for the cache namespace which will prefix all entries with whatever is specified.

    [HttpPost]
    [Route("/")]
    [ActionCache(Namespace = "MyNamespace")]
    public IActionResult Post() 
    {
    }

## Cache Key Creation

Both the route values and the action arguments are serialized then encoded to generate the cache key suffix. This suffix is appended to the string "ActionCache:{Namespace}".

> [!NOTE]
> Any route data from the request, i.e. the area, controller and action names as well as parameters are also added to the key. This is to support automatic cache refreshing.

> [!NOTE]
> Cache keys are a reversible **encoding** (hex) of the request's route values and
> action arguments — they are **not encrypted** and are not confidential. Anyone
> with read access to the cache store can decode them. Secure the cache store as
> you would any datastore holding request metadata, and avoid placing secrets in
> route values or action arguments.

## Cache Eviction

An `ActionCacheEvictionAttribute` can be applied to a controller action. A cache eviction occurs at the namespace level. One or more namespaces can be used separated by a comma. In the example below, both *MyNamespace* and *MyOtherNamespace* would have their entries evicted on a successful execution of the action.

    [HttpDelete]
    [Route("/")]
    [ActionCacheEviction(Namespace = "MyNamespace, MyOtherNamespace")]
    public IActionResult Delete()
    {
    }

## Cache Refresh

An `ActionCacheRefreshAttribute` can be applied to a controller action. A cache refresh occurs at the namespace level. Any entries currently in the cache will be refetched by executing their corresponding controller action and repopulating the cache. This is done automatically because all of the route details are persisted into the cache key.

    [HttpPut]
    [Route("/")]
    [ActionCacheRefresh(Namespace = "MyNamespace")]
    public IActionResult Put()
    {
    }

## Route Templates for Namespaces

A namespace, i.e. a cache key, can contain route template parameters. In the case below, cache namespaces will vary on the route parameter of *id*. This means that each unique *Account* will have it's own namespace where differing values of *offset* will be stored in their corresponding cache namespace. 

    [HttpGet]
    [Route("{id}")]
    [ActionCache(Namespace = "Account:{id}")]
    public async Task<IActionResult> Get(Guid id, DateTime offset)
    {
    }

> [!NOTE]
> This is beneficial because actions like evicting or refreshing cache entries can be done at the namespace level.