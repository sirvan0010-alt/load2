# SWE-agent integration notes

## Purpose

This document records the proposed integration boundary for using SWE-agent as an external coding-agent runtime. It is an integration design, not a vendored copy of SWE-agent.

## Upstream mechanism

The upstream SWE-agent source provides an agent abstraction with configurable models, tools, environment handling, hooks, trajectories and retry loops. These mechanisms cover the generic coding-agent execution loop that load2 should not duplicate.

## Load2 responsibilities

The load2 adapter must translate an immutable `AiAgentTask` into the runtime's problem/environment configuration and collect the runtime output into the existing load2 result contract.

The adapter must enforce, before execution:

- repository is exactly `sirvan0010-alt/load2`;
- commit is a full immutable SHA;
- allowed scopes are explicit;
- real-target authorization is explicit;
- time and iteration budgets are bounded;
- cancellation is connected;
- credentials are supplied only through the environment/approved secret mechanism.

After execution, load2 independently verifies changed files, tests, security properties and acceptance criteria. Runtime success alone is never promoted to `READY`/verified evidence.

## Do not copy

Do not copy the complete SWE-agent runtime, model implementations, tool implementations or environment implementation into the load2 .NET core. That would create a second agent platform and duplicate maintenance responsibilities.

If a small compatibility shim is required, keep it under an integration boundary and document the exact upstream revision used.

## Prototype acceptance test

A future prototype is accepted only when it can:

1. receive an immutable load2 task;
2. execute against that exact commit in an isolated workspace;
3. remain inside the declared tool/scope/time/iteration limits;
4. propagate cancellation;
5. produce redacted artifacts;
6. return changed files and test results;
7. pass the independent load2 verifier;
8. leave merge/release decisions to the existing repository gates.

Failure of any item means `BLOCKED` or `NEEDS-EVIDENCE`, not success.
