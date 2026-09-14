# AI Working Instructions

## Repository operating model

This repository is the source of truth for the MailLoadTester project. AI work must be evidence-first and repository-first.

### Before changing code
1. Inspect the current branch and relevant files.
2. Identify the smallest concrete change that satisfies the request.
3. Check existing tests and CI before introducing new infrastructure.
4. Do not assume a file, feature, or previous fix exists without verifying it.

### After changing code
1. Run the narrowest relevant test first.
2. Run the canonical CI-equivalent build/test when practical.
3. Check CodeQL/security results when the change affects code or workflows.
4. Check dependency/action changes separately from functional correctness.
5. Record unresolved findings instead of silently working around them.

## Agent roles
Use the specialist roles defined in `AI_AGENT_ARCHITECTURE.md`:

- Build / CI
- CodeQL Security
- Dependency / Actions Hygiene
- Architecture Consistency
- Test / Regression
- Evidence / Audit
- Release Gate

## Decision labels
- `CONFIRMED` — directly demonstrated by code, test, CI, or authoritative repository state.
- `OBSERVED` — directly observed but not necessarily proof of behavior in every environment.
- `DOCUMENTED` — supported by authoritative documentation.
- `INFERRED` — reasoned conclusion that is not directly proven.
- `UNKNOWN` — insufficient evidence.
- `BLOCKED` — required evidence or gate is missing/failing.

## Security rule
Security findings must never be hidden, deleted, downgraded, or ignored solely to obtain a green workflow. If a finding is accepted, document why and what scope it affects.

## Scope rule
Do not mix unrelated cleanup into a bug fix. If architecture or security work reveals a separate problem, record it separately unless the current change cannot safely proceed without it.

## Final response rule
When reporting work, state what changed, which files/workflows changed, what was actually tested, the exact CI/security status if available, and remaining blockers or uncertainty.

Never claim `green`, `secure`, `fixed`, or `verified` without corresponding evidence.
