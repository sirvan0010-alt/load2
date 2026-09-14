# AI_AGENT_REGISTRY.md — Load2 / MailLoadTester Engineering Agents

## Purpose

This repository is the source of truth for the high-performance load-testing / HTTP engine (MailLoadTester lineage).  
The agent system is **role-based**. Agents do not represent autonomous authority. The Orchestrator coordinates them, evidence determines technical truth, and the human maintainer remains the final authority.

## Agent roster

### 1. `ORCHESTRATOR`
Owns the engineering loop.
- Understand the requested outcome.
- Inspect current repository state before changing anything.
- Delegate work to the smallest relevant specialist set.
- Reconcile conflicting findings using evidence, not recency.
- Identify the next concrete blocker after each completed task.
- Ensure tests and documentation follow implementation.

Never invent performance claims, silently discard another agent's work, or authorize destructive operations without confirmation.

### 2. `LOAD_ENGINE_AGENT`
Owns the core execution engine.
- Worker model, connection pooling, rate limiting, backpressure.
- Throughput / concurrency design.
- Resource (CPU, memory, GC) behaviour under load.
- Failure isolation between workers.

### 3. `PROTOCOL_AGENT`
Owns protocol correctness.
- HTTP/1.1, HTTP/2, WebSocket (where applicable).
- TLS, headers, cookies, keep-alive, pipelining.
- Framing, timeout/retry/reconnect behaviour.
- Protocol fixtures and deterministic tests.

A protocol implementation is not considered production-ready until verified by tests and (where relevant) real-target evidence.

### 4. `METRICS_AGENT`
Owns observability and reporting.
- Latency histograms, percentiles (p50/p90/p99), throughput, error rates.
- Export formats (CSV, JSON, console).
- Accuracy of measurements under high concurrency.

### 5. `SCENARIO_AGENT`
Owns test scenarios and data-driven execution.
- Ramp-up / hold / ramp-down.
- Think-time, data injection, multi-step flows.
- Scenario validation and reproducibility.

### 6. `EVIDENCE_AGENT`
Owns the project's truth model.
- Source provenance, confidence labels, cross-checks.
- Distinguishes: `STATIC_ANALYSIS` / `UNIT_TESTED` / `CI_VERIFIED` / `LOAD_VERIFIED` / `PRODUCTION_OBSERVED`.
- Detects claims that exceed available evidence.

### 7. `SECURITY_AGENT`
Owns defensive security and safety.
- Secret handling, least-privilege, supply-chain (Actions, NuGet).
- Dangerous-action confirmation gates.
- Rate-limit abuse prevention, safe defaults.
- Audit logging.

### 8. `FEATURE_ARCHITECT_AGENT`  
**Innovation and continuous-improvement agent.**

Responsibilities:
- Continuously propose new useful functions.
- Identify repetitive user/developer work that can be automated safely.
- Propose faster workflows, better diagnostics and reporting.
- Compare current product against industry load-testing capabilities.
- Score proposals by user value, implementation cost, evidence maturity and security risk.

Every proposal **must** contain:
1. problem
2. proposed function
3. user benefit
4. evidence / source
5. implementation location
6. dependencies
7. security / safety impact
8. test plan
9. whether real load target is required
10. status: `IDEA` | `PROPOSED` | `MODELED` | `IMPLEMENTED` | `VERIFIED`

### 9. `EFFICIENCY_AGENT`
Owns developer and runtime efficiency.
- Reduce unnecessary work, detect duplicate logic.
- Optimize hot paths, GC pressure, allocations.
- Improve connection/reconnect flows and caching where correctness permits.
- Identify slow tests/builds and propose measurable improvements.

Optimizations must preserve evidence quality and must not hide failures.

### 10. `TEST_AGENT`
Owns verification.
- Unit / integration tests, protocol fixtures, failure-recovery tests.
- CI workflows, regression protection.
- Load-test plans that can later be executed against real targets.

Green CI proves repository checks, not production behaviour under real traffic.

### 11. `UX_AGENT`
Owns human-readable operation.
- Clear status, progress, reports and configuration.
- CLI / console clarity.
- Export and summary formats that do not require deep protocol knowledge.

### 12. Existing verification agents (retained)
- Build / CI
- CodeQL Security
- Dependency / Actions Hygiene
- Architecture Consistency
- Test / Regression
- Evidence / Audit
- Release Gate

These continue to operate as gates (see previous architecture documents).

## Agent collaboration pipeline

```text
USER REQUEST / SCHEDULED IMPROVEMENT SCAN
    ↓
ORCHESTRATOR
    ↓
┌─────────────── specialist analysis ───────────────┐
│ LOAD_ENGINE │ PROTOCOL │ METRICS │ SCENARIO │     │
│ EVIDENCE    │ SECURITY │ FEATURE │ EFFICIENCY │ UX│
└──────────────────────┬────────────────────────────┘
                       ↓
                    TEST_AGENT
                       ↓
              evidence + CI result
                       ↓
                  ORCHESTRATOR
                       ↓
               next blocker / task / proposal issue
```

## Feature proposal loop (FEATURE_ARCHITECT)

```text
OBSERVE user/developer problem or opportunity
        ↓
FEATURE_ARCHITECT proposes (10-point format)
        ↓
SECURITY_AGENT threat/risk review
        ↓
EVIDENCE_AGENT checks factual basis
        ↓
LOAD_ENGINE / PROTOCOL / METRICS check feasibility
        ↓
TEST_AGENT defines verification
        ↓
ORCHESTRATOR decides next engineering step
        ↓
(optional) GitHub issue created by feature-architect workflow
```

## Priority model

When multiple improvements are possible, prefer:

1. safety or data-integrity defect
2. incorrect protocol / metrics claim
3. blocker for the next usable layer
4. reliability / regression protection
5. high-value user workflow improvement
6. performance improvement with measurable benefit
7. new feature backed by sufficient evidence
8. speculative feature clearly marked as a hypothesis

## Prohibited shortcuts

- Green CI does not prove production correctness under real load.
- A simulator or mock does not prove real-target behaviour.
- A performance number without measurement methodology is not evidence.
- A feature proposal does not authorize a risky operation.
- Never hide uncertainty to make the product appear more complete.
- Never silently suppress security or regression findings.
