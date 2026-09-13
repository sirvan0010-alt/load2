# A6 — Global concurrency audit

**Status: PASS (no architecture change)**  
**Date:** 2026-09-14  
**Scope:** Prove or disprove concurrent overshoot of `MaxConcurrency` under single- and multi-account paths.

## Question

Can message workers / SMTP SEND / pool rents exceed `MailTestOptions.MaxConcurrency` globally?

## Evidence (main source)

### 1. Message workers (`SmtpTestRunner`)

```text
workerCount = max(1, MaxConcurrency)
Channel capacity = workerCount × 2, FullMode = Wait
Workers = Enumerable.Range(1, MaxConcurrency).Select(WorkerAsync)
```

- One `ProcessMessageAsync` per worker at a time.
- Batches run sequentially (`for` + `await Task.WhenAll(workers)`).
- AutoRestart attempts are sequential.

**Bound:** concurrent message processing ≤ `MaxConcurrency`.

### 2. Adaptive concurrency (`AdaptiveConcurrencyLimiter`)

```text
new AdaptiveConcurrencyLimiter(MaxConcurrency, min: 1, max: MaxConcurrency)
```

- `_current` never exceeds `_max` (= `MaxConcurrency`).
- Reset does not zero `_active` (commented invariant — prevents under-count / over-admit).

**Bound:** when enabled, admissions ≤ `MaxConcurrency`.

### 3. SMTP connection pool (`SmtpConnectionPool`)

```text
_gate = new SemaphoreSlim(MaxConcurrency, MaxConcurrency)
PreWarmAsync(count) → count = Min(count, MaxConcurrency)
```

- Each `RentAsync` takes one permit; `Return`/`Discard` release.
- **Per-pool** concurrent rents ≤ `MaxConcurrency`.

### 4. Multi-account hub (`SmtpAccountPoolHub`)

- One `SmtpConnectionPool` per account Id (each with its own gate).
- Runner still spawns only `MaxConcurrency` workers.
- Therefore **total concurrent rents across all pools ≤ MaxConcurrency** (worker bottleneck).
- Pre-warm may create up to `Accounts × MaxConcurrency` **idle connections** (inventory), not concurrent SEND workers. This is intentional for persistent multi-account sessions and is **not** a message-concurrency overshoot.

### 5. Actual SEND pacing (`SmartPaceController`)

```text
_sendGate = new SemaphoreSlim(1, 1)
AcquireSendSlotAsync → holds gate during SendAsync
```

- At most **one** in-flight `SendAsync` through the global pace gate (stronger than `MaxConcurrency`).

## Verdict

| Layer                         | Cap                         | Overshoot? |
|------------------------------|-----------------------------|------------|
| Workers                      | MaxConcurrency              | No         |
| Adaptive                     | MaxConcurrency              | No         |
| Pool gate (per account)      | MaxConcurrency              | No*        |
| Concurrent SEND (pace gate)  | 1                           | No         |
| Pre-warm connection inventory| Accounts × MaxConcurrency   | N/A**      |

\* Multi-account: sum of concurrent rents still ≤ workers ≤ MaxConcurrency.  
\** Inventory only; not concurrent message processing.

**A6 = PASS without adding a second global SemaphoreSlim or changing the execution model.**

## Explicitly out of scope for A6

- New limiter on top of workers + pool + pace gate
- Changing multi-account pre-warm policy (A7/report may surface `PoolConnections`)
- Retry/requeue concurrency (covered by A4 budget + same workers)

## Follow-up (optional, not blocking)

- GUI: clarify that multi-account pre-warm can open more *connections* than MaxConcurrency
- A7: report peak concurrent workers if instrumented later
