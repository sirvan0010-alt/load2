# load2 — Current Capability and Gap Matrix

**Authority:** `sirvan0010-alt/load2` / `main`  
**Baseline:** TRACK A A1–A8 COMPLETE and protected.  
**Current evolution:** TRACK B post-baseline.

## Status rules

`HAVE` means demonstrated in current source/tests. `FOUNDATION` means the contract exists but integration is incomplete. `POST-BASELINE` means planned work. `COMPLETE` requires source/tests/CI/documentation evidence.

## TRACK A — closed

A1–A8 remain closed and regression-protected. Phase 1.3 agent-runtime Docker boundary is verified by green CI.

## Existing engine capability

The existing bounded `Channel<T>`, workers/`MaxConcurrency`, `SmartPaceController`, recipient/provider gates, SMTP pools, `RetryPolicy`, `DeliveryLedger`, `RunReport`, `RunObservability`, diagnostics and `IMailPayloadPlugin` remain the single execution/evidence pipeline.

## TRACK B status

| Item | Status | Next gate |
|---|---|---|
| B1 canonical documentation reset | COMPLETE | maintain synchronization |
| B2 external mechanism audit | COMPLETE — EXT-AUDIT-001 | future repos are separate audits |
| B3 typed scenario engine | COMPLETE — REPAIRED | regression coverage |
| B4 provider simulator | COMPLETE — CI VERIFIED | maintain deterministic/security boundary |
| B5 behavioral analyzer | COMPLETE — CI VERIFIED | maintain regression coverage |
| B6 mailbox/deliverability lab | COMPLETE — CI VERIFIED | maintain lab/security boundary |
| B7 richer auth/transport evidence | COMPLETE — CI VERIFIED | maintain evidence projection |
| B8 replayable redacted artifacts | IMPLEMENTED — CI PENDING | deterministic artifact contract |
| B9 security execution gates | IMPLEMENTED — CI PENDING | full CI verification |

## B3–B7 delivered behavior

**B3:** typed scenario definitions and a single `ScenarioEngine` adapter route supported scenarios through the existing SMTP runner. Simulation-only scenario kinds remain explicitly blocked from direct SMTP execution.

**B4:** deterministic local provider simulation supports accepted, throttled, temporary-failure and permanent-failure outcomes, seeded reproducibility, cancellation and multi-provider composition. No network I/O and no second queue/pacing/retry stack.

**B5:** side-effect-free analysis over normalized mail events calculates event velocity, peak burst size, provider diversity, sender-domain diversity, recipient concentration and a bounded transparent anomaly score. It maps B4 simulator events and honors cancellation. A CI-only test equality issue was corrected by making the determinism assertion value-based.

**B6:** controlled in-memory mailbox/deliverability lab supports message and byte quotas, provider-outcome rejection, mailbox pressure snapshots, reads and bounded recovery. It is thread-safe, cancellation-aware and intentionally performs no SMTP/IMAP network I/O. The focused pressure test now matches the defined 80% pressure threshold.

**B7:** `AuthenticationTransportEvidence` projects existing `TransportDiagnosticReport` data into machine-readable evidence with explicit statuses for SMTP, TLS, MX, SPF and DMARC, authentication mechanisms, preserved diagnostic steps and an explicit DKIM-not-evaluated state when no selector is supplied. No second diagnostics engine or credential-bearing result model was introduced.

## B8 — replayable artifacts

`ReplayableRunArtifactBuilder` provides a versioned scenario/configuration/event/result artifact, deterministic SHA-256 fingerprinting, recursive secret-key redaction, deterministic JSON and cancellation-aware atomic persistence. It is data-only and does not execute replay or create network traffic.

## B9 — security execution gate

`SecurityExecutionGate` is the pre-execution admission boundary for typed scenarios. It enforces explicit scope, authorization, hard limits and cancellation checks before `ScenarioEngine` delegates to the existing SMTP runner. Runtime pacing, bounded concurrency, secret redaction and evidence remain enforced by their existing authoritative components rather than being duplicated in B9.

## Security boundary

The framework supports controlled/authorized security testing, not a public abuse launcher. No arbitrary third-party registrations, CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target DoS/DDoS. Defensive objectives use controlled simulators, owned applications, synthetic recipients/providers and bounded lab workers.

## Architecture constraint

```text
ScenarioDefinition → SecurityExecutionGate → ScenarioEngine → existing bounded Channel
→ existing workers/MaxConcurrency → existing pacing + provider/recipient gates
→ existing SMTP pools → existing SEND/outcome classification
→ DeliveryLedger/RetryMetrics/RunReport → RunObservability + BehavioralAnalysis
→ controlled MailboxDeliverabilityLab → AuthenticationTransportEvidence
→ ReplayableRunArtifactBuilder
```

B4–B9 must not introduce a second queue, pacing/limiting stack, retry policy or source-of-truth result model.

## Rules for future work

- Do not reopen A1–A8 without concrete regression evidence.
- Preserve cancellation, hard limits, DryRun/TestMode, `--unauthorized` and secret redaction.
- Every retry remains under existing pacing/concurrency controls.
- Every new status requires source/test/CI evidence.
- Synchronize canonical documentation after verified implementation changes.
