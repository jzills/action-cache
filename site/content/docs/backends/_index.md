---
title: Backends
weight: 2
---

Four backends implement the same `IActionCache` contract. Register one, or several — see
[Layered backends](../operations/layering).

| | Memory | Redis | SQL Server | Azure Cosmos DB |
|---|---|---|---|---|
| **Package** | `ActionCache` | `ActionCache.Redis` | `ActionCache.SqlServer` | `ActionCache.AzureCosmos` |
| **Shared across instances** | No | Yes | Yes | Yes |
| **Locking** | `SemaphoreSlim` | none needed | `sp_getapplock` | none needed |
| **Distributed single flight** | — | Yes | Yes | — |
| **Cancellation** | checked before dispatch | checked before dispatch | forwarded | forwarded |

{{< cards >}}
  {{< card link="memory" title="Memory" subtitle="In-process, no infrastructure." >}}
  {{< card link="redis" title="Redis" subtitle="The default distributed choice." >}}
  {{< card link="sql-server" title="SQL Server" subtitle="For a stack that already runs one." >}}
  {{< card link="cosmos" title="Azure Cosmos DB" subtitle="TTL-backed documents." >}}
{{< /cards >}}

## Why locking differs

Each backend extends `ActionCacheBase<TLock>`, which carries a locking strategy chosen for
what that store can already do atomically.

Redis and Cosmos need none: their operations are atomic as issued — Redis through Lua
scripts, Cosmos through single-document writes. SQL Server uses `sp_getapplock`. The memory
backend needs a `SemaphoreSlim` because the namespace key index is a read-modify-write that
`IMemoryCache` does not make atomic; its locker is a **singleton**, since caches are created
per request and a per-instance locker would guard nothing.
