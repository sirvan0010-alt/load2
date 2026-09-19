# load2 — AI orchestration extension design

**Status:** PHASE-2C IMPLEMENTED + analysis-informed evidence/verification

This design adopts only the transferable engineering ideas from OBLITERATUS: staged pipelines, observable intermediate artifacts, analysis-informed adaptation, explicit verification, and reproducible metadata. OBLITERATUS itself is a model-abliteration/refusal-removal research system; its weight-editing or guardrail-removal mechanisms are not part of load2.
 — bounded contracts, registry, action guard and supervisor authorization are implemented in `src/MailLoadTester.Core/AiOrchestration.cs`; the AI model/planner integration remains a later phase.  
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