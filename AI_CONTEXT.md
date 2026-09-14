# AI_CONTEXT.md — READ THIS FIRST (MailLoadTester / load2)

## Purpose

Handoff document for AI coding assistants working on **MailLoadTester** — an authorized SMTP / email load-testing tool.

## Domain

- Primary protocol: SMTP / ESMTP (submission & relay paths).
- Focus: concurrent sessions, AUTH, STARTTLS, message injection, rate limiting, accurate metrics.
- Related IT-network concerns: TCP behaviour, timeouts, connection reuse, error taxonomy.
- The tool must remain strictly within authorized testing boundaries.

## Progressive engineering workflow

```text
TASK A — PRIMARY
Complete the explicitly requested change.

TASK B — DEEP ENGINEERING
Inspect the affected layers (SMTP state machine, engine, metrics, scenarios) and continue with the next non-blocked improvement.

TASK C — NEXT-BLOCKER DISCOVERY
Determine what concretely prevents the next usable capability.
```

## Capability ladder

```text
L0 DOCUMENTED
 ↓
L1 MODELED / MOCKED (mock SMTP)
 ↓
L2 PROTOCOL IMPLEMENTED
 ↓
L3 UNIT / INTEGRATION VERIFIED
 ↓
L4 CI VERIFIED
 ↓
L5 LOAD VERIFIED (controlled authorized target)
 ↓
L6 PRODUCTION OBSERVED (only with explicit authorization)
```

Never skip a level in documentation without evidence.

## Evidence labels

- `STATIC_ANALYSIS` / `UNIT_TESTED` / `CI_VERIFIED` / `LOAD_VERIFIED` / `PRODUCTION_OBSERVED`
- Metric confidence: `DETECTED` | `REPORTED` | `EXPECTED` | `UNKNOWN` | `HYPOTHESIS` | `SIMULATED`

Never turn a simulated or mock result into a production claim.

## Hard rules

- Authorized targets only.
- Never log full credentials or AUTH material.
- Prefer small, reviewable commits.
- Preserve existing verification gates.
- New features must go through the FEATURE_ARCHITECT 10-point format before large implementation.

## Repository integrity

An AI assistant with write access MUST NOT force-push or rewrite history unless explicitly instructed by the human maintainer for that specific action.

When reporting work, state previous/new HEAD (if known), files changed, what was actually tested, and remaining blockers or uncertainty.

## Definition of success

An operator can define SMTP scenarios, execute controlled concurrent load against authorized targets, obtain trustworthy per-command and session metrics, and clearly understand the confidence level of every claim — without hidden assumptions or suppressed findings.
