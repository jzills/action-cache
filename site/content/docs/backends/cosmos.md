---
title: Azure Cosmos DB
weight: 4
---

```bash
dotnet add package ActionCache.AzureCosmos
```

```csharp
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

## Storage shape

Each entry is a document holding the key, the namespace, the serialized value, and its
expiration as both an absolute timestamp and a Cosmos `ttl`. Expiry is enforced by the
container's TTL rather than by a background sweep.

The serialized value is a JSON string inside that document, so a cached body is escaped
once as JSON and again as the document's field.

## Locking

None needed — operations are atomic.

Cosmos does **not** supply a distributed lock, so it cannot back
[`UseDistributedSingleFlight()`](../../reliability/stampede). Register Redis or SQL Server
alongside it if you need cross-instance coalescing.

## Cancellation

The Cosmos backend forwards the `CancellationToken` to the SDK.
