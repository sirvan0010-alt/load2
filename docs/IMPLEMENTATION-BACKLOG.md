# load2 — Implementation Backlog

**Authority:** source + tests on `main`.  
**Canonical roadmap:** `docs/LOAD2-ROADMAP.md`.  
**Current phase:** TRACK B post-baseline.

## TRACK A — CLOSED

A1–A8 are complete and remain the protected baseline.

## P0 — baseline integrity

- preserve green CI for the verified agent-runtime Docker boundary;
- keep regression coverage for A1–A8;
- keep `--unauthorized`, DryRun/TestMode, hard limits, cancellation and secret redaction mandatory.

## P1 — TRACK B

### B1 Documentation reset — COMPLETE

`docs/DOCUMENTATION-MAP.md` is the canonical map. Historical audit/version files are evidence/history rather than competing plans.

### B2 External repository audit — COMPLETE

EXT-AUDIT-001 is closed. Future repositories are separate audit items. `rojberr/mailcannon` is reference-only for the current one-PC deployment.

### B3 Scenario Engine — COMPLETE, REPAIRED

The first B3 adapter attempt introduced duplicate `LoadScenarioKind` / `LoadScenarioDefinition` declarations and failed the release build. That defect was removed. The canonical typed scenario contract remains `src/MailLoadTester.Core/LoadScenario.cs`, while `src/MailLoadTester.Core/ScenarioEngine.cs` now contains only the execution adapter.

Delivered:

- typed `LoadScenarioKind` / immutable `LoadScenarioDefinition`;
- authorization validation for non-test execution;
- bounded message/concurrency/interval/duration validation;
- `ScenarioEngine` adapter over the existing `SmtpTestRunner`;
- no second queue, pacing/limiting stack, concurrency stack or retry policy;
- existing cancellation and result/report pipeline remain authoritative;
- focused scenario adapter tests.

### B4 Provider Simulator — COMPLETE, CI VERIFIED

Implemented in `src/MailLoadTester.Core/ProviderSimulator.cs`.

Delivered deterministic local provider simulation, seeded reproducibility, provider/workflow identity, accepted/throttled/temporary/permanent outcomes, latency/authentication-result patterns, cancellation, bounded event count, B3 composition, duplicate-provider protection and focused tests. The implementation performs no network I/O and introduces no second queue/pacing/retry stack.

CI evidence: the verified B4 branch run was green across the required repository checks.

### B5 Behavioral Analyzer — IMPLEMENTED, CI PENDING

Implemented in `src/MailLoadTester.Core/BehavioralAnalyzer.cs` with focused tests in `tests/MailLoadTester.Tests/BehavioralAnalyzerTests.cs`.

Delivered normalized deterministic events, burst/velocity analysis, provider and sender-domain diversity, recipient concentration, bounded anomaly scoring, findings, B4 event mapping, cancellation and validation. No network I/O and no modification of delivery execution.

The first CI attempt exposed a test-only constructor mismatch against the canonical B4 event contract. It has been corrected; fresh CI is running on the corrected head. B5 is not marked COMPLETE until that CI gate passes.

### B6 Mailbox / Deliverability Lab — IMPLEMENTED, CI PENDING

Implemented as a controlled in-memory lab in `src/MailLoadTester.Core/MailboxDeliverabilityLab.cs` with focused tests in `tests/MailLoadTester.Tests/MailboxDeliverabilityLabTests.cs`.

Delivered:

- explicit mailbox/quota contract;
- message-count and byte-count quota enforcement;
- accepted/rejected/quota-exceeded dispositions;
- provider-outcome-aware delivery behavior;
- mailbox snapshots and pressure indicators;
- deterministic mailbox reads;
- bounded recovery/removal operation;
- thread-safe mailbox state;
- `CancellationToken` support;
- no SMTP/IMAP sockets, credentials or external traffic;
- explicit boundary for a future external MTA adapter outside the core transport engine.

This closes the core B6 lab boundary without introducing a second SMTP queue, pacing system, retry policy or network transport. CI verification remains the final acceptance gate.

### B7 Authentication / Transport Evidence — POST-BASELINE

Extend existing MX/SPF/DKIM/DMARC/TLS diagnostics into evidence-rich scenario results without creating a second diagnostics engine.

### B8 Replayable Artifacts — POST-BASELINE

Persist redacted scenario/configuration/event/result artifacts sufficient for deterministic reproduction without secrets or unnecessary personal data.

### B9 Security Execution Gates — POST-BASELINE

Enforce SCOPE → AUTHORIZATION → HARD LIMIT → CANCELLATION → PACING → CONCURRENCY → SECRETS → EVIDENCE → TEST → CI.

## P2 — advanced authorized lab automation

- distributed authorized lab workers;
- controlled proxy/IP diversity as a lab topology dimension;
- registration/Double-Opt-In simulators for owned applications;
- anti-bot/anti-abuse control testing without bypass;
- mailbox saturation/recovery;
- failure injection/replay;
- GUI scenario selection and live observability.

## Explicit non-goals

Do not implement arbitrary third-party registration automation, CAPTCHA/OTP bypass, anti-abuse evasion, real botnet operation, provider-limit evasion, credential/token theft, reflection/amplification or unrestricted public-target flooding/destructive DoS/DDoS.

## Definition of done

An item becomes `COMPLETE` only when source implementation, focused tests, security review, CI evidence and canonical documentation are synchronized.
