# External repository deep-dives

**Authority:** `sirvan0010-alt/load2` / `main`

This directory contains one source-audited file per retained external repository.

## Audit rule

Do **not** classify a repository by its name (`bomber`, `flooder`, `scanner`, `POC`, etc.). Audit the actual source and classify individual mechanisms.

A repository can therefore contain mechanisms marked `ADOPT`, `ADAPT`, `HARDEN`, `SIMULATE`, `EXTRACT`, `REFERENCE` and `REJECT` at the same time.

The purpose is to transfer useful engineering capability into `load2` while preserving the existing authorization, scope, pacing, concurrency, cancellation and security controls.

## Required per-repository deep-dive

Every completed audit should document, where the source supports it:

1. repository URL, branch/revision and license/archive state;
2. exact entry points;
3. exact source file paths;
4. classes/functions/methods/symbols of interest;
5. configuration and inputs;
6. network protocols and external services;
7. queue/concurrency/threading model;
8. provider/endpoint discovery and selection;
9. rate limiting, retry, timeout and cancellation behavior;
10. payload/message generation;
11. target handling and scope model;
12. logging, persistence and reporting;
13. dependencies/runtime assumptions;
14. security-relevant behavior;
15. mechanism-by-mechanism mapping to `load2`;
16. decision tag and technical reason;
17. tests required before implementation;
18. evidence: commit/revision and date reviewed.

**README-only review is not a source-level audit.** If the implementation cannot be inspected, mark the affected conclusions `AUDIT PENDING` and do not present them as proven.

## Decision tags

| Tag | Meaning |
|---|---|
| **ADOPT** | Mechanism is suitable for direct implementation after mapping to `load2`. |
| **ADAPT** | Mechanism is useful but must be redesigned for `load2`. |
| **HARDEN** | Existing `load2` behavior should be improved using the external evidence. |
| **SIMULATE** | Implement a bounded/lab equivalent for testing or failure injection. |
| **EXTRACT** | Keep the architectural pattern without importing the original implementation. |
| **REFERENCE** | Retain for comparison/research only. |
| **REJECT** | Reject the individual mechanism because its primary purpose is theft, credential/token harvesting, CAPTCHA/OTP bypass, stealth/evasion, arbitrary public-target abuse discovery, unrestricted destructive DoS/DDoS/flooding, or defeating provider abuse controls. |

**REJECT is mechanism-level, not repository-level.** Offensive repositories must still be audited for useful transport, scheduling, orchestration, reliability, diagnostics and testing mechanisms.

## Template

```markdown
# <repo>

- URL:
- Revision reviewed:
- License / archived?:
- Entry points:
- Stack:
- Audit status: COMPLETE / PARTIAL / PENDING

## Mechanisms of interest
| Mechanism | File | Symbol | Inputs | Network behavior | Concurrency | Failure/retry | load2 mapping | Decision |

## Detailed findings

### <mechanism>
- What it does:
- Exact source evidence:
- Why it is useful:
- Current load2 equivalent/gap:
- Proposed implementation:
- Required tests:

## Do not import
- Mechanisms rejected by function, with technical reason.

## Evidence
- Revision/commit:
- Date reviewed:
- Files inspected:
```

Central index: `../EXTERNAL-REPO-TRANSFER-AUDIT.md`  
Gap matrix: `../LOAD2-GAP-MATRIX.md`

The implementation source of truth remains `sirvan0010-alt/load2/main`; external repositories are evidence/reference material only.
