# MailLoadTester — MASTER AUDIT REGISTER

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> This document is an audit/history register. Current implementation status must be verified against `main`; historical claims are not authoritative.

## Audit method

For each component, inspect entry points/callers, async and cancellation flow, shared state and synchronization, resource ownership, boundary values, retry/pacing interactions, security-sensitive data flow, configuration validation, GUI lifecycle, and regression-test coverage. A feature is not marked implemented merely because it is proposed in an older document.

## Current execution model — IMPLEMENTED / TEST-BACKED AREAS

`main` now contains the repaired execution model:

- bounded `Channel<T>` work queue with fixed workers derived from `MaxConcurrency`;
- per-message delivery ledger so AutoRestart does not duplicate already accepted logical messages;
- actual-SEND pacing through `SmartPaceController` and an exclusive `SemaphoreSlim` gate;
- first actual send is immediate;
- retries reacquire the same actual-SEND gate after retry/admission delays;
- adaptive concurrency and SMTP-pool admission occur before actual-SEND pacing;
- per-recipient limiting remains separate from global pacing;
- `CancellationToken` is propagated through worker, limiter, pool and send paths.

These are the current invariants. Do not resurrect the former task-per-message or pre-admission pacing model.

## Historical defect register — status must follow current evidence

BUG-001 through BUG-009 were the original deep-audit findings. The execution-model fixes for BUG-001/003/007/009 are merged into `main`; BUG-004/005 have implementation and regression-test evidence; BUG-002 and BUG-008 have corresponding ledger/routing handling in the current runner. Before declaring the complete ledger closed, verify current tests and source rather than relying on this historical paragraph.

## Security / robustness status

Current `main` contains:

- explicit `--unauthorized` handling through `AuthorizationGate` for live sending outside TestMode/DryRun;
- protocol AUTH-secret redaction through the MailKit authentication-secret detector path;
- path traversal validation plus `PathSecurity` reparse-point checks on relevant file-producing/reading paths;
- plugin directory/DLL path hardening;
- MIME/attachment quotas and bounded resource handling;
- environment/configuration based secret handling rather than hardcoded credentials;
- CI and CodeQL workflows.

Remaining work must be identified from source/tests, not by reopening already verified historical findings without evidence.

## Networking / diagnostics

Current implementation includes SMTP connectivity/TLS mapping, SMTP connection pooling, proxy rotation and blocked-endpoint exclusion, IPv4/IPv6 binding/rotation, MX resolution, DNS policy checks and Direct-MX routing. The network matrix still benefits from targeted integration testing where an actual SMTP test service is available.

## Offensive/load-generation audit policy

The project purpose includes user-selected SMTP/load scenarios. A concrete recipient or target supplied by the user is part of the normal scenario model; the audit must therefore study implementations that perform repeated delivery, high concurrency, provider fan-out, retries, endpoint rotation, flooding-style scheduling and failure recovery rather than filtering them out merely because their source projects call themselves "bombers".

The distinction is **not** "offensive code is forbidden to study". The distinction is:

1. study the mechanism and source implementation;
2. identify the engineering value;
3. map it to the existing `load2` transport/orchestration model;
4. preserve explicit authorization, cancellation, pacing and concurrency semantics;
5. do not add stealth, credential theft, abuse-control bypasses, public-target discovery for flooding, or mechanisms whose primary purpose is uncontrolled harm.

`--unauthorized` is an explicit authorization acknowledgement, not a claim that every destination is automatically authorized. The application may accept a user-specified concrete recipient as input; authorization semantics must remain explicit and auditable.

## External repository research

`docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` is the dedicated transfer register. Its purpose is to record **source-level** findings, not merely README impressions.

Every requested external repository should eventually be classified per concrete implementation unit as:

- `ADOPT` — transfer the mechanism with minimal architectural change;
- `ADAPT` — reimplement it against existing `load2` interfaces;
- `HARDEN` — useful mechanism but current implementation needs stronger correctness/security;
- `SIMULATE` — reproduce the behavior against a controlled test target;
- `EXTRACT` — keep only a reusable algorithm/data model;
- `REFERENCE` — useful for comparison but not yet ready to transfer;
- `REJECT` — no legitimate engineering value or unacceptable mechanism.

## Final verification

Required evidence includes Release build/test, targeted concurrency/pacing tests, retry pacing, AutoRestart ledger behavior, Direct-MX routing, proxy ban behavior, IPv4 `/31` and `/32`, AUTH redaction, path security, profile round-trip, TLS/network tests, GUI start/stop behavior, repository-integrity checks and CI/CodeQL status.

No PASS/FIXED claim without source plus test/CI evidence.
