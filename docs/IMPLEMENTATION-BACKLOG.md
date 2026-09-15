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

The adapter intentionally does not reinterpret scenario metadata into a second options engine. Existing load2 execution controls remain the single source of execution behavior.

### B4 Provider Simulator — IMPLEMENTED, CI PENDING

Implemented in `src/MailLoadTester.Core/ProviderSimulator.cs`.

Delivered:

- deterministic local provider simulation with a supplied seed;
- provider identity and workflow/message type;
- accepted/throttled/temporary/permanent outcomes;
- latency and authentication-result patterns;
- `CancellationToken` support;
- bounded event count (1..10000);
- `ProviderScenarioSimulator` composition with the B3 `LoadScenarioDefinition`;
- deterministic multi-provider distribution for controlled scenarios;
- duplicate provider-id protection;
- no network access and no external-provider credentials;
- focused deterministic, cancellation, outcome and B3-composition tests.

Security boundary: simulation is local/controlled. It does not automate third-party registrations, bypass CAPTCHA/OTP, evade provider limits or create unrestricted public traffic.

Acceptance after CI verification:

- deterministic seed;
- provider identity/workflow model;
- throttling/transient/permanent failure/latency/authentication-result events;
- no arbitrary external-provider calls;
- composition with B3;
- focused tests;
- CI green.

### B5 Behavioral Analyzer — POST-BASELINE

Normalize transport/provider/mailbox events and calculate evidence for velocity, burst duration, sender/domain diversity, recipient concentration, provider diversity, authentication results, throttling/rejection and mailbox pressure.

Output: `Normal | Elevated | Suspicious | HighRisk`.

### B6 Mailbox / Deliverability Lab — POST-BASELINE

Use a controlled SMTP/IMAP environment for delivery, mailbox quota, recovery and authentication testing. Keep the MTA/test environment outside the core transport layer.

### B7 Authentication / Transport Evidence — POST-BASELINE

Extend existing MX/SPF/DKIM/DMARC/TLS diagnostics into evidence-rich scenario results without creating a second diagnostics engine.

### B8 Replayable Artifacts — POST-BASELINE

Persist redacted scenario/configuration/event/result artifacts sufficient for deterministic reproduction without secrets or unnecessary personal data.

### B9 Security Execution Gates — POST-BASELINE

Enforce:

```text
SCOPE
→ AUTHORIZATION
→ HARD LIMIT
→ CANCELLATION
→ PACING
→ CONCURRENCY
→ SECRETS
→ EVIDENCE
→ TEST
→ CI
```

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

Use controlled simulations, owned applications, synthetic providers/recipients and bounded lab workers for defensive testing.

## Definition of done

An item becomes `COMPLETE` only when source implementation, focused tests, security review, CI evidence and canonical documentation are synchronized. Historical notes and external README claims cannot close an item.
