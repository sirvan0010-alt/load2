# AI runtime reuse decision

## Decision

`load2` will **reuse an established coding-agent runtime** rather than implementing a general-purpose LLM coding runtime inside the .NET core.

The first integration prototype targets **mini-SWE-agent v2**. The upstream project is intentionally small and supports local/container-oriented execution. Its license is MIT.

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
            +--> environment allow-list
            +--> output redaction
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

`AiAgentRunner` and `IAiAgentModelAdapter` remain the load2 contract boundary. `GitWorkspaceIntegrityGate` verifies that an execution workspace is a real, clean Git worktree at the exact immutable SHA without mutating it. `IsolatedGitWorkspaceFactory` creates a separate local clone from an already verified source workspace, checks out the exact SHA in detached mode, and independently re-verifies the resulting workspace.

`MiniSweAgentRuntime` now propagates an explicit configuration path and model selection into the provider-neutral launch contract. `MiniSweAgentDockerConfiguration` provides a fail-closed environment-based image configuration; it does not read or forward credentials. `DockerMiniSweAgentSandbox` is the concrete execution boundary.

## Phase 1 status

```text
1.1 Workspace Integrity Gate       COMPLETE
1.2 Isolated Git Workspace         IMPLEMENTED + UNIT TESTS
1.3 Sandbox Boundary               IMPLEMENTED + UNIT TESTS
```

Phase 1.3 has an explicit Docker-backed execution boundary. It uses a dedicated container, a bind-mounted isolated workspace, a read-only container root filesystem, dropped Linux capabilities, `no-new-privileges`, PID/CPU/memory limits, a no-exec temporary filesystem and `--network none`. The Docker daemon/host remains a trusted prerequisite; this implementation does not claim to sandbox the host itself.

## Phase 2 status

```text
2.1 Provider-neutral launch contract   COMPLETE
2.2 Immutable SHA/scope propagation    COMPLETE
2.3 Bounded time/iteration contract    COMPLETE
2.4 Cancellation/process-tree contract COMPLETE
2.5 Environment allow-list             COMPLETE
2.6 Output secret redaction            COMPLETE
2.7 Runtime boundary tests             COMPLETE
2.8 External runtime execution         IMPLEMENTED + OPT-IN CI SMOKE TEST
```

Phase 2.8 now has a reproducible external-runtime path. `tools/mini-swe-agent/Dockerfile` pins the upstream mini-SWE-agent source to commit `04d809ceab9df28f9adaed044884180159172930`. The integration workflow builds that image, then executes the real mini-SWE-agent CLI through `DockerMiniSweAgentSandbox` using a deterministic test model, so no model-provider credentials or network access are required.

The smoke test is intentionally opt-in outside CI. It proves process/container execution, task/config propagation, network isolation, resource bounds, and the real external CLI boundary. It does **not** certify an arbitrary model, production workload, autonomous merge, or host isolation.

`ProcessMiniSweAgentSandbox` remains deliberately blocked by the capability gate because a normal host process is not an OS sandbox. `DockerMiniSweAgentSandbox` remains the approved execution boundary and hard-codes network isolation.

## Safety constraints

- Real-target work requires explicit authorization.
- No hardcoded credentials or tokens.
- The runtime process receives no inherited host environment; only explicitly supplied variables are passed.
- Credential-like environment keys are rejected by the Docker boundary.
- Secrets remain environment/configuration based and must be redacted from evidence.
- Cancellation and hard limits are mandatory.
- Non-target tasks default to network access denied.
- No automatic merge or release.
- Network/load testing remains governed by the existing authorization and safety gates.
