# Autonomous Improvement Loop

The load2 autonomous development runtime is bounded and evidence-first.

## Execution

`Task Factory -> Loader -> Workspace Integrity -> Orchestrator -> Research -> Architecture -> Implementation -> Test -> Security -> Evidence -> Independent Verification -> PR`

A failed verification may start another bounded repair iteration. Every iteration is limited by both `MaxIterations` and `TimeBudget`.

## Immutable safety boundaries

The loop cannot mutate the task's repository, immutable commit, allowed scopes, authorization state, or network policy. Real-target work requires explicit authorization. Network-disabled execution is the default for non-target work.

Credentials are not discovered, inherited from the host, or emitted into evidence. Automatic merge and release are disabled.

## Evidence

Each phase records task ID, immutable commit, iteration, phase, timestamp, success, summary, and findings. Evidence is observational; it does not replace independent verification.

## Model independence

The model is supplied through `IAiAgentModelAdapter`. Provider/model selection therefore remains outside the core orchestration contract.

## Completion states

- `Completed`: independent verifier accepted the bounded execution result.
- `NeedsEvidence`: execution or verification requires another repair/evidence cycle.
- `Blocked`: an authorization, integrity, capability, or policy gate rejected execution.

A completed autonomous run is a PR handoff, not an automatic merge or release.
