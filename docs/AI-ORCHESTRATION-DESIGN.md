# load2 — AI orchestration extension design

**Status:** PHASE-2B IMPLEMENTED — bounded contracts, registry, action guard and supervisor authorization are implemented in `src/MailLoadTester.Core/AiOrchestration.cs`; the AI model/planner integration remains a later phase.  
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
5. bounded configured planner + coordinator — COMPLETE;
6. bounded deterministic replan loop — COMPLETE;
7. connect model-backed supervisor planning — NEXT;
7. add replay/checkpoint only after deterministic execution works;
8. add focused tests and CI verification;
9. synchronize docs.

## Non-goals

- no C2/WebShell/exploitation framework;
- no unrestricted flooding/DDoS launcher;
- no bypass of authorization, pacing, concurrency or cancellation;
- no second SMTP transport implementation;
- no hardcoded credentials.


### Phase 2A implementation

The repository now contains `ConfiguredExecutionPlanner` and `AiExecutionCoordinator`. `ConfiguredExecutionPlanner` is deliberately deterministic: it derives its single bounded action from the already configured `MailTestOptions`. The coordinator performs planning followed by the existing authorization guard. It still does not execute SMTP operations; `SmtpTestRunner` remains the sole execution engine.


### Phase 2B implementation

`AiReplanContext`, `IAiReplanner` and `ConservativeAiReplanner` now provide a deterministic post-run replan boundary. The policy reacts only to observed failures/throttling/timeouts/circuit-breaker state and reduces concurrency; it never increases the configured message budget, concurrency, duration or target scope. `AiExecutionCoordinator.ReplanAsync` enforces a hard maximum of three replans and sends every generated plan back through `AiSupervisor` and `AiActionGuard`.

The replanner consumes the existing `MailTestResult`; no parallel telemetry model or second SMTP executor was introduced. A model-backed replanner can replace the deterministic policy later through `IAiReplanner`.

### Phase 2C — structured model-planner boundary

Phase 2C now adds a provider-neutral `IStructuredAiPlanProvider` and `StructuredAiExecutionPlanner`. The planner accepts only structured JSON, rejects unknown JSON fields, validates action kinds/bounds, and converts the response into the existing `ExecutionPlan` contract.

The planner itself does **not** authorize or execute the plan. The required path remains:

`model/provider → StructuredAiExecutionPlanner → ExecutionPlan.Validate → AiSupervisor → AiActionGuard → existing execution engine`

No model provider is hardcoded yet. Provider credentials and network transport will be introduced only behind the provider interface and configuration boundary.

### Phase 2C execution bridge

`AiLoadTestExecutor` is now the narrow execution boundary for AI-authorized load-test actions. It accepts only `LoadTest` actions, re-checks the configured message/concurrency bounds, re-applies `AuthorizationGate` immediately before network execution, and delegates to the existing `SmtpTestRunner`. No AI code calls MailKit directly and no second pacing, retry, queue, or SMTP transport was introduced.

### Phase 2C provider

`EnvironmentStructuredAiPlanProvider` adds the first model-facing boundary. `LOAD2_AI_PLAN_ENDPOINT` selects an HTTP(S) JSON endpoint and `LOAD2_AI_API_KEY` is optional bearer authentication. The provider serializes only a sanitized planning envelope; SMTP username/password, message body, headers and attachments are never sent to the AI endpoint. The endpoint response is consumed as structured `ExecutionPlan` JSON by `StructuredAiExecutionPlanner`, then authorization remains mandatory through `AiSupervisor`/`AiActionGuard` before `AiLoadTestExecutor` can reach `SmtpTestRunner`.
