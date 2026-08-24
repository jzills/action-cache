---
title: Documentation
next: /docs/getting-started/installation
---

ActionCache adds response caching to ASP.NET Core controller actions and Minimal API
endpoints through three attributes, over four interchangeable backends.

Entries are grouped under a **namespace** you name. That grouping is what makes eviction
and refresh possible without tracking keys: you name the namespace, and every entry under
it is dropped or re-warmed together.

## Start here

{{< cards >}}
  {{< card link="getting-started/installation" title="Installation" subtitle="Pick a backend package and register it." >}}
  {{< card link="getting-started/quickstart" title="Quickstart" subtitle="Cache, evict and refresh an endpoint end to end." >}}
{{< /cards >}}

## Sections

{{< cards >}}
  {{< card link="backends" title="Backends" subtitle="Memory, Redis, SQL Server and Azure Cosmos DB." >}}
  {{< card link="caching" title="Caching" subtitle="The attributes, how keys are built, and what a response varies by." >}}
  {{< card link="operations" title="Operations" subtitle="Eviction, refresh and layered backends." >}}
  {{< card link="reliability" title="Reliability" subtitle="Failure behaviour, timeouts and stampede protection." >}}
  {{< card link="observability" title="Observability" subtitle="Metrics and traces." >}}
  {{< card link="reference" title="Reference" subtitle="Every configuration option in one table." >}}
{{< /cards >}}
