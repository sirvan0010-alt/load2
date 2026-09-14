# AI Swarm Architecture — load2

**Status:** Phase 0 foundation (deterministic gates + Research Ledger).  
**Goal:** Progress toward an autonomous multi-agent swarm that can research, propose, implement, verify and iterate under explicit governance.

## 1. Current reality vs target

| Layer | Current state | Target state |
|-------|---------------|--------------|
| Role definitions | `AI_AGENT_REGISTRY.md` + `AI_AGENT_EXTENSIONS.md` | Same + machine-readable capability contracts |
| Collaboration | Deterministic handoffs via `agent-collaboration.yml` | Orchestrator selects next work from ledger |
| Research state | Human/docs scattered | `docs/RESEARCH-LEDGER.json` as source of research truth |
| Execution | Human + AI assistants in chat / PR | Bounded execution backend invoking specialists |
| Autonomy | None (gates only) | Graduated autonomy levels with human merge boundary |

Current GitHub Actions **do not** run intelligent agents. They validate contracts and generate handoff artifacts. That is intentional Phase 0.

## 2. Autonomy levels (governance)

1. **L0 — Deterministic gates** (current)  
   Workflows check documents, roles, ledger schema. No autonomous decisions.

2. **L1 — Research-only autonomy**  
   System may audit external sources, fill ledger entries as PENDING/AUDITED, open research issues. No code changes.

3. **L2 — Issue / branch autonomy**  
   System may create issues and feature branches from ledger DECISION items. No merge.

4. **L3 — Implementation + test autonomy**  
   Specialists implement on isolated branches, run tests, push, open PRs. Merge remains human-gated.

5. **L4 — Continuous maintenance**  
   Scheduled loops with hard stop conditions, still protected `main`/release.

**Protected boundaries at every level:**  
- `main` remains authoritative.  
- Authorization, pacing, concurrency, cancellation, hard limits, `--unauthorized`, secret redaction remain mandatory.  
- No unrestricted public-target flooding, reflection/amplification, provider-abuse bypass, stealth/evasion for abuse, or DDoS launchers.

## 3. Research Ledger lifecycle

Every mechanism follows:

```text
PENDING → AUDITED → MAPPED → DECISION → IMPLEMENTED → TESTED → VERIFIED → CLOSED
                                              ↘ REJECTED
                                              ↘ BLOCKED
```

- `RESEARCH_AGENT` owns ledger completeness.
- `ORCHESTRATOR` must not mark a research work item complete while any required mechanism remains open (`PENDING`/`AUDITED`/`MAPPED` without decision, or `IMPLEMENTED` without tests).
- Decision tags: `ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`.
- Unknown source facts stay `PENDING`. No invented revisions or mechanisms.

## 4. Agent collaboration (target pipeline)

```text
ORCHESTRATOR
    ├─ RESEARCH_AGENT          (ledger, external audits)
    ├─ FEATURE_ARCHITECT_AGENT (proposals from ledger gaps)
    ├─ DOMAIN AGENTS           (SMTP, LOAD_ENGINE, NETWORK, SCENARIO, …)
    ├─ NETWORK_STRESS_AGENT    (bounded network dimensions)
    ├─ INTEGRATION_AGENT       (cross-layer conflicts, duplicates)
    ├─ SECURITY_AGENT + EVIDENCE_AGENT
    ├─ TEST_AGENT
    └─ RELEASE_AGENT           (CI, CodeQL, packaging evidence)
```

Cross-cutting: `METRICS_AGENT`, `EFFICIENCY_AGENT`, `UX_AGENT`.

## 5. Phase roadmap toward autonomous swarm

| Phase | Deliverable |
|-------|-------------|
| **0 (now)** | Research Ledger schema + Ledger Gate in Actions + this architecture doc |
| **1** | Machine-readable capability/task contracts; autonomous research scheduler (select next ledger item) |
| **1b–1d** | Result collector, verification loop, structured specialist outputs |
| **2** | Execution backend (invoke specialists, collect evidence, iterate until VERIFIED/BLOCKED) |
| **3** | Full specialist loop with evidence gates |
| **4** | Continuous maintenance loop with stop conditions |
| **5** | Autonomous implementation on branches + PR; human merge/release |

## 6. What Phase 0 does **not** claim

- No autonomous code changes.
- No claim that TRACK A closed equals project complete.
- No claim that Feature Architect replaces the historical agreed capability list.
- No weakening of the authorized-use / anti-abuse boundary.

## 7. Source of truth order

1. `main` source + tests  
2. Green CI / CodeQL evidence  
3. `docs/RESEARCH-LEDGER.json` (research state)  
4. `AI_AGENT_REGISTRY.md` + `AI_AGENT_EXTENSIONS.md`  
5. This architecture document  
6. Chat / session notes

## 8. Next concrete steps

1. Merge or complete PR #6 (collaboration roles + contract workflow).  
2. Keep Research Ledger populated only with source-backed data.  
3. Extract mechanisms from slowhttptest / GoldenEye into ledger entries (PENDING → AUDITED → …).  
4. Only after ledger + gates are stable, design the Phase 1 scheduler and execution backend.
