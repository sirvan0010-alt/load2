# load2 — SMTP-first Implementation Backlog

**Authority:** source + tests on `main`.

## Verified done

| Item | Status |
|------|--------|
| Health / Report / Multi-account / Churn / Provider / Diagnostics | ✅ |
| TargetSet / ScenarioLimits / DurationSeconds | ✅ |
| GUI TransportDiagnostics on Test connection | ✅ |
| GUI Accounts editor | ✅ tab Účty SMTP + SmtpAccountMapping → `MailTestOptions.Accounts` |
| A1 destination-provider throttling | ✅ commit `175b379239a7c4c07c5d5953f628def1db30f621`; CI #208 + CodeQL SUCCESS |
| A2 multi-account throttling regression | ✅ commit `b3e075fc099eb4fc124eadd42297158471265606`; CI #210 + CodeQL #95 SUCCESS |

## Current execution plan

```text
A1 destination-provider throttling                 ✅ VERIFIED
A2 multi-account throttling regression + metrics   ✅ FIXED
A3 bounded scenario queue + queue metrics          ← CURRENT
A4 retry / requeue with hard limits + retry metrics
A5 unified SMTP response classification + counters
A6 global concurrency audit                         CONDITIONAL
A7 final observability/report/GUI reconciliation
A8 verification / documentation / release baseline
```

Do not mark an item FIXED/PASS until the implementation is on `main`, its tests pass, and the relevant CI is green.

---

## A2 — Multi-account regression + metrics

**Status: FIXED** — `b3e075fc099eb4fc124eadd42297158471265606`; CI #210 and CodeQL #95 green.

The regression matrix proves that multiple SMTP accounts cannot bypass the existing recipient/destination-provider pacing contract. The implementation reuses the shared `SmartPaceController`; no second limiter or parallel pacing stack was introduced.

---

## A3 — Bounded scenario queue

**Goal:** make scenario execution explicitly queue-based and bounded while reusing the existing worker/channel/pacing architecture.

### Inspect first

- existing bounded `Channel` / worker pipeline
- `TargetSet`
- `ScenarioLimits`
- `SmtpScenario`
- `DeliveryLedger`
- `SmartPaceController`
- existing cancellation and AutoRestart paths

### Implement

1. Explicit bounded scenario queue capacity.
2. Producer backpressure when the queue is full.
3. No unbounded in-memory accumulation.
4. Cancellation drains/stops production safely.
5. Queue completion is coordinated with workers.
6. Ledger remains the authoritative send-count boundary.
7. Count and duration limits remain hard limits.
8. Multi-account execution uses the same bounded scenario contract.
9. DryRun remains network-free.
10. Preserve persistent SMTP sessions; queueing must not force connection-per-message behavior.

### Queue metrics

Add only the metrics that naturally belong to the queue implementation:

- current queue depth
- peak queue depth
- enqueued
- dequeued/started
- completed
- rejected/full, if a bounded enqueue can be refused
- cancelled/drained

These must flow through existing progress/reporting rather than creating a parallel telemetry stack.

### A3 source-audit finding

The current `SmtpTestRunner` already uses a bounded `Channel<int>` per batch with `BoundedChannelFullMode.Wait`, `SingleWriter = true`, `SingleReader = false`, and `MaxConcurrency * 2` capacity. Workers consume it through `ReadAllAsync(CancellationToken)`, and producer completion is coordinated with worker completion. This is the existing queue foundation; A3 must instrument and test this pipeline rather than introduce a second queue or replace it blindly.

The existing runner also keeps `SmartPaceController` as the actual SEND pacing gate and uses the shared `DeliveryLedger`; A3 must not add another limiter or alter those semantics.

### Acceptance

- deterministic unit tests for full queue/backpressure
- cancellation while producer is blocked
- worker completion/drain
- count limit
- duration limit
- multi-account compatibility
- DryRun
- CI green

---

## A4 — Retry / requeue with hard limits

**Goal:** make retries explicit, bounded and observable, using the A3 queue and existing SmartPace behavior.

### Implement

1. Requeue only failures classified as retryable.
2. Never retry permanent/policy/auth/invalid-recipient failures blindly.
3. Per-message maximum retry count.
4. Global retry budget where needed to prevent retry storms.
5. Backoff remains coordinated with `SmartPaceController`.
6. Preserve cancellation/deadline semantics during retry delay.
7. Do not hold SMTP connection leases unnecessarily while waiting to retry.
8. Multi-account retry remains compatible with account pools/health.
9. Ledger semantics must distinguish original attempt from final outcome.

