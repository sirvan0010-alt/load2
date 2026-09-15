# Autonomous AI Coding Swarm — A1 to A10

Status: implementation continues on a feature branch; `main` remains the source of truth until reviewed and merged.

## A1 — Execution backend contract — IMPLEMENTED

Implemented `IAgentExecutionBackend`, `AgentExecutionRequest`, `AgentExecutionResult` and bounded `AgentExecutionPolicy`.

## A2 — Provider-neutral routing — IMPLEMENTED

The swarm depends on `IAgentExecutionBackend`, not on SWE-agent or OpenHands directly. Existing SWE-agent foundation remains compatible behind this contract.

## A3 — OpenHands backend — IMPLEMENTED BASELINE

`OpenHandsAgentExecutionBackend` targets the native OpenHands Agent Server REST contract. Server URL, session key and model are supplied externally; no credentials are hardcoded.

The adapter creates, polls and cleans up conversations and now reads the bounded OpenHands event-search API for structured execution evidence.

## A4 — Isolated workspace boundary — FOUNDATION PRESENT / A10 BLOCKER

Execution requests carry an explicit workspace and immutable commit. The repository already contains a Docker-backed isolated agent sandbox, but the OpenHands adapter is not yet bound to that sandbox/runtime boundary.

A plain host `working_dir` is deliberately not treated as a sandbox. A10 therefore remains blocked until OpenHands execution is proven to run inside the isolated workspace/container boundary.

## A5 — Structured execution evidence — IMPLEMENTED BASELINE

The OpenHands adapter now consumes `GET /api/conversations/{id}/events/search` with bounded pagination and extracts observed commands, file actions, exit codes, diagnostics, observed commit identifiers and test-command success evidence. Sensitive command text is redacted before being returned.

The adapter does not infer security approval from the final natural-language response. `SecurityPassed` remains false until the separate security gate supplies authoritative evidence.

## A6 — Bounded repair loop — IMPLEMENTED

`AutonomousAgentLoop` now enforces a single total `TimeBudget` and `MaxIterations` across retries. Failed attempts produce bounded repair feedback for the next attempt. Policy-controlled evidence, test, security and changed-file limits are evaluated on every attempt. `Blocked`, `Cancelled` and `TimedOut` states stop immediately.

## A7 — Multi-agent orchestration — IMPLEMENTED FOUNDATION

`AutonomousAgentOrchestrator` dispatches specialist tasks through the same bounded execution loop and stops at the first failed/blocked handoff. It does not introduce a second queue, pacing, retry or SMTP execution subsystem.

The existing role registry/task factory remains authoritative for role definitions.

## A8 — GitHub autonomous branch/PR flow — IMPLEMENTED FOUNDATION

`.github/workflows/autonomous-pr-gate.yml` verifies an agent result, restores/tests the repository, and creates a pull request from an agent branch into `main`. The workflow has no merge step; protected-branch merge remains outside the autonomous path.

## A9 — Scheduled maintenance — IMPLEMENTED FAIL-CLOSED DISPATCHER

`.github/workflows/autonomous-maintenance.yml` is scheduled weekly and manually runnable, but it is disabled unless repository variable `AUTONOMOUS_MAINTENANCE_ENABLED` is explicitly set to `true`. The dispatcher creates only an allow-listed maintenance task; it does not bypass the execution/evidence/security gates.

## A10 — Full autonomy gate — IMPLEMENTED, CURRENTLY BLOCKED

`scripts/verify-autonomous-swarm-gate.sh` and `.github/workflows/autonomous-gate.yml` enforce the final prerequisites.

Current blocking condition:

- A4: OpenHands execution is not yet proven to be attached to an isolated Docker/VM/Kubernetes workspace boundary.
- Security evidence is intentionally not inferred from model output; the authoritative security gate must be wired to the OpenHands execution result before A10 can pass.

Therefore the project must still be described as an **autonomous-swarm foundation**, not a fully autonomous coding swarm.

A10 will pass only after all of the following are verified by CI/integration evidence:

- real model-backed execution;
- isolated workspace/container;
- deterministic task contract;
- bounded runtime and iterations;
- structured evidence from observed execution events;
- bounded repair loop;
- authoritative security and scope gates;
- tests and CodeQL;
- branch/PR creation;
- deterministic audit trail;
- no automatic protected-branch merge.

## Runtime architecture

```text
Research Ledger
      ↓
ORCHESTRATOR
      ↓
AiAgentTask / AgentExecutionRequest
      ↓
IAgentExecutionBackend
      ├── existing SWE-agent-compatible adapter
      └── OpenHandsAgentExecutionBackend
                ↓
         isolated runtime boundary
                ↓
         OpenHands Agent Server
                ↓
             code + tests
                ↓
      evidence / security / CI
                ↓
          bounded repair
                ↓
        branch / protected PR
```

## Security invariants

- `main` is authoritative.
- `--unauthorized` remains required for live execution paths.
- No hardcoded credentials.
- Network access is denied unless the task policy explicitly allows it.
- Agent work is constrained to an isolated workspace and allowed scopes.
- Time, iteration and changed-file budgets are mandatory.
- No agent may bypass security, evidence, review or test gates.
- Automatic protected-branch merge is not part of the autonomous loop.
