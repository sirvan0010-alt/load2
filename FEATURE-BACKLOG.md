# MailLoadTester — FEATURE BACKLOG

> **SOURCE OF TRUTH: `sirvan0010-alt/load2`, branch `main`.**
>
> This file contains proposed improvements only. An entry here does not mean the feature already exists.

| ID | Priority | Area | Proposal | Status |
|---|---|---|---|---|
| FEAT-001 | High | Test orchestration | Bounded worker/channel execution model with explicit in-flight metrics. | PROPOSED |
| FEAT-002 | High | SMTP diagnostics | Per-message delivery-attempt ledger and unique-message accounting, including retry/restart visibility. | PROPOSED |
| FEAT-003 | High | Safety | Centralized authorization/scope guard for network operations, with `--unauthorized` required and dry-run isolation. | PROPOSED |
| FEAT-004 | Medium | DNS | Structured SPF/DKIM/DMARC diagnostics as read-only checks, with clear evidence and no modification. | PROPOSED |
| FEAT-005 | Medium | SMTP diagnostics | Open-relay verification as a bounded, explicit diagnostic against an authorized target. | PROPOSED |
| FEAT-006 | Medium | TLS | SMTP/TLS capability matrix and certificate diagnostics before a load run. | PROPOSED |
| FEAT-007 | Medium | Observability | Unified event/metrics model for pacing, concurrency, circuit state, SMTP response classes and connection-pool state. | PROPOSED |
| FEAT-008 | Medium | Profiles | Automatic profile validation, migration and safe round-trip checks. | PROPOSED |
| FEAT-009 | Low | UX | Preset modes that minimize configuration while keeping safety bounds explicit. | PROPOSED |
| FEAT-010 | Low | Testing | Automated stress/property tests for limiter, pool, pacing and cancellation state transitions. | PROPOSED |
| FEAT-011 | Low | Reporting | Exportable machine-readable run report with unique-message vs attempt metrics. | PROPOSED |

## Safety boundary

Features intended to increase unsolicited bulk delivery, flooding, mailbombing, DoS/DDoS or spam capability are not implementation targets. The project can instead provide bounded, authorized diagnostics, deterministic load tests and defensive SMTP/DNS/TLS checks.

## Selection rule

During the audit, add proposals when a concrete gap or architectural opportunity is found. Do not implement feature work until the corresponding correctness/security impact is understood and the repair backlog has been prioritized.
