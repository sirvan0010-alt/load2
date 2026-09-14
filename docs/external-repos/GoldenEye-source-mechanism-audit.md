# GoldenEye — source mechanism audit

Repository: `jseidl/GoldenEye`
Pinned revision: `792862f5c8cb98f9ffcb9fab245e2c663e3a1026`
Status: archived/deprecated upstream.

This audit extracts mechanisms from the source. It does not treat the original denial-of-service objective as a design requirement.

## M-008 — worker/socket concurrency model
**Evidence:** `goldeneye.py`

The source separates a controller (`GoldenEye`) from worker processes (`Striker`). Each worker owns a configured socket batch and reports counters through a shared manager object.

**Decision: REFERENCE**

The separation of orchestration, worker execution and metrics is useful. load2 should retain its bounded async worker/channel architecture rather than adopt Python multiprocessing or unbounded socket batches.

## M-009 — HTTP method/header variation
**Evidence:** `goldeneye.py`

The source supports GET/POST selection and a random method mode, and generates variable headers/user agents.

**Decision: ADAPT**

Only the general scenario-diversity mechanism is transferable. load2 should expose protocol-specific payload plugins/scenarios with explicit bounds and deterministic evidence. Randomization must never bypass authorization, pacing, recipient limits or observability.

## M-010 — keep-alive / session reuse
**Evidence:** `goldeneye.py`

Generated requests include `Connection: keep-alive` and a `Keep-Alive` header; the implementation groups requests around reusable connection objects before closing them.

**Decision: ADOPT**

The general session-reuse principle directly supports load2's persistent SMTP-session architecture. Preserve explicit session lifecycle, synchronization and cancellation rather than copying HTTP details.

## M-011 — worker scheduling and shutdown
**Evidence:** `goldeneye.py`

The controller starts multiple workers, periodically joins them with a bounded join timeout, monitors counters, and requests worker stop on interruption.

**Decision: HARDEN**

load2 already has cancellation and bounded workers. Strengthen shutdown tests so cancellation propagates through every worker/session and cleanup completes deterministically without orphaned work.

## M-012 — SSL verification policy
**Evidence:** `goldeneye.py`

The source has an explicit SSL verification setting and constructs HTTPS connections differently depending on that policy.

**Decision: ADAPT**

Transfer only the explicit TLS policy concept. load2 must default to certificate verification, make insecure/test modes explicit, and keep TLS configuration separate from SMTP payload generation.

## Transfer summary

| Mechanism | Decision | load2 direction |
|---|---|---|
| M-008 workers/sockets | REFERENCE | bounded workers + controlled session pool |
| M-009 method/header variation | ADAPT | bounded scenario/payload variation |
| M-010 session reuse | ADOPT | persistent SMTP sessions |
| M-011 shutdown scheduling | HARDEN | cancellation + deterministic cleanup |
| M-012 TLS verification | ADAPT | explicit safe TLS policy |

The upstream repository is deprecated, so these decisions are mechanism-level only and are pinned to the observed revision.
