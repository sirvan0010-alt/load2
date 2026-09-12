# External Repo Audit — hackerxphantom/X_BOMB

Status: **SOURCE-AUDITED**

## Source of truth inspected
- Repository: `hackerxphantom/X_BOMB`
- Source ref inspected: `09a6cfbeba431ef119a71cd522371328dd016f43`
- Main execution source: `bomber.py`
- Provider layer: `utils/provider.py`
- Message layer: `utils/decorators.py`
- Provider data: `apidata.json`

## Verified execution architecture
`bomber.py` creates an `APIProvider`, then uses `ThreadPoolExecutor(max_workers=max_threads)` to submit `api.hit` jobs. The worker reports success/failure and repeats until the requested count is reached. The provider layer maintains a provider list, selects providers, formats target/country-code placeholders, performs HTTP requests and removes providers after a negative result. A shared class-level status flag stops execution when the provider set is exhausted or unavailable.

`APIProvider` also applies a configurable delay before each request and a fixed HTTP timeout. Provider selection is serialized with an instance lock in `hit()`. The provider configuration is externalized in JSON and supports mode/country-code grouping plus a `multi` provider group.

## Verified mechanisms and transfer decisions

| Mechanism | Decision | load2 integration | Rationale |
|---|---|---|---|
| Provider abstraction | ADOPT | Transport/provider registry | Separates scenario orchestration from concrete transport implementations. |
| External provider configuration | ADAPT | Validated provider/profile configuration | Useful for configurable transports, but load2 must validate schema and scope. |
| Provider grouping/capabilities | ADAPT | Capability metadata | Select transports by declared capability rather than hardcoded branching. |
| Provider rotation | ADAPT | Health-aware endpoint/provider selection | Reuse the rotation concept while respecting bounded concurrency and authorization. |
| Remove failed provider | ADAPT / HARDEN | Quarantine/health state | Avoid repeatedly selecting a known failing endpoint; use thread-safe state rather than list mutation. |
| Per-request timeout | ADOPT | CancellationToken + transport timeout | Prevent a stuck provider from blocking the run. |
| ThreadPoolExecutor bounded workers | ADAPT | Existing bounded Channel worker pool | The bounded-worker principle maps directly; do not copy thread-per-request behavior. |
| Delay before request | ADAPT | SmartPaceController / scenario pacing | Centralize pacing so retries and concurrent workers cannot bypass global policy. |
| Structured success/failure result | ADOPT | RunResult / delivery ledger | Make outcomes machine-readable and auditable. |
| Target normalization/validation | ADAPT | Canonical target-set validation | Validate and scope targets before execution. |
| Hardcoded external SMS/OTP endpoints | REJECT | None | Do not import public third-party abuse endpoints into load2. |
| Embedded API tokens/cookies/CSRF material | REJECT | None | Never copy credentials, tokens, cookies or live authorization material. |
| OTP/CAPTCHA/service-abuse flow | REJECT | None | Not transferable as a legitimate load2 transport feature. |
| Infinite/unrestricted public-target bombing behavior | REJECT | None | load2 remains bounded by authorization, scenario limits, cancellation and pacing. |

## Explicit adoption list for load2
1. **Provider/transport registry pattern** — model transport providers behind a stable interface and keep orchestration independent of provider-specific details.
2. **Capability metadata** — providers can declare what operations they support so selection can be policy-driven.
3. **Health/quarantine concept** — temporarily remove failing providers from eligibility instead of retrying them blindly; implement with thread-safe state and expiry.
4. **Bounded worker orchestration** — preserve the principle of a fixed worker budget; load2's Channel workers remain authoritative.
5. **Per-operation timeout** — combine transport timeout with `CancellationToken` so individual operations cannot hang indefinitely.
6. **Structured run outcomes** — feed provider/transport outcomes into load2's existing result, statistics and ledger model.
7. **Target canonicalization and validation** — normalize and validate targets before they enter the execution pipeline.
8. **Centralized pacing** — retain delay as a scenario property, but enforce actual-send pacing through `SmartPaceController` rather than worker-local sleeps.

## Important implementation correction vs source
The source uses mutable class-level provider state and mutates the provider list while worker execution is active. load2 must **not** copy that concurrency pattern. Provider health belongs in thread-safe state (`ConcurrentDictionary`/immutable snapshots as appropriate), and selection must not race with removal/quarantine.

## Security boundary
The provider data contains concrete public SMS/OTP service URLs and embedded request material. Those values are evidence of how the external project works, not implementation inputs for load2. They are deliberately excluded from the adoption list. The transferable mechanism is the provider abstraction and health-aware orchestration, not the abuse endpoints or credentials.

## load2 mapping
`TargetSet → provider/transport selection → bounded Channel worker → timeout/cancellation → transport → structured outcome → ledger/statistics/renderers`

The existing SMTP-specific pipeline remains authoritative for email execution. Any future non-SMTP diagnostic transport must use the same authorization, bounded concurrency, cancellation, pacing and result boundaries rather than importing X_BOMB's direct endpoint execution model.
