# ActionCache.AzureCosmos

The Azure Cosmos DB backend for [ActionCache](https://www.nuget.org/packages/ActionCache/) —
namespaced response caching for ASP.NET Core.

```bash
dotnet add package ActionCache.AzureCosmos
```

This package references `ActionCache`, so installing it is enough — you do not need the
core package as well.

## Registration

```csharp
using ActionCache.Common.Extensions;

builder.Services.AddActionCache(options =>
{
    options.UseAzureCosmosCache(cosmos =>
    {
        cosmos.DatabaseId = "MyDatabase";
        cosmos.ConnectionString = configuration.GetValue<string>("CosmosDb:ConnectionString");
    });
});
```

Both `DatabaseId` and `ConnectionString` are required.

## Provisioning

The only thing to create in Azure is the Cosmos DB account. The database and container are
created on first use if they do not already exist, so there is no setup script to run.

Initialization is **lazy** — it happens on the first cache operation rather than at startup,
so an application does not fail to start because Cosmos is unreachable.

Each entry is a document holding the key, the namespace, the serialized value, and its
expiration as both an absolute timestamp and a Cosmos `ttl`. Expiry is enforced by the
container's TTL rather than by a background sweep.

## Distributed single-flight

Cosmos supplies no distributed lock, so it cannot back `options.UseDistributedSingleFlight()`.
Register `ActionCache.Redis` or `ActionCache.SqlServer` alongside it if you want distributed
stampede protection; otherwise the in-process default applies.

## Documentation

Full documentation: <https://jzills.github.io/action-cache/docs/backends/cosmos/>
