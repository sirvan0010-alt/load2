# load2 — Post-baseline mechanism and product plan

**Authority:** source + tests on `main`  
**Canonical roadmap:** `docs/LOAD2-ROADMAP.md`

This document converts external research into concrete TRACK B work. It does not reopen TRACK A.

## 1. External mechanisms already covered

| Mechanism | Current load2 capability | Decision |
|---|---|---|
| rate vs concurrency separation | `SmartPaceController` + bounded concurrency | HARDEN, do not duplicate |
| timeout/probe separation | transport diagnostics + observability | ADOPT principle |
| event/report projection | `MailTestResult` → `RunReport` → `RunObservability` | ADOPT principle |
| SMTP session reuse | `SmtpConnectionPool` | HAVE |
| worker monitoring/shutdown | bounded workers + `CancellationToken` | HARDEN |
| payload variation | `IMailPayloadPlugin` | HAVE / ADAPT where needed |
| endpoint canonicalization | canonical health keys + target deduplication | HAVE |

## 2. Mailcannon decision

`rojberr/mailcannon` is **REFERENCE only** for the current product.

Its principal differentiator is distributed SMTP load generation through Docker/Swarm/Kubernetes and multiple machines. For a one-PC load2 deployment this adds operational complexity without a demonstrated requirement. load2 already has bounded local concurrency, pacing and connection/session controls.

No mailcannon dependency or port is planned. Reconsider only if a concrete single-host mechanism, reproducibility feature or benchmark methodology is shown to improve load2.

## 3. B2 — external repository transfer

For every retained repository, record:

```text
pinned revision
→ entry points / symbols
→ execution trace
→ mechanism inventory
→ load2 mapping
→ decision
→ focused evidence
```

Unknown source facts remain `PENDING`.

## 4. B3 — Scenario Engine

Goal: represent test intent as typed scenarios while retaining the existing execution engine.

Initial kinds:

- `NormalDelivery`
- `BurstDelivery`
- `SustainedLoad`
- `ConnectionSaturation`
- `ProviderDistribution`
- `FailureInjection`
- `MailboxQuota`
- `Deliverability`
- controlled security simulations

The scenario layer must translate into the existing bounded Channel/workers/pacing/concurrency/session/reporting path.

Required properties:

- cancellation-aware;
- deterministic when a seed is supplied;
- hard-limit aware;
- authorization-aware;
- no second queue;
- no second pacing system;
- no second retry pipeline;
- existing `MailTestResult` remains the result authority.

## 5. B4 — Provider Simulator

Goal: model provider behavior locally or in a controlled lab so defensive workflows can be tested without automating abuse against third parties.

The simulator should represent:

- provider identity;
- sender-domain population;
- workflow/message type;
- acceptance, throttling and rejection;
- latency and transient failure;
- authentication-result pattern;
- mailbox outcome.

A deterministic seed must reproduce the same generated event sequence. Simulation must not silently contact arbitrary external providers.

## 6. B5 — Behavioral Analyzer

Consume normalized events and calculate:

- velocity;
- burst size/duration;
- sender/domain diversity;
- recipient concentration;
- provider diversity;
- authentication-result distribution;
- throttle/rejection rate;
- mailbox pressure;
- recovery time.

Return evidence classification:

`Normal | Elevated | Suspicious | HighRisk`

## 7. B6/B7/B8/B9

- B6: controlled SMTP/IMAP mailbox and deliverability lab;
- B7: richer SPF/DKIM/DMARC/TLS evidence without duplicating diagnostics;
- B8: replayable redacted scenario/config/event/result artifacts;
- B9: explicit security execution gates.

## 8. Controlled security boundary

Subscription-bombing, Double-Opt-In, anti-abuse, botnet-distribution and proxy-diversity objectives may be represented as **controlled simulations** against owned/authorized systems.

Do not implement arbitrary third-party registration automation, CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target flooding/DoS/DDoS launchers.

The security objective is to reproduce observable conditions and measure defensive controls, not to remove those controls.

## 9. Implementation gate

Before each implementation item:

```text
source audit → minimal design → focused tests → security review → implementation → CI → documentation sync
```

If source evidence shows the behavior already exists, mark it `ALREADY-COVERED` instead of creating duplicate code.
