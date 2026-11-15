# Documentation map

This index separates current engineering guidance from historical audit records.

## Start here

- [User guide](USER_GUIDE.md) — installation, first safe run, tabs, settings, Capacity Run, IMAP monitor and charts.
- [Results and metrics](RESULTS_AND_METRICS.md) — SMTP Accepted vs mailbox storage, quotas, MIME accounting and graph interpretation.
- [Safety and privacy](SAFETY_AND_PRIVACY.md) — authorization, limits, credentials, logs, reports and webhook handling.
- [Troubleshooting](TROUBLESHOOTING.md) — SMTP, TLS, authentication, 4xx/5xx, Capacity Run and IMAP issues.
- [GitHub Actions setup](GITHUB_ACTIONS_SETUP.md) — repository permissions, security features, required checks and importable main ruleset.
- [Build and release guide](../README-BUILD.md) — Windows build, publish and installer commands.
- [Development guide](DEVELOPMENT.md) — architecture, local validation and contribution workflow.
- [AI guide](AI-GUIDE.md) — instructions for AI-assisted repository work.
- [Maintenance contract](../README-MAINTENANCE.md) — invariants for generated content and attachment safety.

## Current technical design

- [A3 queue metrics](A4-APPLY.md) and [A4 retry metrics](A5-APPLY.md) — implementation notes and invariants.
- [A6 concurrency audit](A6-CONCURRENCY-AUDIT.md) — worker, queue and concurrency design.
- [A7 observability](A7-OBSERVABILITY.md) — run metrics and reporting model.
- [A8 baseline](A8-BASELINE.md) — performance baseline and measurement notes.
- [Security secret provenance](SEC-003-SECRET-PROVENANCE.md) — credential flow and redaction constraints.
- [Implementation backlog](IMPLEMENTATION-BACKLOG.md) — planned and in-progress engineering work.

## Historical records

Root-level `AUDIT-*`, `BUGS-AUDIT.md`, `DEEP-AUDIT-*`, and numbered `AUDIT-FIXES-*` files are historical snapshots. They explain why changes were made at a particular point in time; they are not automatically a description of the current implementation.

The recovery snapshot in [archive](archive/README.md) is historical only. It is not compiled and must never be copied over the current GUI source.

## Source-of-truth rules

1. Current source code and tests define actual behavior.
2. `VERSION` is the release version source of truth; `AppVersion.Current` and user-facing build documentation must match it.
3. Security and safety invariants in current code/tests take precedence over older audit notes.
4. When a feature changes, update the relevant current design page and this index. Do not create another dated audit file as a substitute for updating current documentation.
