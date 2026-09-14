# AI runtime reuse decision

## Decision

`load2` will **reuse an established coding-agent runtime** rather than implementing a general-purpose LLM coding runtime inside the .NET core.

The load2 repository remains responsible for the domain-specific control plane: immutable task identity, authorization, hard limits, cancellation, tool/scope policy, evidence normalization, independent verification, tests, CI/CodeQL and the final merge/release boundary.

## Candidates reviewed

### SWE-agent

SWE-agent provides a mature agent loop around an environment, model, tools, trajectories and configurable retry/review behavior. Its implementation exposes model configuration, tool configuration, environment handling, action parsing, hooks and retry loops. This is substantially more functionality than load2 should recreate.

Evidence checked from the upstream `SWE-agent/SWE-agent` source at review time includes `AbstractAgent`, model configuration, tool configuration, environment setup, hooks, trajectories and retry-agent support.

### OpenHands

OpenHands is another mature coding-agent platform and remains a viable integration candidate. It is broader than a small embedded library, so integration should be treated as an external runtime boundary rather than copied wholesale into load2.

### AutoGPT

AutoGPT is a broad autonomous-agent platform. It is useful as an orchestration reference, but it is not the preferred first substrate for the load2 coding-agent path because load2 primarily needs a controlled software-engineering runtime with explicit repository/tool execution boundaries.

## Selection rule

The first implementation target should be the runtime that can satisfy all of these requirements with the least custom code:

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

No runtime is considered selected merely because it can edit code. The adapter must be proven against these gates.

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

`AiAgentRunner` and `IAiAgentModelAdapter` are retained temporarily as the load2 contract boundary. They are not treated as a reason to build a complete custom coding-agent runtime. The next implementation step is an external-runtime adapter/prototype and its verifier tests.

Until that adapter is proven, the existing deterministic task-factory workflow remains the authoritative automation path.

## Safety constraints

- Real-target work requires explicit authorization.
- No hardcoded credentials or tokens.
- Secrets remain environment/configuration based and must be redacted from evidence.
- Cancellation and hard limits are mandatory.
- No automatic merge or release.
- Network/load testing remains governed by the existing authorization and safety gates.
