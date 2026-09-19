# Cross-repository AI architecture

**Status:** foundation + autonomous workspace gates implemented on main.

The goal is a shared autonomous engineering layer that can coordinate work across multiple repositories without giving the model a second unrestricted execution engine.

## Flow

Task -> CrossRepositoryAiSupervisor -> repository registry/source-of-truth -> bounded repository actions -> repository-specific agent -> inspect/plan/implement/test/verify -> evidence/handoff -> replan

## Autonomous task contract

`AiAgentTask` carries repository identity, immutable commit SHA, allowed workspace scopes, acceptance criteria, real-target authorization state, time budget, iteration budget, and workspace path.

`AiAgentAutonomyPolicy` rejects unauthorized real-target work and keeps automatic merge/release disabled.

## Workspace integrity

`GitWorkspaceIntegrityGate` verifies the workspace before an autonomous agent runs:

1. workspace exists;
2. expected commit is a valid SHA-1;
3. Git resolves the repository root;
4. `HEAD` exactly matches the immutable task commit;
5. the worktree is clean, including untracked files.

The gate uses `ProcessStartInfo.ArgumentList`; it does not invoke a shell. Cancellation is propagated to Git process operations.

## Change-scope protection

`GitChangeScopeGuard` inspects Git porcelain status and rejects changes outside the task's explicitly allowed scopes. Renames are checked using the new path.

## Execution boundary

The supervisor converts a task into bounded actions. External execution remains behind repository-specific authorization, workspace-integrity, cancellation, resource-limit, and independent-verification gates. Network access is not granted by this cross-repository layer.

## Evidence

`AiRepositoryHandoff` records the immutable commit, changed files, tests, findings, independent-verification state, and the next step. Tool success is not treated as correctness.

## Integration with load2

The existing AI SMTP path remains authoritative:

AI planner -> AiSupervisor -> AiActionGuard -> AiLoadTestExecutor -> SmtpTestRunner -> MailTestResult -> AiExecutionEvidence -> verification -> bounded replan

The cross-repository layer must not introduce another SMTP transport, pacing stack, retry stack, or authorization mechanism.

## Other repositories

Additional repositories can be registered through `AiRepositoryDescriptor` without embedding their names or credentials in the core. Each repository should supply its own agent and verification rules.

## Security-lab work

Security research can be represented as isolated, bounded tasks against explicitly controlled laboratory targets. C2/WebShell behavior, unrestricted flooding/DoS, credential collection, and guardrail bypass are not enabled by this orchestration contract.