# load2 — AI Agent Extensions

**Authority:** `sirvan0010-alt/load2`, branch `main` remains the source of truth.

This document is an additive extension to `AI_AGENT_REGISTRY.md`. It does not replace or weaken the existing roles, engine invariants, authorization boundary, or evidence rules.

## Additional roles

### 14. `RESEARCH_AGENT`
Owns source-backed technical research before implementation.

- Inspect external repositories at source level, not README-only.
- Record revision, entry points, execution trace, dependencies and concrete mechanisms.
- Classify each mechanism `ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`.
- Compare external behavior with the current load2 implementation before declaring a gap.
- Never treat an external project as authoritative over load2 `main`.
- Produce evidence suitable for `EVIDENCE_AGENT` review.

### 15. `NETWORK_STRESS_AGENT`
Owns controlled network/application stress design relevant to SMTP and IT infrastructure.

- TCP connection establishment and churn.
- Connection-rate and concurrency envelopes.
- Slow/partial I/O and timeout behavior as controlled laboratory scenarios.
- Backpressure, connection ceilings, minimum-data-rate style defenses and recovery observation.
- IPv4/IPv6 and proxy-path test coverage where supported.
- Test-duration, connection-count and rate limits must remain explicit and bounded.
- Every executable scenario must preserve authorization, cancellation, pacing, hard limits and observability.

This role may study mechanisms from HTTP stress/DoS research, but must not import an unrestricted public-target DoS/DDoS path.

### 16. `INTEGRATION_AGENT`
Owns cross-layer integration and agent handoffs.

- Verify that a proposed change maps to the existing TargetSet → Channel → workers → pacing → SMTP pool → `AcquireSendSlotAsync` → outcome → ledger/report pipeline.
- Detect duplicate implementations of queues, pacing, retry, metrics or observability.
- Verify interfaces between protocol, engine, network, metrics, scenario and UX layers.
- Require focused tests for each changed boundary.
- Reject changes that silently bypass cancellation, authorization or hard limits.

### 17. `RELEASE_AGENT`
Owns release-readiness after implementation and verification.

- Verify CI, CodeQL, dependency/action hygiene and architecture gates.
- Verify versioning/packaging decisions are explicit.
- Verify documentation matches the verified implementation.
- Verify no credentials, build artifacts or generated binaries are committed.
- Produce a concise release evidence summary; never infer production readiness from green CI alone.

## Collaboration contract

```text
USER / ISSUE / SCHEDULE
        ↓
ORCHESTRATOR
        ↓
RESEARCH_AGENT (when external evidence is relevant)
        ↓
FEATURE_ARCHITECT / domain specialists
        ↓
NETWORK_STRESS / SMTP_PROTOCOL / LOAD_ENGINE / NETWORK / METRICS / SCENARIO
        ↓
INTEGRATION_AGENT
        ↓
SECURITY_AGENT + EVIDENCE_AGENT
        ↓
TEST_AGENT
        ↓
CI + CodeQL + Architecture + Actions/Dependency + Release gates
        ↓
RELEASE_AGENT
        ↓
ORCHESTRATOR → next blocker / verified result
```

### Conflict resolution

1. Current `main` source and tests beat documentation.
2. Current source/test evidence beats historical audit text.
3. Security/authorization constraints cannot be overridden by a feature proposal.
4. Conflicting agent conclusions are recorded and sent back to `EVIDENCE_AGENT`/`INTEGRATION_AGENT`; no silent discard.
5. An agent may return `BLOCKED` or `NEEDS-EVIDENCE`; it must not invent a positive result.

## External stress-test research extracted for load2

The reviewed `slowhttptest` material demonstrates useful *test dimensions*: explicit connection count, connection rate, test duration, follow-up intervals, proxy routing, IPv6 behavior, response probing, output statistics, and handling of partial/slow I/O. Its documentation also exposes practical edge cases such as connection ceilings, 503 classification and large-request/header limits.

For load2 these are research inputs for bounded `NETWORK_STRESS_AGENT` scenarios and diagnostics, not attack-mode implementations.

The reviewed GoldenEye project demonstrates a simple separation of worker/socket concurrency and method selection, but the repository is archived and explicitly describes itself as an HTTP DoS test tool. Therefore it is `REFERENCE`, not an implementation source.

The linked historical gist is useful as observational evidence for server-side connection-limit/minimum-data-rate behavior; it is not an implementation source.

## Definition of done for agent collaboration

A task is not considered collaboratively complete merely because several role names exist. The work item must have:

- an identified owner (`ORCHESTRATOR`);
- explicit specialist handoffs where relevant;
- source-backed findings;
- a security/authorization decision;
- integration review;
- focused verification;
- CI evidence;
- documentation synchronization;
- a recorded next blocker or verified completion state.
