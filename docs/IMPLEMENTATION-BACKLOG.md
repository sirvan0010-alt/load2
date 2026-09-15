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

### B5 Behavioral Analyzer — COMPLETE, CI VERIFIED

Implemented in `src/MailLoadTester.Core/BehavioralAnalyzer.cs` with focused tests in `tests/MailLoadTester.Tests/BehavioralAnalyzerTests.cs`.

Delivered normalized deterministic events, burst/velocity analysis, provider and sender-domain diversity, recipient concentration, bounded anomaly scoring, findings, B4 event mapping, cancellation and validation. No network I/O and no modification of delivery execution.

The initial CI failure was test-only: the record result contained a collection whose reference identity made a direct record equality assertion order-sensitive. The test was corrected to compare value fields and findings content; the current full CI suite passes.

### B6 Mailbox / Deliverability Lab — COMPLETE, CI VERIFIED

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

The focused lab test was corrected to use the defined 80% pressure threshold while still exercising byte quota rejection. The current full CI suite passes.

### B7 Authentication / Transport Evidence — COMPLETE, CI VERIFIED

Implemented as a projection over the existing `TransportDiagnostics` pipeline in `src/MailLoadTester.Core/AuthenticationTransportEvidence.cs`.

Delivered:

- machine-readable SMTP/TLS/MX/SPF/DMARC evidence status;
- observed TLS protocol and SMTP authentication state/mechanisms;
- DNS-check failure distinguished from a confirmed missing record;
- explicit DKIM `NotEvaluated` state when no selector is supplied rather than guessing;
- preserved diagnostic steps and error evidence;
- compact JSON serialization;
- no credentials or second diagnostics engine;
- focused tests covering observed evidence, DKIM boundary and secret absence.

The B7 implementation is intentionally a projection: it does not create a parallel network diagnostic stack. The current full CI suite passes.

### B8 Replayable Artifacts — IMPLEMENTED, CI PENDING

Implemented in `src/MailLoadTester.Core/ReplayableRunArtifact.cs` with focused tests in `tests/MailLoadTester.Tests/ReplayableRunArtifactTests.cs`.

Delivered:

- versioned scenario/configuration/event/result artifact contract;
- deterministic SHA-256 integrity fingerprint over the unsigned artifact payload;
- recursive redaction of passwords, secrets, tokens, API keys, authorization/cookie material and private keys;
- deterministic JSON serialization for identical inputs;
- cancellation-aware asynchronous persistence;
- filesystem-safe run-id normalization;
- atomic temporary-file replacement to avoid partial final artifacts;
- no network I/O, credentials or replay execution;
- data-only boundary suitable for later deterministic replay tooling.

The artifact writer deliberately does not invent missing runtime events: callers supply the normalized event collection they actually observed.

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
