---
title: ActionCache
layout: home
toc: false
---

<div class="ac-hero">
  <h1 class="ac-hero__title">Namespaced response caching<br />for ASP.NET Core</h1>
  <p class="ac-hero__blurb">
    Cache a controller action or a Minimal API endpoint with one attribute.
    Evict and refresh whole namespaces without knowing a single key.
  </p>
  <div class="ac-hero__actions">
    <a class="ac-button ac-button--primary" href="docs/getting-started/installation/">Get started</a>
  </div>
</div>

<div class="ac-showcase">
<div class="ac-showcase__frame">

{{< highlight csharp "" >}}
[HttpGet("forecasts")]
[ActionCache(Namespace = "Forecasts")]
public IActionResult Get() => Ok(_repository.All());

[HttpPost("forecasts")]
[ActionCacheEviction(Namespace = "Forecasts")]
public IActionResult Create(Forecast forecast) => Ok(_repository.Add(forecast));
{{< /highlight >}}

</div>
<p class="ac-showcase__caption">
  Cache a read. Drop the whole namespace on write. No keys to track, and nothing
  else to wire up.
</p>
</div>

<div class="ac-section" id="install">
  <h2 class="ac-section__title">Install</h2>
  <p class="ac-section__lede">
    One package per backend, so you only take the dependencies you use. Every
    backend package brings the core with it.
  </p>
  <div class="ac-grid">
    <a class="ac-card" href="docs/backends/memory/">
      <span class="ac-card__title">Memory</span>
      <span class="ac-card__command">dotnet add package ActionCache</span>
      <span class="ac-card__body">In-process. No infrastructure, and the natural first layer of a chain.</span>
    </a>
    <a class="ac-card" href="docs/backends/redis/">
      <span class="ac-card__title">Redis</span>
      <span class="ac-card__command">dotnet add package ActionCache.Redis</span>
      <span class="ac-card__body">The default distributed choice. Atomic through Lua, and the preferred distributed lock.</span>
    </a>
    <a class="ac-card" href="docs/backends/sql-server/">
      <span class="ac-card__title">SQL Server</span>
      <span class="ac-card__command">dotnet add package ActionCache.SqlServer</span>
      <span class="ac-card__body">For a stack that already runs one. Locks with <code>sp_getapplock</code>.</span>
    </a>
    <a class="ac-card" href="docs/backends/cosmos/">
      <span class="ac-card__title">Azure Cosmos DB</span>
      <span class="ac-card__command">dotnet add package ActionCache.AzureCosmos</span>
      <span class="ac-card__body">TTL-backed documents, with the database and container created on first use.</span>
    </a>
  </div>
</div>

<div class="ac-section" id="attributes">
  <h2 class="ac-section__title">Three attributes</h2>
  <p class="ac-section__lede">
    Everything the library does is reached through one of these. Each takes a
    namespace, which is the group an entry belongs to.
  </p>
  <div class="ac-grid">
    <a class="ac-card" href="docs/caching/attributes/">
      <span class="ac-card__title"><code>[ActionCache]</code></span>
      <span class="ac-card__body">Cache the response. Varies by the authenticated user automatically, so one caller is never served another's data.</span>
    </a>
    <a class="ac-card" href="docs/operations/eviction/">
      <span class="ac-card__title"><code>[ActionCacheEviction]</code></span>
      <span class="ac-card__body">Drop every entry in a namespace after a successful write. You never name a key.</span>
    </a>
    <a class="ac-card" href="docs/operations/refresh/">
      <span class="ac-card__title"><code>[ActionCacheRefresh]</code></span>
      <span class="ac-card__body">Replay the request recorded on each entry, so the cache is left warm rather than empty.</span>
    </a>
  </div>
</div>

<div class="ac-section" id="features">
  <h2 class="ac-section__title">What you get</h2>
  <p class="ac-section__lede">
    Each of these has a page in <a href="docs/">the documentation</a>.
  </p>
  <div class="ac-grid ac-grid--wide">
    <a class="ac-card" href="docs/caching/attributes/">
      <span class="ac-card__title">Namespace eviction</span>
      <span class="ac-card__body">Entries are grouped under a namespace you name, and a namespace can embed route tokens — <code>Account:{id}</code> gives every account its own group to evict or refresh.</span>
    </a>
    <a class="ac-card" href="docs/caching/vary-by/">
      <span class="ac-card__title">Per-user by default</span>
      <span class="ac-card__body">On an authenticated endpoint the caller's identity joins the key without being asked for. Two users on one <code>[Authorize]</code> action cannot collide.</span>
    </a>
    <a class="ac-card" href="docs/operations/refresh/">
      <span class="ac-card__title">Refresh by replay</span>
      <span class="ac-card__body">Refresh re-issues the recorded request against the real endpoint, in its own DI scope — model binding, filters and result execution all run normally.</span>
    </a>
    <a class="ac-card" href="docs/reliability/resilience/">
      <span class="ac-card__title">Fails open</span>
      <span class="ac-card__body">A backend that throws degrades to a miss and the request still succeeds. Opt in to fail-closed, and set a timeout to bound one that hangs instead.</span>
    </a>
    <a class="ac-card" href="docs/reliability/stampede/">
      <span class="ac-card__title">Stampede protection</span>
      <span class="ac-card__body">Concurrent misses for one key are coalesced so the action runs once. In-process by default; opt in to a lock over Redis or SQL Server.</span>
    </a>
    <a class="ac-card" href="docs/operations/layering/">
      <span class="ac-card__title">Layered backends</span>
      <span class="ac-card__body">Register several and they chain. A hit in a deeper layer is promoted into the faster one, so Memory + Redis pays the round-trip once.</span>
    </a>
    <a class="ac-card" href="docs/caching/cache-keys/">
      <span class="ac-card__title">Hashed keys</span>
      <span class="ac-card__body">SHA-256 over route values, arguments and vary-by values. Nothing needs to reverse a key, so nothing readable is left in the store.</span>
    </a>
    <a class="ac-card" href="docs/observability/">
      <span class="ac-card__title">OpenTelemetry</span>
      <span class="ac-card__body">A meter and an activity source, inert until something subscribes. Hit ratio, backend latency by outcome, evictions and coalesced requests.</span>
    </a>
    <a class="ac-card" href="docs/getting-started/quickstart/">
      <span class="ac-card__title">MVC and Minimal APIs</span>
      <span class="ac-card__body">The same model covers controller actions and endpoints alike, with the right filter chosen at runtime from how the app is built.</span>
    </a>
  </div>
</div>

<div class="ac-section" id="backends">
  <h2 class="ac-section__title">Choosing a backend</h2>
  <p class="ac-section__lede">
    They are interchangeable, and they compose — see
    <a href="docs/operations/layering/">layered backends</a>.
  </p>
  <div class="ac-table-wrap">

| | Memory | Redis | SQL Server | Cosmos DB |
|---|---|---|---|---|
| **Package** | `ActionCache` | `.Redis` | `.SqlServer` | `.AzureCosmos` |
| **Shared across instances** | No | Yes | Yes | Yes |
| **Locking** | `SemaphoreSlim` | atomic Lua | `sp_getapplock` | atomic |
| **Distributed single flight** | — | Yes | Yes | — |
| **Expiry** | `IMemoryCache` | TTL + keyspace events | `SqlServerCache` | container TTL |

  </div>
</div>

<p class="ac-closing">
  Targets .NET 8 and .NET 10 · <a href="docs/">Read the documentation</a>
</p>
