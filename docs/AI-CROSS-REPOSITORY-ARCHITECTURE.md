# Cross-repository AI architecture

**Status:** foundation added on main.

The goal is a shared autonomous engineering layer that can coordinate work across multiple repositories without giving the model a second unrestricted execution engine.

## Flow

Task
  |
  v
CrossRepositoryAiSupervisor
  |
  +--> repository registry / source-of-truth
  |
  +--> bounded repository actions
  |
  v
repository-specific agent
  |
  v
inspect -> plan -> implement -> test -> verify
  |
  v
evidence / handoff
  |
  v
replan

## Repository contract

Every participating repository is registered with:

- repository identity;
- source-of-truth ref;
- immutable commit SHA for the current task;
- explicitly allowed capabilities;
- explicitly allowed workspace scopes.

The registry is provider-neutral. It does not execute GitHub, shell, network, Docker, or repository commands.

## Execution boundary

The supervisor converts a task into bounded actions. External execution remains behind the repository-specific agent/runtime and its existing authorization, workspace-integrity, cancellation, resource-limit, and independent-verification gates.

Network access is not granted by this cross-repository layer.

## Evidence

AiRepositoryHandoff records the immutable commit, changed files, tests, findings, independent-verification state, and the next step. Tool success is not treated as correctness.

## Integration with load2

For load2, the existing AI SMTP path remains authoritative:

AI planner
  -> AiSupervisor
  -> AiActionGuard
  -> AiLoadTestExecutor
  -> SmtpTestRunner
  -> MailTestResult
  -> AiExecutionEvidence
  -> verification
  -> bounded replan

The cross-repository layer must not introduce another SMTP transport, pacing stack, retry stack, or authorization mechanism.

## Other repositories

Additional repositories can be registered through AiRepositoryDescriptor without embedding their names or credentials in the core. Each repository should supply its own agent and verification rules.

## Security-lab work

Security research can be represented as isolated, bounded tasks against explicitly controlled laboratory targets. C2/WebShell behavior, unrestricted flooding/DoS, credential collection, and guardrail bypass are not enabled by this orchestration contract.
