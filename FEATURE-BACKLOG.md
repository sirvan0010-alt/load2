# MailLoadTester — FEATURE / CAPABILITY BACKLOG

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> Proposed work is not automatically implemented. This backlog now distinguishes engineering capabilities learned from external repositories from their original use case.

| ID | Priority | Area | Proposal | Status |
|---|---|---|---|---|
| FEAT-001 | High | Test orchestration | Bounded worker/channel execution model with explicit in-flight metrics. | IMPLEMENTED — verify on main |
| FEAT-002 | High | SMTP diagnostics | Per-message delivery-attempt ledger and unique-message accounting, including retry/restart visibility. | IMPLEMENTED — verify on main |
| FEAT-003 | High | Authorization | Centralized `--unauthorized` live-send gate with DryRun/TestMode behavior. | IMPLEMENTED — verify on main |
| FEAT-004 | Medium | DNS | Structured SPF/DKIM/DMARC diagnostics as read-only checks with evidence. | PROPOSED / PARTIAL |
| FEAT-005 | Medium | SMTP diagnostics | Open-relay verification as an explicit diagnostic. | PROPOSED |
| FEAT-006 | Medium | TLS | SMTP/TLS capability matrix and certificate diagnostics before a load run. | IMPLEMENTED PARTIAL — expand integration matrix |
| FEAT-007 | Medium | Observability | Unified event/metrics model for pacing, concurrency, circuit state, SMTP response classes and pool state. | PROPOSED |
| FEAT-008 | Medium | Profiles | Automatic profile validation, migration and safe round-trip checks. | PROPOSED |
| FEAT-009 | Low | UX | Preset modes that minimize configuration while keeping test parameters visible. | PROPOSED |
| FEAT-010 | High | Testing | Automated stress/property tests for limiter, pool, pacing, cancellation and composed execution. | IMPLEMENTED PARTIAL — expand |
| FEAT-011 | Medium | Reporting | Machine-readable run report with unique-message vs attempt metrics. | PROPOSED |
| FEAT-012 | High | Transport architecture | Common transport/provider registry over SMTP, persistent SMTP, Direct-MX and future diagnostic transports. | PROPOSED — external audit priority |
| FEAT-013 | High | Endpoint health | Provider/endpoint health state, cooldown, quarantine, recovery and reason codes. | PROPOSED — external audit priority |
| FEAT-014 | High | Endpoint normalization | Canonical endpoint IDs, URI normalization and duplicate elimination before execution. | PROPOSED — external audit priority |
| FEAT-015 | High | Scenario engine | Declarative scenario containing targets, transport, payload, count, concurrency, pacing, retry, TLS/DNS checks and stop conditions. | PROPOSED — external audit priority |
| FEAT-016 | High | Verification | Explicit VERIFY → evidence → EXECUTE workflow so diagnostics and active tests are independently visible. | PROPOSED — external audit priority |
| FEAT-017 | High | Failure handling | Typed result/failure classification driving retry and stop policy. | PROPOSED — external audit priority |
| FEAT-018 | Medium | Multi-target execution | User-specified target sets with per-target validation, bounded workers and per-target results. | PROPOSED — design against current runner |
| FEAT-019 | Medium | Reproducibility | Run IDs, deterministic scenario/config snapshots and replayable machine-readable artifacts. | PROPOSED |
| FEAT-020 | Medium | Provider discovery | Pluggable provider/endpoint inventory sources for explicitly configured or authorized infrastructure. | PROPOSED |
| FEAT-021 | Medium | Failure injection | Controlled SMTP/TLS/DNS/protocol failure scenarios against test infrastructure. | PROPOSED |
| FEAT-022 | Medium | Throughput telemetry | Separate transport latency, queue wait, limiter wait, pacing wait and actual-send timing. | PROPOSED |
| FEAT-023 | Medium | Payload testing | Pluggable malformed-message/protocol robustness scenarios through `IMailPayloadPlugin`. | PROPOSED |

## External repository policy

The external audit is deliberately broad. Repositories described as bombers, scanners, stress tools or automation projects are valid technical references. We audit their actual mechanisms rather than rejecting a repository because of its label.

The transfer question is per mechanism:

- **ADOPT** — useful mechanism maps directly to current architecture.
- **ADAPT** — reimplement against current C#/.NET/MailKit/MimeKit abstractions.
- **HARDEN** — adopt the idea after correcting correctness/security weaknesses.
- **SIMULATE** — reproduce the behavior against a controlled test service.
- **EXTRACT** — keep the algorithm/data model only.
- **REFERENCE** — retain for comparison pending source-level evidence.
- **REJECT** — no appropriate engineering value or mechanism cannot be safely integrated.

## Target model

`load2` is a load-testing application: the user supplies the concrete recipient/target and the test parameters. The audit must not invent a requirement that all targets be predeclared in a repository-owned allowlist when the current application does not have such a model.

The explicit `--unauthorized` gate remains the application's acknowledgement that a live send is an active operation. DryRun/TestMode remain non-live modes. Cancellation, bounded concurrency, actual-SEND pacing, retry accounting and other existing invariants remain mandatory.

## Explicit non-transfer boundary

Do not import mechanisms whose primary purpose is credential/token theft, stealth, CAPTCHA/OTP bypass, abuse-control evasion, automatic discovery of arbitrary public infrastructure for flooding, or uncontrolled destructive traffic. This does **not** prevent implementing legitimate load/stress behavior requested by the application's user within its existing execution model.

## Selection rule

Do not implement a capability merely because an external repository contains it. First record the exact source-level mechanism, compare it with `load2`, identify the target classes/interfaces, then add regression/integration tests and update this backlog to the verified status.
