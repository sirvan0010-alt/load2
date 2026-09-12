# load2 — AI Guide (single entry point)

**Read this first.** Other docs are references, not parallel instruction sets.

**SOURCE OF TRUTH:** `sirvan0010-alt/load2` branch **`main`** — **source code + tests** win over any document.

```
README → docs/AI-GUIDE.md → source + tests → then detail docs if needed
```

## 1. What load2 is

Authorized **SMTP / email load-testing** framework. User supplies target(s) and scenario parameters. Core engine already includes:

- bounded workers, pacing (`AcquireSendSlotAsync`), adaptive concurrency, circuit breaker
- SMTP pool + health, proxy ban, IPv4/IPv6 rotation, DeliveryLedger / AutoRestart
- DryRun / TestMode / `--unauthorized`, PathSecurity, AUTH log redaction
- plugins (`IMailPayloadPlugin`), GUI, dashboard, FEAT-022 phase timings

The project may investigate mechanisms originating in spam, bomber, flood, DoS, DDoS, scanner, or stress tooling. The origin/name of a mechanism does **not** decide whether it is useful. The implementation and the intended execution path must be inspected first.

## 2. Dual work tracks (do not confuse)

| Track | Purpose | Status |
|-------|---------|--------|
| **A–F External audit** | Source-level audit of external repos → mechanism inventory → ADOPT/… → optional implementation | Ongoing (`docs/external-repos/`, EXT-AUDIT) |
| **Engine backlog** | Harden/extend current Core from gap matrix (FEAT-022 done, FEAT-HEALTH/REPORT/…) | Parallel, only after clear need |

FEAT-022 (timing breakdown) was **not** a replacement for phases A–F. It was one concrete gap from `LOAD2-GAP-MATRIX.md` while audit continues.

**Spam/Stress scenario modules are not implemented yet** — only audited/designed. Do not claim they exist in code.

## 3. How to continue work

1. `git pull` `main`; read this file.
2. Prefer **tests + source** over historical audit markdown (many `AUDIT-*.md` at repo root are archival).
3. If docs conflict with code → **fix code/tests**, mark doc discrepancy.
4. **Do not reconstruct project history** from multiple guides.
5. Never invent PASS/FIXED without commit SHA + green CI URL.
6. Small PRs: one mechanism or one fix → tests → CI.

## 4. External-repo audit (phases A–F)

Operational detail: `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md` (reference). Summary:

**A** Inventory · **B** Source-level symbol map · **C** Study mechanisms including aggressive ones · **D** Transfer only if load2 needs them · **E** Write `docs/external-repos/<name>.md` · **F** Update gap matrix / backlog.

Per mechanism decision:

`ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`

Separate **mechanism** (workers, pacing, retry taxonomy, session reuse…) from an **abuse path** (credential theft, CAPTCHA/OTP bypass, stealth/evasion, automated discovery of unrelated third-party infrastructure for mass abuse, or unrestricted destructive attack behavior). A useful mechanism can still be implemented when it is placed behind load2's explicit target/scope, authorization, pacing, concurrency, cancellation and observability controls.

Already source-audited examples: `Beast_Bomber.md`, `Bombers.md`.

## 5. Authorization / execution boundaries (always)

- Explicit user target + scenario
- `--unauthorized` / DryRun / TestMode as implemented
- Bounded concurrency, actual-SEND pacing, cancellation, ledger
- Do not weaken these controls merely to reproduce an external tool's behavior

### Stress test versus distributed attack

The project must distinguish the **test objective** from the **attack form**:

- **SMTP/application-layer load test:** repeated sends, rate tests, concurrency tests, connection churn, retry/failure stress, multi-recipient scenarios, and similar mechanisms may be implemented as explicit load2 scenarios when the target and scope are authorized and the existing execution controls remain in force.
- **Network transport stress test:** controlled testing of network transport capacity/behavior may be studied and, where technically relevant, represented through bounded lab/authorized scenarios. This is not the same thing as implementing a packet-flooding or reflection/amplification tool.
- **Distributed attack:** a real distributed attack against arbitrary third-party systems is not a load2 feature. Do not add an unrestricted DDoS launcher, botnet/orchestrator, reflection/amplification workflow, or equivalent public-target attack path.

The distinction is important: **DDoS is a distribution/attack model, not a synonym for every high-concurrency load test.** A controlled distributed test can be represented as multiple authorized load generators feeding a bounded scenario, with explicit scope, limits, cancellation and observability.

## 6. Current backlog (short)

| Priority | Item |
|----------|------|
| Audit | Continue EXT-AUDIT (next repos after Bombers, e.g. POC-bomber) |
| Engine | FEAT-HEALTH · FEAT-REPORT · FEAT-RUNID · FEAT-VERIFY |
| Done | FEAT-022 phase timings (`AvgPrepWaitMs` … `AvgSmtpSendMs`) |
| Deferred | Live NET matrix without fixtures |

## 7. Definition of Done

- Code on `main`
- Tests covering the contract
- Green CI (and CodeQL when it runs)
- Doc status matches **source**, not chat memory

## 8. Detail index (optional reading)

| Doc | Role |
|-----|------|
| `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md` | Full audit procedure |
| `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` | Repo table + transfer posture |
| `docs/LOAD2-GAP-MATRIX.md` | Mechanism vs load2 |
| `docs/external-repos/*.md` | Per-repo source audits |
| `FEATURE-BACKLOG.md` | Feature proposals |
| `PLAN.md` / `VERIFICATION.md` | High-level status (may lag; verify CI) |
| Root `AUDIT-*.md` | Historical; do not treat as current TODO |

Older: `docs/AI-PROJECT-GUIDE.md` → superseded by this file (kept as pointer only).
