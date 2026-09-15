# Autonomous AI Coding Swarm — A1 to A10

Status: implementation baseline on a feature branch; `main` remains the source of truth until reviewed and merged.

## A1 — Execution backend contract

Implemented `IAgentExecutionBackend`, `AgentExecutionRequest`, `AgentExecutionResult` and bounded `AgentExecutionPolicy`.

## A2 — Provider-neutral routing

The swarm depends on `IAgentExecutionBackend`, not on SWE-agent or OpenHands directly. Existing `MiniSweAgentRuntime` remains compatible and can be adapted behind this contract.

## A3 — OpenHands backend

Implemented `OpenHandsAgentExecutionBackend` against the OpenHands Agent Server REST contract. The server URL, session key and model are supplied externally; no credentials are hardcoded.

## A4 — Isolated workspace boundary

Execution requests carry an explicit workspace and immutable commit. The existing sandbox capability checks remain mandatory. Production execution must use an isolated container/VM boundary; a plain host process is not considered a sandbox.

## A5 — Structured execution evidence

`AgentExecutionResult` carries status, task identity, commit, changed files, commands, evidence, diagnostics, test/security state, iteration count and duration. Subsequent adapters must populate these fields from observed execution data rather than inference.

## A6 — Bounded repair loop

The orchestrator must retry only within `MaxIterations` and `TimeBudget`. A failed run returns structured state; it does not create an unbounded loop.

## A7 — Multi-agent orchestration

The existing role registry and task factory remain authoritative. The execution backend is a pluggable worker layer for IMPLEMENTATION, REFACTOR, TEST and other specialist tasks.

## A8 — GitHub autonomous branch/PR flow

The target flow is: issue/ledger item → isolated workspace → agent execution → commit → CI → evidence/security/review gates → PR. Merge remains a protected human/release boundary.

## A9 — Scheduled maintenance

Only after A1-A8 are green: scheduled, budgeted maintenance tasks may be dispatched for dependency audits, test repair, documentation drift and other explicitly allow-listed work.

## A10 — Full autonomy gate

Full autonomy is declared only when the following are all verified by CI/integration evidence:

- real model-backed execution;
- isolated workspace/container;
- deterministic task contract;
- bounded runtime and iterations;
- structured evidence;
- repair loop;
- security and scope gates;
- tests and CodeQL;
- branch/PR creation;
- deterministic audit trail;
- no automatic protected-branch merge.

Until every item is verified, the system must describe itself as an autonomous-swarm foundation, not a fully autonomous coding swarm.

## Runtime architecture

```text
Research Ledger
      ↓
ORCHESTRATOR
      ↓
AiAgentTask / AgentExecutionRequest
      ↓
IAgentExecutionBackend
      ├── MiniSweAgentRuntime (existing adapter)
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
          repair or PR
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
