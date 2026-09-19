# CyberStrikeAI — mechanism transfer audit

## Status
- Audit: COMPLETE
- Repository: `AIPentest/CyberStrikeAI`
- Revision inspected: `4d1854e7ea06f5da18c9b0387517840a32f50f48` (main)
- load2 authority remains `sirvan0010-alt/load2/main`
- Decision: ADAPT / EXTRACT / REFERENCE
- Scope: orchestration and governance mechanisms only

## Source map

Relevant CyberStrikeAI source/documentation inspected:
- `docs/en-US/agent-and-role-guide.md`
- `docs/en-US/MULTI_AGENT_EINO.md`
- `docs/en-US/security-model.md`
- `docs/en-US/workflow-graph.md`
- `internal/agents/markdown.go`
- `internal/multiagent/orchestrator_instruction.go`
- `internal/multiagent/runner.go`
- `internal/multiagent/eino_middleware.go`
- `internal/multiagent/eino_skills.go`

The repository documents three orchestration modes: Deep, Plan-Execute and Supervisor. Agent definitions can be separated into orchestrator Markdown files and specialist sub-agents. Roles define identity/tool boundaries, while Skills provide reusable procedures and are loaded progressively.

## Mechanisms

| Mechanism | Evidence | load2 mapping | Decision |
|---|---|---|---|
| Plan → Execute → Replan | `docs/en-US/agent-and-role-guide.md`, `MULTI_AGENT_EINO.md` | New AI orchestration layer above existing bounded execution engine | ADAPT |
| Supervisor specialist routing | `internal/multiagent/runner.go`, orchestrator instruction resolver | SMTP/DNS/TLS/Payload/Evidence specialist agents | ADAPT |
| Dedicated orchestrator definitions | `internal/agents/markdown.go` | Keep agent definitions separate from SMTP transport implementation | ADAPT |
| Role/tool boundary | `agent-and-role-guide.md`, `security-model.md` | Tool/action guard before network operations | ADAPT |
| Progressive Skills | `MULTI_AGENT_EINO.md`, `eino_skills.go` | SMTP/TLS/DNS diagnostic knowledge as on-demand skills | ADAPT |
| Structured sub-agent output | `agent-and-role-guide.md` | Evidence envelope feeding RunReport/RunObservability | ADAPT |
| HITL checkpoint | `workflow-graph.md`, security model | Preserve --unauthorized and explicit authorization boundary | ADAPT |
| Graph workflow | `workflow-graph.md` | Future scenario composition; do not replace existing Channel/worker pipeline | REFERENCE |
| Tool-call guard / blocking | README and security documentation | Validate authorization, target, limits and operation before real SEND | ADAPT |
| Checkpoint/resume middleware | `MULTI_AGENT_EINO.md` | Candidate for replayable/redacted run artifacts | EXTRACT |
| MCP/C2/WebShell/security-tool execution | repository scope | Outside load2 SMTP scope | REJECT |

## Execution trace relevant to load2

CyberStrikeAI's reusable conceptual path is:

`intent → orchestrator → specialist/skill → governed tool call → structured result/evidence → orchestrator → next step`

The load2 adaptation must remain:

`intent → AI plan/supervisor → policy guard → existing TargetSet/Scenario → bounded Channel/workers → existing pacing/concurrency → SMTP session/pool → outcome classification → DeliveryLedger/RunReport/RunObservability`

The AI layer must never create a parallel sender, queue, pacing system or retry system.

## load2 gap assessment

The current load2 AI guide establishes a source-of-truth and external-repository audit process, while the documented engine baseline already provides bounded execution, pacing, persistent SMTP sessions, retry classification, delivery ledger and observability.

What is not established by the inspected load2 documentation is a verified C# implementation of:
1. a first-class AI Supervisor;
2. a Plan/Execute/Replan contract;
3. specialist-agent registry;
4. a pre-action policy/authorization guard abstraction;
5. structured AI evidence envelopes;
6. replay/checkpoint artifacts for autonomous AI runs.

These are therefore POST-BASELINE design/implementation candidates, not claims that the current engine lacks the underlying execution controls.

## Security boundary

Do not import CyberStrikeAI capabilities for C2, WebShell, credential theft, exploitation, stealth/evasion, arbitrary public-target discovery or unrestricted destructive traffic.

For load2, the guard must preserve:
- explicit authorization;
- `--unauthorized` behavior;
- hard target/count/duration limits;
- cancellation;
- existing pacing and concurrency;
- secret redaction;
- DryRun/TestMode;
- observable/auditable results.

## Tests required before implementation is considered complete

- supervisor chooses a specialist without bypassing the engine;
- plan contains bounded operations only;
- every real SEND passes the guard and existing `AcquireSendSlotAsync` path;
- cancellation propagates through planning and execution;
- failed specialist execution produces structured evidence;
- replay cannot duplicate an already terminal `DeliveryLedger.Accepted` item;
- no credentials appear in AI context, evidence or reports;
- `--unauthorized` remains enforced;
- CI and CodeQL pass.

## Conclusion

CyberStrikeAI is useful as an architectural reference for autonomous orchestration and governance. The transferable value is the separation of orchestrator, specialist agents, skills, policy/approval and evidence. It should not replace or duplicate load2's existing SMTP execution engine.

Recommended next implementation unit: a small C# `IAiActionGuard` + specialist registry + Plan/Execute contract that wraps the existing execution pipeline, followed by focused tests.
