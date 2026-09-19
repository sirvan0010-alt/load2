# load2 — Mail Security Architecture

**Status:** design baseline for TRACK B  
**Authority:** source + tests on `main`

## 1. Layers

```text
┌─────────────────────────────────────────────┐
│ Scenario / Security Test Definition         │
├─────────────────────────────────────────────┤
│ Provider / Workflow Simulation              │
├─────────────────────────────────────────────┤
│ Existing SMTP Load Engine                   │
│ Channel → workers → pacing → sessions       │
├─────────────────────────────────────────────┤
│ Transport / SMTP / TLS / DNS diagnostics    │
├─────────────────────────────────────────────┤
│ Evidence / RunReport / RunObservability     │
└─────────────────────────────────────────────┘
```

The network/transport layer must not contain application-workflow generation logic.

## 2. Scenario model

A scenario describes **what should be tested**, not how to bypass a control.

The initial scenario kinds are:

- normal delivery;
- burst delivery;
- sustained load;
- connection saturation;
- provider distribution;
- failure injection;
- mailbox quota/saturation;
- deliverability/authentication;
- subscription-bomb simulation;
- Double-Opt-In simulation;
- anti-abuse control simulation.

Scenario execution must be translated into the existing bounded execution path.

## 3. Provider simulation

Provider simulation is an internal test abstraction. A simulator can represent:

- provider identity;
- sender-domain population;
- message workflow/type;
- acceptance/throttling/rejection behavior;
- latency and transient failures;
- authentication-result patterns;
- mailbox delivery outcome.

Simulators must not contact arbitrary external providers merely to reproduce a security scenario.

## 4. Behavioral analysis

The analyzer consumes normalized mail events and produces evidence such as:

- messages per second/minute;
- burst size and burst duration;
- unique sender count;
- unique sender-domain count;
- recipient concentration;
- provider diversity;
- authentication-result distribution;
- rejection/throttle rate;
- mailbox utilization/pressure;
- recovery time after a control is triggered.

An anomaly score is an analysis result, not an execution control. Thresholds must be configurable and documented.

## 5. Controlled abuse-pattern simulations

The framework may model abuse patterns for defensive testing when the traffic terminates in an owned or explicitly authorized environment.

Examples:

- subscription bombing simulation → synthetic provider fleet → controlled mailbox;
- Double-Opt-In simulation → owned registration test app → synthetic confirmation messages;
- botnet simulation → bounded distributed lab workers, never a real botnet;
- proxy diversity simulation → explicitly configured lab proxies, never limit-evasion automation.

CAPTCHA/OTP bypass and third-party registration automation are not implementation requirements.

## 6. Security gates

Every high-impact scenario must pass:

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

Failure at any gate blocks execution.

## 7. Data and evidence

Security scenarios should use stable identifiers rather than raw secrets or unnecessary personal data. Evidence must be redacted before persistence. Run artifacts should be reproducible without storing SMTP passwords, proxy credentials or OAuth tokens.

## 8. Extension point

Message-generation behavior belongs behind `IMailPayloadPlugin` and related plugin infrastructure. Provider simulation is orchestration/test behavior; SMTP transport remains in the existing transport layer.
