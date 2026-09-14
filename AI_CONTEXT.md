# AI_CONTEXT.md — READ THIS FIRST (Load2 / MailLoadTester)

## Purpose

This file is the handoff document for future AI coding assistants working on the load-testing engine.

The project delivers a high-performance, evidence-driven HTTP / protocol load tester with clear metrics, scenarios and safety boundaries.

## Progressive engineering workflow

For every development request:

```text
TASK A — PRIMARY
Complete the explicitly requested change.

TASK B — DEEP ENGINEERING
Inspect the affected layers and continue with the next non-blocked engineering improvement.

TASK C — NEXT-BLOCKER DISCOVERY
Determine what concretely prevents the next layer from becoming usable.
```

Do not stop merely because Task A produced one file. Inspect callers, consumers, tests, documentation and the next dependent layer.

## Capability ladder

```text
L0 DOCUMENTED
 ↓
L1 MODELED / MOCKED
 ↓
L2 PROTOCOL IMPLEMENTED
 ↓
L3 HOST / UNIT VERIFIED
 ↓
L4 CI VERIFIED
 ↓
L5 LOAD VERIFIED (controlled target)
 ↓
L6 PRODUCTION OBSERVED
```

Never skip a level in documentation without evidence.

## Evidence and truth rules

Clearly distinguish:

- `STATIC_ANALYSIS`
- `UNIT_TESTED`
- `CI_VERIFIED`
- `LOAD_VERIFIED`
- `PRODUCTION_OBSERVED`

Diagnostic / metric values use:

- `DETECTED` — direct reliable measurement
- `REPORTED` — reported by the system under test
- `EXPECTED` — design expectation
- `UNKNOWN` — not safely determinable
- `HYPOTHESIS` — proposed but unverified
- `SIMULATED` — model behaviour only

Never turn a simulated result into a production claim.

## Current project stage

High-quality CI, CodeQL, dependency and architecture gates are in place.  
Core engine and metrics continue to be strengthened. Real-target load verification remains an explicit later stage.

## Architecture rules (summary)

- Keep Core logic independent of any particular UI or reporting front-end.
- Prefer small, reviewable commits.
- Preserve existing verification agents and gates.
- New features must be proposed through the FEATURE_ARCHITECT 10-point format before large implementation.

## Repository integrity rule

An AI assistant with write access MUST NOT force-push, rewrite history, or replace repository state unless explicitly instructed by the human maintainer for that specific action.

Before modifying an existing file, inspect its current contents and preserve unrelated changes.

When reporting work, state:
- previous HEAD (if known)
- new HEAD
- files changed
- what was actually tested
- remaining blockers or uncertainty

## Definition of success

The project succeeds when a user can define scenarios, execute controlled load, obtain trustworthy metrics and reports, and understand the confidence level of every claim — without hidden assumptions or suppressed findings.
