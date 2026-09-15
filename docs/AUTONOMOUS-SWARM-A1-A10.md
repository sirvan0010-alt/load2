# Autonomous AI Coding Swarm — A1 to A10

Status: implementation continues on a feature branch; `main` remains the source of truth until reviewed and merged.

## A1 — Execution backend contract — IMPLEMENTED

Implemented `IAgentExecutionBackend`, `AgentExecutionRequest`, `AgentExecutionResult` and bounded `AgentExecutionPolicy`.

## A2 — Provider-neutral routing — IMPLEMENTED

The swarm depends on `IAgentExecutionBackend`, not on SWE-agent or OpenHands directly. Existing SWE-agent foundation remains compatible behind this contract.

## A3 — OpenHands backend — IMPLEMENTED BASELINE

`OpenHandsAgentExecutionBackend` targets the native OpenHands Agent Server REST contract. Server URL, session key and model are supplied externally; no credentials are hardcoded.

The adapter currently creates, polls and cleans up conversations. Structured event extraction is still an A5 prerequisite and is therefore intentionally enforced by the A10 gate.

## A4 — Isolated workspace boundary — FOUNDATION PRESENT

Execution requests carry an explicit workspace and immutable commit. The production invariant remains strict: a plain host process is not a sandbox. Real execution must be bound to the repository's existing isolated workspace/container capability before A10 can pass.

## A5 — Structured execution evidence — BLOCKING PREREQUISITE

`AgentExecutionResult` already carries status, task identity, commit, changed files, commands, evidence, diagnostics, test/security state, iteration count and duration.

However, the OpenHands adapter must populate these fields from observed Agent Server events rather than inference. The A10 gate explicitly checks for `/events/search` integration before full autonomy can be declared.

## A6 — Bounded repair loop — IMPLEMENTED

`AutonomousAgentLoop` now enforces a single total `TimeBudget` and `MaxIterations` across retries. Failed attempts produce bounded repair feedback for the next attempt. `Blocked`, `Cancelled` and `TimedOut` states stop immediately. There is no unbounded retry path.

## A7 — Multi-agent orchestration — IMPLEMENTED FOUNDATION

`AutonomousAgentOrchestrator` dispatches specialist tasks through the same bounded execution loop and stops at the first failed/blocked handoff. It does not introduce a second queue, pacing, retry or SMTP execution subsystem.

The existing role registry/task factory remains authoritative for role definitions.

## A8 — GitHub autonomous branch/PR flow — IMPLEMENTED FOUNDATION

`.github/workflows/autonomous-pr-gate.yml` verifies an agent result, runs repository tests, and creates a pull request from an agent branch into `main`. The workflow has no merge step; protected-branch merge remains outside the autonomous path.

## A9 — Scheduled maintenance — IMPLEMENTED FAIL-CLOSED DISPATCHER

`.github/workflows/autonomous-maintenance.yml` is scheduled weekly and manually runnable, but it is disabled unless repository variable `AUTONOMOUS_MAINTENANCE_ENABLED` is explicitly set to `true`. The dispatcher creates only an allow-listed maintenance task; it does not bypass the execution/evidence/security gates.

## A10 — Full autonomy gate — IMPLEMENTED, CURRENTLY BLOCKED

`scripts/verify-autonomous-swarm-gate.sh` and `.github/workflows/autonomous-gate.yml` enforce the final prerequisites.

Current blocking condition:

- A5 structured OpenHands event evidence is not yet wired into `OpenHandsAgentExecutionBackend`.

Therefore the project must still be described as an **autonomous-swarm foundation**, not a fully autonomous coding swarm.

A10 will pass only after all of the following are verified by CI/integration evidence:

- real model-backed execution;
- isolated workspace/container;
- deterministic task contract;
- bounded runtime and iterations;
- structured evidence from observed execution events;
- bounded repair loop;
- security and scope gates;
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
         OpenHands Agent Server
                ↓
        isolated workspace/container
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
