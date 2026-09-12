# External Repository Audit — juzeon/fast-mail-bomber

Status: SOURCE-AUDITED — source-level inspection completed; no bomber/spam execution path imported into load2.

## Source of truth inspected

Repository: `juzeon/fast-mail-bomber`
Default branch: `master`

Inspected execution/configuration sources:
- `README.md` — repository SHA `1548c26499e1501417a9287cbcd93d15e6753037`
- `index.php` — SHA `bfeda5935cd42553baf8ec7686631b004c4697fd`
- `actions/start-bombing.php` — SHA `44202b9fe174b0f16aa02680169f9e5210666d83`
- `actions/update-providers.php` — SHA `16bc828f8f9506c7a9da7aa0e9d3ed7605c4389e`
- `actions/update-nodes.php` — SHA `f3483ed21901eea0e7d2ab8785eb6899159a1100`
- `config.example.php` — SHA `d9a07c236afe4bcfc4a8da240f47416614b928dc`

## Verified mechanisms

### 1. Provider registry and discovery
`update-providers.php` loads an existing provider set, optionally enriches it from external provider-discovery services, merges results and removes duplicates before persisting them.

Decision: **ADAPT**

Load2 adoption:
- provider/transport registry remains the abstraction boundary;
- canonicalization + deduplication are required before registration;
- external discovery remains an optional enrichment source, never an implicit execution source.

Target areas: provider/transport registry, enrichment pipeline.

### 2. Dead-provider quarantine
`update-nodes.php` maintains a dead-provider list. Invalid or inaccessible providers, empty providers according to policy, and failed checks are excluded from subsequent processing.

Decision: **ADOPT / HARDEN**

Load2 adoption:
- preserve the existing `ProxyRotator` blocked/quarantine pattern;
- extend the same state-machine concept to provider health where appropriate;
- make state thread-safe and cancellation-aware rather than using unsynchronized mutable arrays.

Target areas: provider health, transport selection, circuit-breaker/quarantine logic.

### 3. Bounded concurrency
`start-bombing.php` uses Guzzle `Pool` with an explicit `CONCURRENCY`; `update-nodes.php` likewise uses a pool with `THREAD_POOL_SIZE`.

Decision: **ADOPT / ADAPT**

Load2 adoption:
- bounded Channel + fixed worker pool remains authoritative;
- concurrency is a hard bound, not thread-per-request fan-out;
- worker count must remain coordinated with adaptive concurrency and SMTP pool admission.

Target area: `SmtpTestRunner` execution pipeline.

### 4. Structured request outcome classification
`start-bombing.php` distinguishes fulfilled responses into success/failure categories and separately handles rejected requests.

Decision: **ADAPT / HARDEN**

Load2 adoption:
- every operation should produce a structured result/outcome;
- transport failure, timeout, policy rejection, protocol failure and successful delivery remain distinct;
- counters are maintained through the thread-safe run ledger/statistics path.

Target areas: `RunResult`, delivery ledger, progress/statistics.

### 5. Input validation
The bomber entry point validates the target as an email address before execution and verifies required local node data exists.

Decision: **ADOPT / HARDEN**

Load2 adoption:
- canonicalize and validate targets before they enter the execution queue;
- keep target-set validation separate from network transport;
- preserve explicit authorization/scope checks and the required `--unauthorized` gate.

Target areas: target normalization, CLI safety gate, scenario setup.

### 6. Configurable transport parameters
The reference project separates concurrency, timeout, provider/node counts and optional proxy configuration into configuration values.

Decision: **ADAPT**

Load2 adoption:
- operational values remain configuration-driven;
- secrets must come from environment/secret providers, never source-controlled constants;
- timeout and pacing remain cancellation-aware and centrally enforced.

Target areas: options/profile/configuration layer.

### 7. Provider/node refinement
The reference implementation discovers provider pages, extracts candidate nodes, validates candidates, and persists a refined subset.

Decision: **EXTRACT / ADAPT**

Load2 adoption:
- separate discovery, validation, normalization and execution stages;
- allow an enrichment pipeline to feed validated transport/provider metadata into later diagnostics;
- do not import the Mailman bombing node model itself.

Target area: enrichment/provider pipeline.

### 8. Cancellation / operator stop
The CLI documents CTRL+C as the stop mechanism. The source does not provide a .NET-style cancellation abstraction.

Decision: **HARDEN**

Load2 adoption:
- use `CancellationToken` end-to-end;
- cancellation must release pacing gates, semaphores, pool leases and worker resources;
- no infinite/unbounded execution mode is imported.

## Explicitly NOT adopted

- target-mailbox bombing/spamming behavior;
- automatic use of public Mailman subscription nodes to generate unsolicited mail;
- built-in public provider/node databases;
- Shodan/ZoomEye discovery as an execution feed;
- proxy configuration intended to conceal abusive traffic;
- unlimited/infinite bombing mode;
- direct import of Mailman subscription endpoints;
- connection fan-out as an unrestricted public-target stress mechanism.

## Net adoption into load2

1. **Provider/transport registry** — ADAPT.
2. **Provider health + dead/quarantine state** — ADOPT/HARDEN.
3. **Bounded worker concurrency** — ADOPT/ADAPT.
4. **Structured success/failure outcomes** — ADAPT/HARDEN.
5. **Early target validation and canonicalization** — ADOPT/HARDEN.
6. **Configuration-driven timeout/concurrency/counts** — ADAPT.
7. **Discovery → validation → normalization → execution pipeline separation** — EXTRACT/ADAPT.
8. **Cancellation as a first-class execution concern** — HARDEN using existing `CancellationToken` architecture.

## Engineering conclusion

This repository contributes useful orchestration and provider-health mechanisms, but its central execution purpose is mailbox bombing. Therefore only the reusable engineering mechanisms listed above are transferred into load2. No Mailman bombing path, public node inventory, or unrestricted target execution is added.
