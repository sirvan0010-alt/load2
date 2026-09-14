# load2 — External mechanism gap matrix

**Authority:** `sirvan0010-alt/load2` / `main`  
**Current baseline:** TRACK A A1–A8 **ALL COMPLETE**.

## 0. Interpretation rule

External repositories are audited **by mechanism**, not by project label. A repository named bomber, flooder, scanner, POC, stress tool or similar is not automatically rejected.

For each mechanism, distinguish reusable engineering from execution-specific behavior. Reusable scheduling, concurrency, pacing, retry, provider health, session lifecycle, diagnostics, connection lifecycle, timeout handling, protocol behavior, measurement and failure handling may be adopted/adapted when they improve load2 testing.

Decision tags:

`HAVE | GAP | ADOPT | ADAPT | HARDEN | EXTRACT | SIMULATE | REFERENCE | REJECT`

`REJECT` applies to a specific mechanism after technical/security review; it is not automatically applied to an entire repository because of its label or intended use.

## 1. TRACK A status

| Area | Current status |
|---|---|
| A1 destination-provider throttling | FIXED |
| A2 multi-account throttling regression | FIXED |
| A3 bounded scenario queue + metrics | FIXED |
| A4 retry policy + budget + RetryMetrics | FIXED |
| A5 unified SMTP outcome classification | FIXED |
| A6 global concurrency audit | PASS — no architecture change |
| A7 RunObservability / RunReport | FIXED |
| A8 final baseline/documentation | COMPLETE |
| FEAT-022 phase timing | IMPLEMENTED |

See `docs/IMPLEMENTATION-BACKLOG.md` and `docs/A8-BASELINE.md` for the authoritative baseline record.

## 2. Current capability map

| Mechanism | load2 status | Current evidence / treatment |
|---|---|---|
| bounded worker queue | HAVE | bounded `Channel<T>` + fixed workers |
| adaptive concurrency | HAVE | bounded by `MaxConcurrency` |
| actual-SEND pacing | HAVE | `SmartPaceController` / `AcquireSendSlotAsync` |
| per-recipient pacing | HAVE | existing recipient gate/limiter |
| destination-provider throttling | HAVE | SmartPace provider windows |
| SMTP session pool | HAVE | persistent `SmtpConnectionPool` |
| multi-account SMTP pools | HAVE | `SmtpAccountRegistry` / `SmtpAccountPoolHub` |
| endpoint health/quarantine | HAVE | `TransportHealthRegistry` |
| delivery ledger / AutoRestart | HAVE | `DeliveryLedger` |
| connection churn | HAVE | bounded scenario runner |
| scenario model / target sets | HAVE | `TargetSet` + `ScenarioLimits` |
| retry policy + retry budget | HAVE | `RetryPolicy` + `RetryMetrics` |
| unified SMTP outcome classification | HAVE | `SmtpOutcomeClassifier` + `OutcomeCounts` |
| queue metrics | HAVE | `ScenarioQueueMetrics` |
| run report | HAVE | `RunReportBuilder` |
| run observability projection | HAVE | `RunObservability` |
| FEAT-022 timing breakdown | HAVE | phase timing fields on result/report |
| DNS/MX/SPF/DMARC diagnostics | HAVE | read-only diagnostics layer where implemented |
| SMTP/TLS diagnostics | HAVE | `TransportDiagnostics` |
| provider/transport registry | HAVE / POST-BASELINE REFINED | `SmtpAccountRegistry` + `TransportHealthRegistry`; endpoint identity is now canonicalized before health selection |
| endpoint canonicalization/deduplication | HAVE | SMTP health keys canonicalize host/port/security; `TargetSet` canonicalizes domains before deduplication |
| replayable run artifacts | POST-BASELINE | extend only with concrete product requirement |
| failure injection | POST-BASELINE | controlled test scenarios |
| GUI summary binding | POST-BASELINE | use `RunObservability.FromReport` |
| live NET-AUDIT-001 | POST-BASELINE | test endpoints and fixtures selected by the operator |

## 3. Agent verification

Agent role definitions alone do not prove that an agent performed its task. The swarm now has a machine-readable result contract in `docs/AI-SWARM-RESULT-SCHEMA.json` and a deterministic verifier in `scripts/verify-agent-results.sh`.

A `READY` result requires findings/evidence, acceptance criteria with a passing item, verification tests and passing security constraints. `BLOCKED` and `NEEDS-EVIDENCE` remain valid states and cannot be silently upgraded to `READY`.

The current GitHub Actions workflow tests the verifier with both positive and negative fixtures. It does **not** claim to run an LLM. Real result verification becomes authoritative only when a future execution backend publishes actual agent results into the verifier pipeline.

## 4. Agent runtime Phase 1

The first runtime phase now has a concrete workspace-integrity boundary:

```text
AiAgentTask
  -> immutable commit
  -> workspace path
  -> GitWorkspaceIntegrityGate
  -> agent execution only when VERIFIED
```

The gate verifies:

- the workspace directory exists;
- Git can resolve the repository root;
- `HEAD` exactly matches the task's 40-character immutable SHA-1;
- the worktree is clean, including untracked files;
- cancellation is propagated;
- verification itself performs no checkout, fetch, reset, merge or other repository mutation.

`AiAgentRunner` invokes this gate **before** authorization-dependent agent execution. A failed workspace check returns `BLOCKED`; it cannot be converted to `READY` by the agent.

Persisted agent tasks now carry `workspacePath`, so a runtime cannot silently substitute an implicit working directory.

Phase 1 is therefore implemented at the contract/unit-test level. OS/container isolation and creation of the isolated workspace remain the next runtime boundary; they are intentionally not hidden inside the Git verification gate.

## 5. External audit

The retained external repositories are handled independently from TRACK A. Their useful mechanisms are recorded in `docs/external-repos/*.md` and summarized in `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`.

Audit method:

```text
source audit
  → mechanism inventory
  → load2 comparison
  → decision tag
  → focused implementation only when justified
  → tests
  → CI / CodeQL
  → documentation sync
```

README-only claims are not implementation evidence. Unknowns remain explicitly unknown.

## 6. Testing scope and execution governance

Load2 may evolve as a general security/load-testing framework. The gap matrix therefore evaluates capabilities by technical mechanism and test objective rather than imposing a blanket restriction based on the category or label of an external project.

High-intensity, distributed, destructive-impact or otherwise high-risk capabilities require explicit architectural review of target selection, authorization context, rate/concurrency controls, cancellation, observability, failure handling and operational safeguards before execution support is expanded.

Authorization is an execution concern and must not be confused with whether a mechanism is technically useful to the framework. Mechanisms can be researched, modeled, tested with fixtures and integrated where justified; deployment and target authorization remain outside the mechanism classification itself.

Do not weaken core engineering invariants merely to reproduce an external implementation. Preserve deterministic behavior, cancellation, diagnostics, testability and evidence even when evaluating aggressive or failure-oriented test mechanisms.

## 7. Rules for future work

- Do not reopen A1–A8 as unfinished without concrete regression evidence.
- Do not add a second pacing/limiting stack when the existing `SmartPaceController` covers the contract.
- Do not add a second queue when the bounded scenario `Channel` covers the contract.
- Retry paths must use the existing pacing/concurrency controls unless a documented architectural change supersedes them.
- Keep `MailTestResult` as the source of truth and `RunReport`/`RunObservability` as projections.
- Preserve cancellation, hard limits, DryRun/TestMode and `--unauthorized` behavior unless an explicit product/architecture change replaces the contract.
- Never introduce secrets into source, logs or reports.
