# Post-baseline external mechanism implementation plan

Based on source-level audits of `slowhttptest@bbd33de733ccb5d7c87ffefebe1373d035a573a1` and `GoldenEye@792862f5c8cb98f9ffcb9fab245e2c663e3a1026`.

## Already covered by load2

| External mechanism | Existing load2 mechanism | Action |
|---|---|---|
| slowhttptest rate vs concurrency | `SmartPaceController` + bounded concurrency/session pool | HARDEN by regression coverage, not duplicate implementation |
| slowhttptest timeout/probe separation | transport health/diagnostics + observability | ADOPT principle; verify separation |
| slowhttptest authoritative event/report model | `MailTestResult` + `RunReport` + `RunObservability` | ADOPT principle; no second reporting model |
| GoldenEye session reuse | `SmtpConnectionPool` persistent sessions | ADOPT; retain current lifecycle controls |
| GoldenEye worker monitoring/shutdown | bounded async workers + `CancellationToken` | HARDEN tests and cleanup invariants |
| GoldenEye explicit TLS verification policy | SMTP/TLS diagnostics and `TlsMatrixTests` | ADAPT only where a concrete policy gap is demonstrated |

## Candidates requiring implementation work

### EXT-001 — bounded diagnostic partial-I/O scenario
Source: slowhttptest M-003.

Implement only as a domain-specific, explicitly authorized diagnostic scenario if a real load2 requirement exists. It must reuse existing pacing, concurrency, cancellation, authorization and reporting rather than creating a parallel engine.

### EXT-002 — scenario diversity through payload plugins
Source: slowhttptest M-006 and GoldenEye M-009.

Use `IMailPayloadPlugin` for bounded message/header/body variation. Randomization must remain deterministic when a seed is supplied and must be represented in run evidence. It must not bypass transport controls.

### EXT-003 — connection/session lifecycle state evidence
Source: slowhttptest M-001.

Where session lifecycle transitions are currently implicit, expose them through existing observability/protocol-path mechanisms rather than adding a second state machine. Add focused tests only for a demonstrated observability gap.

## Non-goals

- Do not port HTTP-specific reactor code into the C# SMTP engine.
- Do not add a second rate limiter or concurrency controller.
- Do not copy attack orchestration merely because an external project contains it.
- Do not weaken TLS verification by default.
- Do not claim an implementation is complete until source, tests and CI verify it.

## Next implementation gate

Before implementing EXT-001/002/003, the implementation agent must inspect the current source and tests again and produce a minimal patch plan. If the current architecture already provides the required behavior, close the candidate as `ALREADY-COVERED` rather than duplicating it.
