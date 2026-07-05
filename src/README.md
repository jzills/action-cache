
# ActionCache

[![NuGet Version](https://img.shields.io/nuget/v/ActionCache.svg)](https://www.nuget.org/packages/ActionCache/) [![NuGet Downloads](https://img.shields.io/nuget/dt/ActionCache.svg)](https://www.nuget.org/packages/ActionCache/)

- [Quickstart](#quickstart)
    * [Register with MemoryCache](#register-with-imemorycache)
    * [Register with Redis](#register-with-redis)
    * [Register with SqlServer](#register-with-sqlserver)
    * [Register with Azure Cosmos](#register-with-azure-cosmos)
    * [Register Multiple Cache Stores](#register-multiple-cache-stores)
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