### Retry metrics

Move beyond the aggregate `Retries` counter where useful:

- retry attempts total
- retryable failures
- exhausted retries
- retry distribution by attempt number
- requeued items
- final success after retry
- final failure after retry

Do not duplicate failure classification; A5 becomes the common classification source.

### Acceptance

- retryable 4xx test
- permanent 5xx test
- timeout test
- retry exhaustion test
- cancellation during retry wait
- no duplicate/leaked ledger acceptance
- CI green

---

## A5 — Unified SMTP response classification

**Goal:** one classification contract used by SMTP results, retry policy, endpoint health, pacing decisions, reports and GUI.

### Proposed classification dimensions

At minimum distinguish:

- Success
- Transient
- Throttled
- Timeout
- Authentication
- Policy/Rejected
- Recipient
- Permanent
- Cancelled
- Unknown

The exact enum/labels must be derived from the current source and MailKit exception/SMTP response model; do not invent provider-specific semantics that are not supported by evidence.

### Implement

1. Central classifier for SMTP status/exception outcomes.
2. Map 4xx/5xx and transport exceptions consistently.
3. Preserve raw SMTP status/response text where already safe and available.
4. Ensure passwords/secrets never enter classification/report output.
5. Retry policy consumes classification rather than duplicating status-code logic.
6. Endpoint health consumes the same classification.
7. RunReport exports aggregate classification counts.
8. GUI/progress can expose the same categories.

### Acceptance

- representative SMTP 2xx/4xx/5xx tests
- timeout/connect/auth exception tests
- retry decision tests
- health decision tests
- report serialization tests
- CI green

---

## A6 — Global concurrency audit (conditional)

**Do not implement pre-emptively.**

Only proceed if source/tests demonstrate that multi-account/multi-pool execution can exceed the intended global concurrency boundary.

### Audit

- account pool concurrency
- adaptive concurrency
- worker count
- scenario queue pressure
- SMTP connection pool limits
- any global `SemaphoreSlim` contract

### If overshoot is proven

Add the smallest possible correction while preserving:

- thread safety
- cancellation
- persistent sessions
- adaptive concurrency
- account isolation
- existing pacing

If no overshoot is proven, record the audit as PASS and do not add another limiter.

---

## A7 — Final observability / report / GUI reconciliation

After A2–A6, verify that the existing `MailTestResult`, `RunReport`, `ProgressUpdate`, GUI and dashboard expose the final useful metrics without parallel telemetry systems.

### Minimum run metrics

1. Requested / attempted / sent / failed / cancelled.
2. Success rate.
3. Throughput (overall and active where already supported).
4. P50/P95/P99 latency.
5. SMTP 4xx / 5xx / timeout counters.
6. Phase waits: prep, adaptive, pool, pace, SMTP send.
7. Adaptive concurrency.
8. Circuit-breaker state/events.
9. Pool connection count.
10. Queue depth/peak after A3.
11. Retry metrics after A4.
12. Unified classification counts after A5.
13. RunId and start/finish timestamps.

### Important boundary

Deliverability/reputation is not a substitute for SMTP run results and should remain primarily an operational/pre-flight layer:

- SPF/DKIM/DMARC/PTR
- RBL/blocklist checks
- provider-side reputation signals
- seed placement

Do not add complaint scraping, reputation masking, feedback-loop bypass, or unrestricted public-mailbox flooding.

---

## A8 — Verification / documentation / release baseline

Before declaring the next baseline complete:

1. Full test suite green.
2. CI green on the final `main` commit.
3. CodeQL green when present.
4. No secrets in source/report artifacts.
5. `--unauthorized`, DryRun and TestMode behavior verified.
6. Cancellation and hard limits verified.
7. Persistent SMTP session behavior verified.
8. Documentation matches actual source.
9. MASTER CARD #4 updated with exact commit/CI evidence.
10. No historical/secondary repository is treated as authority.

---

## Parallel track — external repository audit

External audits continue independently from the engine backlog.

Workflow:

```text
source audit
  → mechanism inventory
  → ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT
  → implement only useful mechanisms in controlled/authorized load2 paths
```

Do not mix unrelated external-repo audit changes into an engine PR.

## GUI Accounts notes

- Empty list → classic single-host options path.
- 2+ enabled accounts → `SmtpAccountPoolHub`.
- Passwords never in `RunReport` JSON.
