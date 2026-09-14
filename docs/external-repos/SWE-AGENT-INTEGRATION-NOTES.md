# Coding-agent runtime integration notes

## Decision

`load2` should reuse the current **mini-SWE-agent v2** runtime as the first external coding-agent prototype rather than implementing a general-purpose coding-agent loop in the .NET core.

The upstream project documents `mini` as its default choice for a quick local coding-agent workflow and provides local/container-oriented environments. Its v2 CLI accepts a task, configuration and model, and supports cancellation with Ctrl+C. The project is MIT licensed.

## Load2 responsibilities

The load2 adapter remains the security and evidence control plane. Before execution it must enforce:

- repository is exactly `sirvan0010-alt/load2`;
- commit is a full immutable SHA;
- allowed scopes are explicit;
- real-target authorization is explicit;
- time and iteration budgets are bounded;
- cancellation is connected;
- credentials are supplied only through an explicitly approved environment/secret mechanism;
- the runtime is launched through an approved sandbox boundary.

The prototype adapter therefore does **not** execute `mini` directly from the .NET core. It receives an `IMiniSweAgentSandbox` implementation responsible for process isolation, filesystem policy, network policy and environment allow-listing.

## Runtime boundary

```text
AiAgentTask
    |
    v
MiniSweAgentRuntime
    |
    +--> immutable SHA / scope / limits
    +--> cancellation
    |
    v
IMiniSweAgentSandbox
    |
    v
mini-SWE-agent v2
    |
    v
redacted runtime result
    |
    v
load2 independent verifier
```

This keeps the generic coding-agent loop outside load2 and prevents the core library from accidentally becoming an unrestricted shell executor.

## Prototype implementation

`src/MailLoadTester.Core/MiniSweAgentRuntime.cs` provides:

- normalized workspace validation;
- immutable task information in the runtime prompt;
- explicit time budget propagation;
- an injectable sandbox boundary;
- a process-backed sandbox implementation for controlled environments;
- environment injection only from an explicit allow-list supplied by the caller;
- process-tree termination on cancellation/timeout.

The process implementation intentionally does **not** claim to provide OS-level sandboxing or network isolation. Those properties belong to the host/container sandbox.

## Acceptance gates

The prototype is not considered production-ready until tests and the independent verifier establish:

1. exact immutable commit checkout;
2. isolated workspace;
3. declared tool/scope limits;
4. bounded time/iteration execution;
5. cancellation propagation;
6. no unintended secret exposure;
7. redacted artifacts;
8. changed-file and test evidence;
9. independent load2 verification;
10. no automatic merge or release.

Until all gates pass, runtime execution remains `BLOCKED` or `NEEDS-EVIDENCE`.

## Do not copy

Do not vendor the mini-SWE-agent source, model implementation or sandbox implementation into the load2 .NET core. Keep the dependency at a process/integration boundary and record the upstream revision used for each validated prototype.
