# AI runtime reuse decision

## Decision

`load2` will **reuse an established coding-agent runtime** rather than implementing a general-purpose LLM coding runtime inside the .NET core.

The first integration prototype targets **mini-SWE-agent v2**. The upstream project explicitly recommends `mini-swe-agent` as the default choice for a quick, simple local coding-agent workflow and documents local/container-oriented execution. Its current license is MIT.

The load2 repository remains responsible for the domain-specific control plane: immutable task identity, authorization, hard limits, cancellation, tool/scope policy, evidence normalization, independent verification, tests, CI/CodeQL and the final merge/release boundary.

## Candidates reviewed

### mini-SWE-agent

Selected for the first prototype because it is intentionally small, provider-flexible and exposes a simple CLI/process boundary. Its v2 CLI accepts a task, configuration and model. The runtime can operate with local or containerized environments, while load2 keeps the security policy outside the runtime.

### SWE-agent

SWE-agent remains a valid alternative when load2 needs richer tool interfaces or history processing. Its mature agent/environment/model architecture confirms that generic coding-agent execution should not be recreated inside load2.

### OpenHands

OpenHands is another mature coding-agent platform and remains a viable future integration candidate. It is broader than required for the first load2 prototype, so it should remain an external runtime rather than being copied wholesale into load2.

### AutoGPT

AutoGPT is a broad autonomous-agent platform. It is useful as an orchestration reference, but it is not the preferred first substrate for the load2 coding-agent path because load2 primarily needs a controlled software-engineering runtime with explicit repository/tool execution boundaries.

## Selection rule

The selected runtime must satisfy these requirements with the least custom code:

1. immutable repository/commit workspace;
2. allow-listed shell/repository tools;
3. bounded execution and iteration budgets;
4. cancellation/timeout propagation;
5. model/provider neutrality;
6. deterministic artifact/trajectory output;
7. local or self-hosted execution;
8. no requirement for autonomous merge/release;
9. clear licensing suitable for the project;
10. safe integration with the load2 independent verification gate.

No runtime is considered production-ready merely because it can edit code. The adapter must be proven against these gates.

## Integration boundary

```text
external coding-agent runtime
            |
            v
     load2 runtime adapter
            |
            +--> immutable task / SHA
            +--> authorization + hard limits
            +--> allow-listed tools
            +--> cancellation
            v
      approved sandbox
            |
            v
       runtime execution
            |
            v
   redacted result/artifacts
            |
            v
 independent load2 verifier
            |
            v
       tests / CI / CodeQL
            |
            v
        VERIFIED
```

The runtime must never be its own verifier. A model claim is not evidence merely because the runtime reports success.

## Current implementation status

`AiAgentRunner` and `IAiAgentModelAdapter` remain the load2 contract boundary. `GitWorkspaceIntegrityGate` verifies that an execution workspace is a real, clean Git worktree at the exact immutable SHA without mutating it. `IsolatedGitWorkspaceFactory` now creates a separate local clone from an already verified source workspace, checks out the exact SHA in detached mode, and independently re-verifies the resulting workspace.

The isolation implementation is deliberately local-only: it does not fetch from remotes, and it does not claim OS-level sandboxing, filesystem confinement or network isolation. Those controls remain a separate sandbox responsibility.

## Phase 1 status

```text
1.1 Workspace Integrity Gate       COMPLETE
1.2 Isolated Git Workspace         IMPLEMENTED + UNIT TESTS
1.3 Sandbox Boundary               NEXT
```

Real runtime execution remains unverified until the sandbox boundary and independent execution evidence are implemented and pass CI.

The existing deterministic task-factory workflow remains the authoritative automation path.

## Safety constraints

- Real-target work requires explicit authorization.
- No hardcoded credentials or tokens.
- Secrets remain environment/configuration based and must be redacted from evidence.
- Cancellation and hard limits are mandatory.
- No automatic merge or release.
- Network/load testing remains governed by the existing authorization and safety gates.
