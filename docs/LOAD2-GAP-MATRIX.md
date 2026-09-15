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
| B5 behavioral analyzer | IMPLEMENTED — CI PENDING | corrected tests + CI verification |
| B6 mailbox/deliverability lab | IMPLEMENTED — CI PENDING | lab tests + CI verification |
| B7 richer auth/transport evidence | POST-BASELINE | evidence model over existing diagnostics |
| B8 replayable redacted artifacts | POST-BASELINE | deterministic artifact contract |
| B9 security execution gates | POST-BASELINE | preflight/evidence enforcement |

## B3–B6 delivered behavior

**B3:** typed scenario definitions and a single `ScenarioEngine` adapter route supported scenarios through the existing SMTP runner. Simulation-only scenario kinds remain explicitly blocked from direct SMTP execution.

**B4:** deterministic local provider simulation supports accepted, throttled, temporary-failure and permanent-failure outcomes, seeded reproducibility, cancellation and multi-provider composition. No network I/O and no second queue/pacing/retry stack.

**B5:** side-effect-free analysis over normalized mail events calculates event velocity, peak burst size, provider diversity, sender-domain diversity, recipient concentration and a bounded transparent anomaly score. It maps B4 simulator events and honors cancellation. A CI-only constructor mismatch was found and corrected.

**B6:** controlled in-memory mailbox/deliverability lab supports message and byte quotas, provider-outcome rejection, mailbox pressure snapshots, reads and bounded recovery. It is thread-safe, cancellation-aware and intentionally performs no SMTP/IMAP network I/O. A future real MTA/IMAP environment remains an external adapter boundary rather than a second core transport stack.

## Security boundary

The framework supports controlled/authorized security testing, not a public abuse launcher. No arbitrary third-party registrations, CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target DoS/DDoS. Defensive objectives use controlled simulators, owned applications, synthetic recipients/providers and bounded lab workers.

## Architecture constraint

```text
ScenarioDefinition → ScenarioEngine → existing bounded Channel
→ existing workers/MaxConcurrency → existing pacing + provider/recipient gates
→ existing SMTP pools → existing SEND/outcome classification
→ DeliveryLedger/RetryMetrics/RunReport → RunObservability + BehavioralAnalysis
→ controlled MailboxDeliverabilityLab (lab boundary)
```

B4–B9 must not introduce a second queue, pacing/limiting stack, retry policy or source-of-truth result model.

## Rules for future work

- Do not reopen A1–A8 without concrete regression evidence.
- Preserve cancellation, hard limits, DryRun/TestMode, `--unauthorized` and secret redaction.
- Every retry remains under existing pacing/concurrency controls.
- Every new status requires source/test/CI evidence.
- Synchronize canonical documentation after verified implementation changes.
