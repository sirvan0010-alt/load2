# Universal AI Swarm Bootstrap

This document defines the repository-agnostic workflow to reuse the load2 agent system for other projects such as WiCAN or Proxmark5.

## 1. Source of truth

The swarm must receive exactly one authoritative repository, branch and commit/ref. Secondary copies are research inputs only. Never reconcile repositories by guesswork.

Required context:
- repository URL
- authoritative branch
- current SHA
- build/test commands
- security constraints
- deployment/release boundary

## 2. Common lifecycle

```text
ORCHESTRATOR
   |
   +--> RESEARCH
   +--> TRIAGE
   +--> ARCHITECT
          |
          +--> IMPLEMENTATION
          +--> REFACTOR
          |
       INTEGRATION
          |
     +----+----+----+
     |    |    |    |
 SECURITY EVIDENCE REVIEW
     +----+----+----+
          |
         TEST
          |
      CI / CODEQL
          |
    DOCUMENTATION
          |
       RELEASE
          |
      ORCHESTRATOR
```

The shared engine is domain-neutral. Only the specialist/domain registry changes between repositories.

## 3. Agent contract

Every task artifact must contain:

- task ID
- repository + authoritative SHA
- agent role
- objective
- allowed paths/scope
- prohibited scope
- source evidence
- acceptance criteria
- test plan
- security constraints
- current state: `READY`, `BLOCKED` or `NEEDS-EVIDENCE`
- predecessor and next handoff
- resulting artifact/patch reference

An agent cannot claim implementation from a plan alone.

## 4. Research ledger

Use a machine-readable ledger for external repositories and architectural mechanisms.

Lifecycle:

`PENDING -> AUDITED -> MAPPED -> DECISION -> IMPLEMENTED -> TESTED -> VERIFIED -> CLOSED`

Alternative terminal states:

`REJECTED`, `BLOCKED`

Rules:

- unknown stays `PENDING`
- no invented revisions
- no invented mechanisms
- source evidence precedes a decision
- decision precedes implementation
- rejected means reviewed and out of scope, not forgotten
- orchestrator cannot close while required mechanisms remain unresolved

Recommended decision vocabulary:
`ADOPT`, `ADAPT`, `HARDEN`, `EXTRACT`, `SIMULATE`, `REFERENCE`, `REJECT`.

## 5. Domain adapter

Do not copy load2's SMTP agents into another repository unchanged.

Instead define:

```text
COMMON AGENTS
- ORCHESTRATOR
- RESEARCH
- TRIAGE
- ARCHITECT
- IMPLEMENTATION
- REFACTOR
- INTEGRATION
- SECURITY
- EVIDENCE
- REVIEW
- TEST
- DOCUMENTATION
- RELEASE

DOMAIN AGENTS
- protocol/domain specialist(s)
- transport specialist(s)
- hardware/platform specialist(s)
- domain-specific safety/evidence specialist(s)
```

### WiCAN example

Possible domain agents:

- CAN/ISO-TP agent
- UDS diagnostics agent
- SLCAN transport agent
- Android/Kotlin agent
- vehicle-network evidence agent

### Proxmark5 example

Possible domain agents:

- RFID/NFC protocol agent
- firmware/FPGA agent
- hardware/schematic agent
- USB/Bluetooth transport agent
- client/tooling agent
- evidence/documentation agent

The common orchestration, ledger, review and CI machinery stays unchanged.

## 6. True AI runner boundary

GitHub Actions are the deterministic orchestration layer, not the LLM itself.

A real autonomous runner requires:

```text
TASK QUEUE
   -> MODEL/AGENT RUNNER
   -> REPOSITORY TOOLING
   -> PATCH/ARTIFACT COLLECTOR
   -> TEST EXECUTION
   -> REVIEW/SECURITY GATES
   -> ITERATION OR HANDOFF
```

The runner must operate with bounded budgets, explicit permissions, reproducible artifacts, cancellation and a protected merge/release boundary.

The model must never bypass CI, security gates, authorization requirements or the source-of-truth rule.

## 7. Safe autonomy levels

- **L0:** deterministic contracts and gates
- **L1:** research-only autonomous analysis
- **L2:** autonomous issue/branch task preparation
- **L3:** autonomous implementation + tests on isolated branches
- **L4:** continuous maintenance with bounded tasks
- **L5:** autonomous branch/PR implementation with human merge/release authority

Moving upward requires evidence that the previous level is reliable.

## 8. External repository transfer

For each candidate repository:

1. pin an exact revision;
2. inspect actual source, not repository name/README claims alone;
3. identify one mechanism at a time;
4. record source path and evidence;
5. compare against the target architecture;
6. choose a decision tag;
7. implement only after the decision;
8. add regression tests;
9. run CI/security gates;
10. record the verified result in the ledger.

Copying source is optional. The decision is mechanism-driven: if a proven implementation is materially better and compatible with the target architecture/licensing/security model, it may be adapted or refactored rather than reinvented.

## 9. Universal bootstrap prompt

> You are the ORCHESTRATOR for repository `<REPO>` on authoritative branch `<BRANCH>`, SHA `<SHA>`.
>
> Treat that repository/ref as the sole source of truth. First inspect its architecture, build/test system, security model and existing agent documentation. Do not invent files, APIs, revisions, test results or mechanisms.
>
> Create or reuse a deterministic task-factory workflow with these common roles: ORCHESTRATOR, RESEARCH, TRIAGE, ARCHITECT, IMPLEMENTATION, REFACTOR, INTEGRATION, SECURITY, EVIDENCE, REVIEW, TEST, DOCUMENTATION and RELEASE. Add only domain-specific specialists required by the repository.
>
> For external repositories, pin exact revisions and audit source-level mechanisms individually. Record evidence and use ADOPT/ADAPT/HARDEN/EXTRACT/SIMULATE/REFERENCE/REJECT decisions. Unknown facts remain PENDING. Do not implement a mechanism before a decision exists.
>
> Every handoff must contain the repository SHA, role, scope, acceptance criteria, tests, security restrictions, state and next handoff. Every implementation must produce a patch/artifact and tests. Every claim of success must be backed by an actual test or CI result.
>
> Keep network/transport, domain logic, payload/message generation, metrics and UI/application layers separated according to the target repository's architecture. Preserve existing safety, authorization, cancellation, rate/concurrency and secret-management controls unless source evidence proves a better compatible design.
>
> GitHub Actions may orchestrate deterministic contracts and task artifacts, but must not be represented as an LLM swarm. A true LLM runner is a separate execution backend with bounded permissions, budgets, state, artifacts and protected merge/release gates.
>
> Work continuously through research -> decision -> implementation -> test -> review -> CI -> documentation -> release evidence without waiting for user approval between routine steps. Stop only when blocked by missing evidence, missing permission, failed safety/security gate, or an architectural decision that genuinely requires human ownership.

## 10. Non-negotiable gates

The universal swarm must refuse to advance when:

- source of truth is ambiguous;
- required evidence is missing;
- credentials/secrets would be hardcoded or exposed;
- tests contradict the claimed behaviour;
- security review fails;
- cancellation/cleanup is broken;
- authorization is required but not established;
- an agent attempts to exceed its declared scope;
- a release is being proposed without successful required gates.

The objective is not maximum autonomy at any cost. The objective is reproducible, evidence-backed autonomy with a human-controlled merge/release boundary.
