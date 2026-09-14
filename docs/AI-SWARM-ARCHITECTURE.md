# AI Swarm Architecture — load2

**Status:** Phase 0 foundation (deterministic gates + Research Ledger).  
**Goal:** Progress toward an autonomous multi-agent swarm that can research, propose, implement, verify and iterate under explicit governance.

## 1. Current reality vs target

| Layer | Current state | Target state |
|-------|---------------|--------------|
| Role definitions | `AI_AGENT_REGISTRY.md` + `AI_AGENT_EXTENSIONS.md` | Same + machine-readable capability contracts |
| Collaboration | Deterministic handoffs via `agent-collaboration.yml` | Orchestrator selects next work from ledger |
| Research state | Research Ledger | Ledger + evidence collector + scheduler |
| Execution | Human + AI assistants in chat / PR | Bounded execution backend invoking specialists |
| Autonomy | None (gates only) | Graduated autonomy levels with protected merge/release boundary |

Current GitHub Actions do **not** run intelligent agents. They validate contracts and generate deterministic handoff artifacts. That is intentional Phase 0.

## 2. Autonomy levels and execution governance

1. **L0 — Deterministic gates** (current)  
   Workflows validate documents, roles and ledger integrity. No autonomous implementation.
2. **L1 — Research autonomy**  
   System may inspect external sources, compare mechanisms and update research state with evidence.
3. **L2 — Issue / branch autonomy**  
   System may select DECISION items and prepare isolated implementation work. Merge remains protected.
4. **L3 — Implementation + test autonomy**  
   Specialists implement on isolated branches, run tests, push and open PRs. Merge remains gated.
5. **L4 — Continuous maintenance**  
   Scheduled loops operate within explicit budgets, stop conditions and protected release boundaries.

### Common governance contract

The architecture is **capability-neutral**: a mechanism is not accepted or rejected solely because an external project calls it flooding, bombing, stress testing, scanning or another security term. Each capability must be evaluated by mechanism, intended execution context and evidence.

Higher-risk execution requires explicit scope/authorization, bounded resource and target parameters, cancellation, observability, testability, evidence capture and security review. The presence of a governance gate does not by itself prove that a capability is implemented.

The following remain mandatory across the engine and agent layers:

- `main` is authoritative;
- `--unauthorized` remains a required explicit execution control;
- pacing, concurrency and hard limits are enforced through the existing execution pipeline;
- cancellation and deterministic shutdown are preserved;
- credentials and secrets are not hardcoded or emitted into logs/reports;
- no specialist may bypass the integration, evidence, security or test gates.

## 3. Research Ledger lifecycle

Every mechanism follows:

```text
PENDING → AUDITED → MAPPED → DECISION → IMPLEMENTED → TESTED → VERIFIED → CLOSED
                                              ↘ REJECTED
                                              ↘ BLOCKED
```

`RESEARCH_AGENT` owns ledger completeness. `ORCHESTRATOR` cannot close a work item while required mechanism evidence or verification is missing. Unknown source facts stay `PENDING`. No invented revisions or mechanisms.

Decision tags: `ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`.

## 4. Agent collaboration target

```text
ORCHESTRATOR
    ├─ RESEARCH_AGENT
    ├─ FEATURE_ARCHITECT_AGENT
    ├─ DOMAIN AGENTS (SMTP, LOAD_ENGINE, NETWORK, SCENARIO, …)
    ├─ NETWORK_STRESS_AGENT
    ├─ INTEGRATION_AGENT
    ├─ SECURITY_AGENT + EVIDENCE_AGENT
    ├─ TEST_AGENT
    └─ RELEASE_AGENT
```

Cross-cutting: `METRICS_AGENT`, `EFFICIENCY_AGENT`, `UX_AGENT`.

## 5. Autonomous execution backend still required

GitHub Actions are the gate/automation substrate, not the reasoning engine. A true swarm requires:

- task scheduler backed by the Research Ledger;
- model/tool execution adapter;
- structured specialist result schema;
- artifact/evidence collector;
- retry and iteration state;
- bounded execution budgets;
- protected write/merge boundary;
- deterministic audit trail.

Phase 1 should implement these pieces incrementally rather than pretending the current workflow is already autonomous.

## 6. Phase roadmap

| Phase | Deliverable |
|-------|-------------|
| **0 (now)** | Research Ledger schema + integrity gate + deterministic collaboration contract |
| **1** | Machine-readable capability/task contracts + autonomous research scheduler |
| **1b–1d** | Result collector, verification loop, structured specialist outputs |
| **2** | Execution backend invoking specialists and collecting evidence |
| **3** | Full specialist loop with evidence/security/test gates |
| **4** | Continuous maintenance loop with explicit stop conditions |
| **5** | Autonomous implementation on branches + PR; protected human/release boundary |

## 7. What Phase 0 does not claim

- No autonomous code changes.
- No claim that TRACK A closure means project completion.
- No claim that an external repository is authoritative for load2 implementation.
- No claim that governance text alone makes a capability safe or implemented.

## 8. Source-of-truth order

1. `main` source + tests
2. Green CI / CodeQL evidence
3. `docs/RESEARCH-LEDGER.json` for research state
4. `AI_AGENT_REGISTRY.md` + `AI_AGENT_EXTENSIONS.md`
5. Architecture and audit documentation
6. Chat/session notes

## 9. Next concrete steps

1. Stabilize PR #6 and its gates.
2. Keep the ledger complete at repository level and source-backed at mechanism level.
3. Pin exact revisions and extract mechanisms from slowhttptest / GoldenEye.
4. Reconcile external mechanisms against the current load2 execution pipeline before implementation.
5. Only after Phase 0 is green, implement the Phase 1 research scheduler and execution backend.
