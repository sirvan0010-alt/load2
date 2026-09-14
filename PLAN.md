# MailLoadTester (load2) — plan

**AI entry:** `docs/AI-GUIDE.md`  
**Implementation authority:** `sirvan0010-alt/load2`, branch `main`.

## Current state

| Track | Status |
|---|---|
| **TRACK A — Engine A1–A8** | ✅ CLOSED — ALL COMPLETE |
| **EXT-AUDIT-001** | ✅ Current retained set audited; new repositories are separate post-baseline work |
| **Post-baseline engineering** | Optional; only with concrete requirement/evidence |

The final TRACK A record is `docs/A8-BASELINE.md`. The implementation backlog is `docs/IMPLEMENTATION-BACKLOG.md`.

## TRACK A

```text
A1 → A2 → A3 → A4 → A5 → A6 → A7 → A8
ALL COMPLETE
```

Do not reopen completed milestones because of historical `PLAN.md`, audit notes or chat text. A new regression requires source/test/CI evidence and a separate post-baseline item.

## Current engine invariants

- One `SmartPaceController` pacing system.
- Bounded `Channel` and workers governed by `MaxConcurrency`.
- `AcquireSendSlotAsync` before every real SEND, including retries.
- Persistent SMTP sessions through the pool architecture.
- `DeliveryLedger` keeps accepted messages terminal across AutoRestart.
- `RetryPolicy` uses unified `SmtpOutcomeClassifier` and retry budget/metrics.
- `MailTestResult` is the source of truth; `RunReport` and `RunObservability` are projections.
- Cancellation, DryRun/TestMode, `--unauthorized`, hard limits and secret redaction remain mandatory.

## Post-baseline candidates

- GUI summary panel backed by `RunObservability.FromReport`.
- Authorized `NET-AUDIT-001` fixtures.
- External-repository ADOPT/ADAPT work based on source evidence.
- Release packaging/version decision.

## External audit rule

Study offensive repositories by their actual mechanisms, not their labels. Useful mechanisms may be adapted for authorized testing. Do not import credential theft, bypasses, stealth/evasion for abuse, arbitrary public-target discovery for flooding, reflection/amplification or unrestricted destructive DDoS/flooding paths.
