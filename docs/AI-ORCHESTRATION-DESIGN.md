# load2 — AI orchestration extension design

**Status:** PHASE-1 IMPLEMENTED — bounded contracts, registry, action guard and supervisor authorization are implemented in `src/MailLoadTester.Core/AiOrchestration.cs`; the AI model/planner integration remains a later phase.  
**Authority:** source + tests on `main`.

## Goal

Add autonomous AI orchestration without creating a second SMTP engine.

## Proposed boundaries

```text
AiSupervisor
   |
   +-- IExecutionPlanner
   |      +-- bounded ExecutionPlan
   |
   +-- IAgentRegistry
   |      +-- SmtpAgent
   |      +-- DnsAgent
   |      +-- TlsAgent
   |      +-- PayloadAgent
   |      +-- EvidenceAgent
   |
   +-- IAiActionGuard
   |      +-- authorization
   |      +-- target scope
   |      +-- hard limits
   |      +-- --unauthorized
   |      +-- DryRun/TestMode
   |
   +-- existing load2 execution engine
          +-- bounded Channel
          +-- MaxConcurrency
          +-- SmartPaceController
          +-- AcquireSendSlotAsync
          +-- persistent SMTP sessions
          +-- RetryPolicy
          +-- DeliveryLedger
          +-- RunReport / RunObservability
```

## Core contracts

The Phase-1 contracts below are implemented. `IExecutionPlanner` remains the integration boundary for a future model-backed planner; no model dependency is introduced by Phase 1.

```csharp
public interface IExecutionPlanner
{
    ValueTask<ExecutionPlan> CreatePlanAsync(
        AiTaskContext context,
        CancellationToken cancellationToken);
}

public interface IAiActionGuard
{
    ValueTask<AiActionDecision> ValidateAsync(
        AiAction action,
        AiTaskContext context,
        CancellationToken cancellationToken);
}

public interface IAgentRegistry
{
    IReadOnlyCollection<IMailLoadAgent> Agents { get; }
    IMailLoadAgent GetRequired(string id);
}
```

The final types must first be reconciled with the actual current source tree; do not add duplicate abstractions where existing interfaces already provide the contract.

## Execution invariant

The AI layer may choose **what bounded scenario to run**, but never **how to bypass** the engine.

```text
AI decision
  → guard
  → TargetSet / Scenario
  → existing worker/channel
  → existing pacing
  → existing SMTP transport
  → existing outcome/report pipeline
```

No AI path may call MailKit SEND directly.

## Plan constraints

A plan should be machine-validatable before execution:

- target scope is explicit;
- operation type is allowlisted;
- maximum count/duration/concurrency are bounded;
- authorization state is explicit;
- cancellation is supported;
- every action has an expected evidence/result type;
- destructive/unbounded operations are rejected;
- secrets are references, not prompt contents.

## Evidence envelope

Use a structured internal result rather than free-form AI text:

```csharp
public sealed record AiEvidence(
    string ActionId,
    string AgentId,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    string? Summary,
    IReadOnlyDictionary<string, object?> Data);
```

Before adding an evidence type, project existing `MailTestResult` / `RunReport` / `RunObservability` where possible. Evidence persistence is intentionally not part of Phase 1.

## Rollout order

1. source audit of current AI-related code — COMPLETE;
2. define/reuse action contracts — COMPLETE;
3. implement guard — COMPLETE;
4. implement specialist registry — COMPLETE;
5. bounded planner integration — NEXT;
6. connect model-backed supervisor planning — NEXT;
7. add replay/checkpoint only after deterministic execution works;
8. add focused tests and CI verification;
9. synchronize docs.

## Non-goals

- no C2/WebShell/exploitation framework;
- no unrestricted flooding/DDoS launcher;
- no bypass of authorization, pacing, concurrency or cancellation;
- no second SMTP transport implementation;
- no hardcoded credentials.
