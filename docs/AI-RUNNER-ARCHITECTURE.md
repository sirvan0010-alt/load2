# AI Runner Architecture

## Purpose

`load2` uses a provider-neutral AI Runner boundary so model selection is replaceable without coupling the mail-load core to a vendor SDK. The current GitHub workflow remains a deterministic task factory and result verifier; it is **not** an LLM runtime.

## Execution pipeline

```text
TASK + immutable repository SHA
        |
        v
   AUTHORIZATION
        |
        v
     AI RUNNER
        |
        +--> allow-listed tools only
        |
        v
  SPECIALIST AGENT / MODEL ADAPTER
        |
        v
 RESULT JSON + evidence + handoff
        |
        v
 INDEPENDENT RESULT VERIFIER
        |
        +--> reject -> bounded iteration
        |
        v
 REVIEW -> TEST -> CI/CodeQL -> VERIFIED
```

## Non-negotiable invariants

1. The task records the exact repository SHA. Agents must not silently switch the source of truth.
2. A model/agent is never its own verifier.
3. Real-target execution requires explicit authorization and remains behind the existing safety controls, including `--unauthorized`, hard limits and cancellation.
4. Tools are allow-listed. Arbitrary shell execution is not part of the core runner contract.
5. Every run has a time budget and bounded iteration count.
6. Credentials and API keys come from the runtime environment/secret store and are never embedded in source or result artifacts.
7. `READY` means the result passed the external verifier; it does not mean production behavior is proven.
8. Evidence levels are monotonic: lower-level evidence cannot be silently promoted to a higher level.
9. PR creation may be automated later; merge/release remains outside the model's authority and must pass repository gates.
10. Cancellation must propagate through every async runner/model/tool boundary.

## Evidence ladder

The runner uses the following normalized levels:

`SOURCE_DOCUMENTED -> STATIC_ANALYSIS -> UNIT_TESTED -> CI_VERIFIED -> CONTROLLED_LOAD_VERIFIED -> PRODUCTION_OBSERVED`

A result should report the strongest level actually demonstrated, not the strongest level desired.

## Result contract

The existing `docs/AI-SWARM-RESULT-SCHEMA.json` remains the machine-readable result contract. The runner's internal model keeps evidence level metadata; serialization to the existing contract must not invent fields or claims.

## Initial implementation boundary

The first implementation adds only the orchestration boundary in `MailLoadTester.Core`:

- `AiAgentTask` — immutable task identity, scope, acceptance criteria and budgets.
- `IAiAgent` — model/backend adapter boundary.
- `IAiAgentToolExecutor` — explicit tool allow-list boundary.
- `IAiAgentAuthorizationPolicy` — authorization gate.
- `IAiAgentResultVerifier` — independent verification gate.
- `AiAgentRunner` — bounded, cancellation-aware orchestration loop.

No model API key, vendor SDK, GitHub token or autonomous merge capability is introduced by this layer.

## Next layer

The next implementation layer can add a concrete model adapter and repository/GitHub tools. It must preserve this boundary and must be opt-in. A provider outage or missing API credential must result in `BLOCKED`/`NEEDS-EVIDENCE`, never a fabricated success.
