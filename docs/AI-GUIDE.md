# load2 — AI Guide (single entry point)

**Read this first.** Other documents are references, not parallel instruction sets.

**MASTER CARD:** GitHub Issue **#4 — MASTER CARD — load2 plan, SMTP limits & sender reputation runbook**.

**SOURCE OF TRUTH:** `sirvan0010-alt/load2`, branch **`main`**. Source code + tests take precedence over every document, including this guide and the Master Card.

```text
README → docs/AI-GUIDE.md → MASTER CARD (#4) → source + tests → detail docs
```

## 1. Current baseline

`TRACK A` is **CLOSED**. `docs/IMPLEMENTATION-BACKLOG.md` and `docs/A8-BASELINE.md` record **A1 → A8 ALL COMPLETE**.

| ID | Status |
|---|---|
| A1 destination-provider throttling | ✅ FIXED |
| A2 multi-account throttling regression | ✅ FIXED |
| A3 bounded scenario queue + metrics | ✅ FIXED |
| A4 retry policy + budget + RetryMetrics | ✅ FIXED |
| A5 unified SMTP outcome classification | ✅ FIXED |
| A6 global concurrency audit | ✅ PASS — no architecture change |
| A7 RunObservability / RunReport projection | ✅ FIXED |
| A8 final baseline + documentation sync | ✅ COMPLETE |

Do **not** reopen A4–A8 because of stale historical notes. A new regression requires concrete source/test/CI evidence and a separate post-baseline item.

## 2. Engine invariants

- One pacing system: `SmartPaceController`.
- Bounded `Channel` + bounded workers governed by `MaxConcurrency`.
- `AcquireSendSlotAsync` gates every real SEND, including retries.
- `DeliveryLedger` keeps `Accepted` terminal across AutoRestart.
- `RetryPolicy` uses the unified `SmtpOutcomeClassifier` and global retry budget.
- One outcome taxonomy feeds retry, health, counters and reporting.
- `MailTestResult` is the source of truth; `RunReport` and `RunObservability` are projections.
- Persistent SMTP sessions remain the normal transport model.
- `CancellationToken`, DryRun/TestMode, `--unauthorized`, hard limits and secret redaction remain mandatory.

## 3. How to continue work

1. Inspect current `main` before changing anything.
2. Prefer source + tests + current CI over historical audit documents.
3. Do not invent a feature or status without evidence.
4. Do not duplicate existing retry/histogram/observability work.
5. Make the smallest compatible change, add focused tests, then verify CI/CodeQL when applicable.
6. Synchronize documentation after verified implementation changes.

## 4. External-repository audit

External repositories are audited **mechanism-by-mechanism**, regardless of whether their names describe bombers, flooders, scanners, stress tools or other aggressive tooling.

Decision tags:

`ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`

A useful engineering mechanism may be transferred into an authorized load2 scenario. The repository label itself is never the reason for rejection.

Separate reusable mechanisms (workers, queues, pacing, retry taxonomy, session reuse, provider health, diagnostics) from abuse-specific paths such as credential theft, CAPTCHA/OTP bypass, stealth/evasion, arbitrary public-target discovery for abuse, or unrestricted destructive traffic.

The detailed procedure is `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md`; the central mapping is `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` and `docs/LOAD2-GAP-MATRIX.md`.

## 5. Authorization and execution boundary

Load2 is an authorized SMTP/email load-testing framework. Controlled repeated sends, rate tests, concurrency tests, connection churn, retry/failure stress, multi-recipient scenarios and other legitimate test mechanisms may be represented in the existing bounded execution model.

Network-layer stress may be studied in controlled/lab/explicitly authorized infrastructure. A distributed test can be legitimate when all load generators and targets are explicitly authorized and bounded.

Do not add an unrestricted public-target flooding tool, botnet/DDoS launcher, reflection/amplification workflow, provider-abuse bypass, credential theft or equivalent destructive path. Do not weaken authorization, pacing, concurrency, cancellation or observability to reproduce an external tool.

## 6. Post-baseline work

TRACK A is closed. New work is post-baseline and must not be presented as unfinished TRACK A work.

Current optional directions:

1. GUI summary panel backed by `RunObservability.FromReport`.
2. `NET-AUDIT-001` against explicitly authorized test endpoints.
3. External-repository ADOPT/ADAPT candidates, independently of TRACK A.
4. Release packaging/versioning as a product decision.

## 7. Evidence rule

A status of `FIXED`, `PASS`, `COMPLETE` or `VERIFIED` requires corresponding source/test/CI evidence. Historical audit files may document what happened, but they are not current TODO lists unless explicitly marked as such.

The final TRACK A baseline currently recorded by `docs/A8-BASELINE.md` is the authoritative completion record for A1–A8.

## 8. Deprecated guide

`docs/AI-PROJECT-GUIDE.md` is a compatibility pointer only. Do not maintain parallel instructions there.
