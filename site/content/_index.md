---
title: ActionCache
layout: hextra-home
---

{{< hextra/hero-badge >}}
  <div class="hx-w-2 hx-h-2 hx-rounded-full hx-bg-primary-400"></div>
  <span>Memory · Redis · SQL Server · Azure Cosmos DB</span>
{{< /hextra/hero-badge >}}

<div class="hx-mt-6 hx-mb-6">
{{< hextra/hero-headline >}}
  Namespaced response caching&nbsp;<br class="sm:hx-block hx-hidden" />for ASP.NET Core
{{< /hextra/hero-headline >}}
</div>

<div class="hx-mb-12">
{{< hextra/hero-subtitle >}}
  Cache a controller action or a Minimal API endpoint with one attribute.&nbsp;<br class="sm:hx-block hx-hidden" />Evict and refresh whole namespaces without knowing a single key.
{{< /hextra/hero-subtitle >}}
</div>

<div class="hx-mb-6">
{{< hextra/hero-button text="Get Started" link="docs/getting-started/installation" >}}
</div>

## Install

{{< cards >}}
  {{< card link="docs/backends/memory" title="Memory" icon="chip" subtitle="dotnet add package ActionCache — in-process, no infrastructure." >}}
  {{< card link="docs/backends/redis" title="Redis" icon="lightning-bolt" subtitle="dotnet add package ActionCache.Redis — atomic Lua operations." >}}
  {{< card link="docs/backends/sql-server" title="SQL Server" icon="database" subtitle="dotnet add package ActionCache.SqlServer — sp_getapplock." >}}
  {{< card link="docs/backends/cosmos" title="Azure Cosmos DB" icon="cloud" subtitle="dotnet add package ActionCache.AzureCosmos — TTL-backed." >}}
{{< /cards >}}

## Three attributes

{{< cards >}}
  {{< card link="docs/caching/attributes" title="[ActionCache]" icon="save" subtitle="Cache the response. Varies by the authenticated user automatically." >}}
  {{< card link="docs/operations/eviction" title="[ActionCacheEviction]" icon="trash" subtitle="Drop every entry in a namespace after a successful write." >}}
  {{< card link="docs/operations/refresh" title="[ActionCacheRefresh]" icon="refresh" subtitle="Replay the recorded requests so the cache is warm, not empty." >}}
{{< /cards >}}

## Features

{{< hextra/feature-grid >}}
  {{< hextra/feature-card
    title="Namespace eviction"
    subtitle="Entries are grouped under a namespace you name, and a namespace can embed route tokens — `Account:{id}` gives every account its own. Evict or refresh the group without enumerating keys."
    link="docs/caching/attributes"
  >}}
  {{< hextra/feature-card
    title="Per-user by default"
    subtitle="On an authenticated endpoint the caller's identity joins the key without being asked for. Two users on one `[Authorize]` action cannot be served each other's response."
    link="docs/caching/vary-by"
  >}}
  {{< hextra/feature-card
    title="Refresh by replay"
    subtitle="Refresh re-issues the request recorded on each entry against the real endpoint, in its own DI scope — model binding, filters and result execution all run normally."
    link="docs/operations/refresh"
  >}}
  {{< hextra/feature-card
    title="Fails open"
    subtitle="A backend that throws degrades to a miss and the request still succeeds. Opt in to fail-closed, and set an operation timeout to bound a backend that hangs rather than throws."
    link="docs/reliability/resilience"
  >}}
  {{< hextra/feature-card
    title="Stampede protection"
    subtitle="Concurrent misses for one key are coalesced so the action runs once. In-process by default; opt in to a distributed lock over Redis or SQL Server."
    link="docs/reliability/stampede"
  >}}
  {{< hextra/feature-card
    title="Layered backends"
    subtitle="Register several and they chain. A hit in a deeper layer is promoted into the faster one, so Memory + Redis stops paying the round-trip after the first request."
    link="docs/operations/layering"
  >}}
  {{< hextra/feature-card
    title="Hashed keys"
    subtitle="Keys are SHA-256 over route values, arguments and vary-by values. Nothing needs to reverse one, so nothing in a key is readable by whoever can read the store."
    link="docs/caching/cache-keys"
  >}}
  {{< hextra/feature-card
    title="OpenTelemetry"
    subtitle="A Meter and an ActivitySource, both named ActionCache, inert until something subscribes. Hit ratio, backend latency by outcome, evictions and coalesced requests."
    link="docs/observability"
  >}}
  {{< hextra/feature-card
    title="MVC and Minimal APIs"
    subtitle="The same three attributes drive controller actions and endpoint filters alike. Which filter runs is chosen at runtime from how the app is built."
    link="docs/getting-started/quickstart"
  >}}
{{< /hextra/feature-grid >}}

## Choosing a backend

| | Memory | Redis | SQL Server | Azure Cosmos DB |
|---|---|---|---|---|
| **Package** | `ActionCache` | `ActionCache.Redis` | `ActionCache.SqlServer` | `ActionCache.AzureCosmos` |
| **Shared across instances** | No | Yes | Yes | Yes |
| **Locking** | `SemaphoreSlim` | none needed — atomic Lua | `sp_getapplock` | none needed — atomic |
| **Distributed single flight** | — | Yes | Yes | — |
| **Expiry** | `IMemoryCache` | key TTL + keyspace events | `SqlServerCache` | container TTL |
| **Use it for** | one instance, or as the first layer of a chain | the default distributed choice | a stack that already runs SQL Server | a stack already on Cosmos |

Backends compose — see [Layered backends](docs/operations/layering).
