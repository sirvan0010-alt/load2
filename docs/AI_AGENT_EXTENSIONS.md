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

### 18. `IMPLEMENTATION_AGENT`
Owns conversion of an approved, evidence-backed task into the smallest repository change.

- Implement only a task that has an explicit acceptance contract.
- Reuse existing pacing, queue, retry, metrics and reporting components rather than creating parallel systems.
- Keep the patch focused and reversible.
- Add or update focused tests with the implementation.
- Never implement a research hypothesis as a fact.
- Never bypass authorization, cancellation, hard limits or existing safety gates.

`IMPLEMENTATION_AGENT` may prepare code changes, but it does not decide whether a proposal is safe, evidenced or release-ready.

### 19. `BUG_TRIAGE_AGENT`
Owns defect decomposition and blocker identification.

- Reproduce or model the reported failure from repository evidence.
- Separate symptom, root cause, contributing factor and missing test coverage.
- Assign severity and the smallest useful fix boundary.
- Link the defect to existing architecture and lifecycle invariants.
- Return `BLOCKED` or `NEEDS-EVIDENCE` when reproduction/evidence is insufficient.

### 20. `REFACTOR_AGENT`
Owns safe structural improvement without changing externally intended behavior.

- Detect duplicated pacing, queue, retry, telemetry and lifecycle logic.
- Prefer extraction behind existing interfaces over parallel abstractions.
- Require characterization/regression tests before risky movement.
- Measure or document any expected runtime/build benefit.
- Preserve error semantics, cancellation and observability.

### 21. `DOCUMENTATION_AGENT`
Owns synchronization between implementation, evidence and operator documentation.

- Update docs only from current source/test/CI evidence.
- Preserve provenance and distinguish `SOURCE`, `TESTED`, `CI_VERIFIED`, `LOAD_VERIFIED` and `PRODUCTION_OBSERVED` claims.
- Remove stale claims when implementation changes.
- Keep research conclusions separate from adopted implementation behavior.
- Never upgrade an evidence state merely because documentation was updated.

### 22. `REVIEW_AGENT`
Owns adversarial pre-merge review of proposed changes.

- Check architecture boundaries and unintended duplicate systems.
- Look for missing cancellation, cleanup, error propagation and observability.
- Check evidence claims against actual tests and pinned sources.
- Check security/authorization assumptions.
- Produce actionable findings, not a superficial approval.

## Collaboration contract

```text
USER / ISSUE / SCHEDULE
        ↓
ORCHESTRATOR
        ↓
RESEARCH_AGENT / BUG_TRIAGE_AGENT
        ↓
FEATURE_ARCHITECT / domain specialists
        ↓
IMPLEMENTATION_AGENT / REFACTOR_AGENT
        ↓
INTEGRATION_AGENT
        ↓
SECURITY_AGENT + EVIDENCE_AGENT + REVIEW_AGENT
        ↓
TEST_AGENT
        ↓
CI + CodeQL + Architecture + Actions/Dependency + Release gates
        ↓
DOCUMENTATION_AGENT
        ↓
RELEASE_AGENT
        ↓
ORCHESTRATOR → next blocker / verified result
```

## Collaboration artifact contract

Each implementation task should be representable as a deterministic handoff containing:

- task id and requested outcome;
- owner and specialist roles;
- current source-of-truth revision;
- problem statement;
- evidence and provenance;
- acceptance criteria;
- files/components in scope;
- forbidden boundaries / safety constraints;
- focused test plan;
- required gates;
- resulting status and next blocker.

The repository workflow may generate these artifacts automatically. Such artifacts are **agent coordination contracts**, not proof that an LLM performed the work.

## Important distinction: workflow agents vs AI agents

GitHub Actions can enforce the contract, run deterministic analysis, generate handoffs and execute tests. They do not become an autonomous reasoning model merely because a job is named `AGENT`.

A future LLM runner can consume the handoff artifact and return a patch/proposal artifact. Until a model provider and explicit credentials are configured, the workflow must remain deterministic and must not pretend that a model reviewed or implemented code.

## Conflict resolution

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
