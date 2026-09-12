# BUGS-AUDIT — MailLoadTester

## Reconciliation note (2026-09-12)

Historical Phase 1–7 narrative is retained below. Prefer this **evidence-backed** delta over older "NOT VERIFIED" lines for the same IDs.

| ID | Historical claim | Current evidence-backed status |
|----|------------------|--------------------------------|
| SEC-AUDIT-001 AUTH redaction | NOT VERIFIED | **IMPLEMENTED + EVIDENCE** — `ProtocolLogRedactionTests` |
| SEC-AUDIT-002 path boundary | NOT VERIFIED | **IMPLEMENTED + EVIDENCE** (basic `..`) — `PathBoundaryTests`; UNC/symlink still open |
| SEC-AUDIT-004 `--unauthorized` | NOT VERIFIED | **IMPLEMENTED + EVIDENCE** — `AuthorizationGateTests`, Validation, GUI MainForm, CI #106 |
| CONC-AUDIT-001 cross-component | NOT VERIFIED | **IMPLEMENTED + EVIDENCE** — `CrossComponentConcurrencyTests`, runner/pool/pace tests |
| CONC-AUDIT-002 cancellation | NOT VERIFIED | **IMPLEMENTED + EVIDENCE** — `SmtpTestRunnerTests` cancellation suite + limiter/pool |
| NET-AUDIT-002 TLS (offline) | NOT VERIFIED | **IMPLEMENTED + EVIDENCE** — `TlsMatrixTests` mapping/validation/DryRun |
| NET-AUDIT-001 live matrix | NOT VERIFIED | **NOT VERIFIED** — requires real endpoints |
| SEC-AUDIT-003 secret provenance | NOT VERIFIED | **NOT VERIFIED** — needs full-tree scan |

Full offline Phase H map: `docs/PHASE-H-EVIDENCE.md`.

Phases A–F core repairs, Phase I plugins, and Phase G SEC-001…004 are closed on `main` with green CI. BUG-001/002/… historical OPEN lines below may lag CI evidence (e.g. bounded workers and AutoRestart ledger tests already exist on main).

---

## Audit baseline

- Repository: `sirvan0010-alt/load2`
- Branch: `main`
- `load2` is the sole source of truth.
- `load` is secondary/parallel only.
- `Load-tester-` is legacy and must not be used for new implementation.
- Phase 1–7 source audit is complete; Phase 8 verification is still pending for items that require live network.
- A defect is marked fixed only with source evidence, caller review, regression coverage and verification evidence.
- The repair/audit roadmap is tracked as **9 top-level points**. Subtasks are explicitly numbered `1.1`, `1.2`, etc.

## 9-point repair and audit roadmap

1. **Execution-model refactor and verification** — BUG-001, BUG-003, BUG-007, BUG-009.
2. **Delivery correctness / auto-restart** — BUG-002; delivery ledger.
3. **Proxy rotation** — BUG-004.
4. **IPv4 rotation** — BUG-005.
5. **Repository integrity** — BUG-006.
6. **Direct MX routing hardening** — BUG-008.
7. **Security verification** — SEC-AUDIT-001..004.
8. **Concurrency/network/runtime verification** — CONC-AUDIT-001..002 and NET-AUDIT-001..002.
9. **Architecture and release verification** — plugins, Release build, tests.

## Confirmed / registered findings (historical narrative retained)

See prior sections in git history for full BUG-001…009 write-ups. Prefer CI + tests on `main` for current truth:

- Bounded workers / cancellation / AutoRestart skip-accepted: `SmtpTestRunnerTests`, `DeliveryLedgerTests`
- SmartPace first-send / spacing: `SmartPaceControllerTests`
- Proxy / IPv4 / IPv6 unit contracts: `ProxyRotatorTests`, `IpV4RotatorTests`, `IpV6RotatorTests`
- Direct MX single-domain: `DirectMxRoutingTests`
- Integrity placeholders: `RepositoryIntegrityTests`

## Investigation items — updated by reconciliation table above

Do not treat the older "NOT VERIFIED" paragraphs below as current status for SEC-001/002/004, CONC-001/002, or offline NET-002.

### SEC-AUDIT-003 — Secret provenance — NOT VERIFIED
Full-tree credential scan still required.

### NET-AUDIT-001 — Network interaction matrix — NOT VERIFIED
Live source-IP / proxy / IPv4/IPv6 matrix requires real endpoints.

## Phase 8

Remaining verification that needs external resources: NET-AUDIT-001, live TLS handshake, UNC/symlink paths, SEC-AUDIT-003. Offline regression is exercised continuously by GitHub Actions CI on `main`.
