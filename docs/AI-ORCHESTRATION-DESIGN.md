# load2 — AI orchestration extension design

**Status:** POST-BASELINE design derived from the CyberStrikeAI audit.  
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

These are design contracts, not yet implementation claims.

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

Before implementation, verify whether an existing load2 result type should be projected instead of adding this type.

## Rollout order

1. source audit of current AI-related code;
2. define/reuse action and evidence contracts;
3. implement guard;
4. implement specialist registry;
5. implement bounded planner;
6. connect supervisor;
7. add replay/checkpoint only after deterministic execution works;
8. add focused tests and CI verification;
9. synchronize docs.

## Non-goals

- no C2/WebShell/exploitation framework;
- no unrestricted flooding/DDoS launcher;
- no bypass of authorization, pacing, concurrency or cancellation;
- no second SMTP transport implementation;
- no hardcoded credentials.
