# load2 — Documentation Map and Authority

The repository contains a large historical audit trail. It must not become a collection of competing plans.

## Authority hierarchy

1. **`sirvan0010-alt/load2` / `main` source code + tests** — product source of truth.
2. **Verified CI / security evidence** — proves implementation status.
3. **Canonical documents below** — current engineering/product description.
4. **Historical audit material** — evidence only; never current authority by itself.

A working branch may contain proposed work. A proposed feature becomes part of the authoritative baseline only after merge to `main` and verification.

## Canonical documents

| Document | Role | Status |
|---|---|---|
| `docs/AI-GUIDE.md` | single entry point for engineering/AI work | ACTIVE |
| `docs/DOCUMENTATION-MAP.md` | authority and document routing | ACTIVE |
| `docs/LOAD2-ROADMAP.md` | current product direction and priorities | ACTIVE |
| `docs/LOAD2-GAP-MATRIX.md` | current capability/gap status | ACTIVE |
| `docs/IMPLEMENTATION-BACKLOG.md` | executable backlog | ACTIVE |
| `docs/A8-BASELINE.md` | TRACK A completion record | BASELINE |
| `docs/MAIL-SECURITY-ARCHITECTURE.md` | TRACK B architecture | ACTIVE |
| `docs/SECURITY-SCENARIO-CATALOG.md` | TRACK B scenario definitions | ACTIVE |
| `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` | normalized external mechanism decisions | ACTIVE |
| `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md` | external repository audit method | ACTIVE |
| `docs/RESEARCH-LEDGER.json` | machine-readable research state | ACTIVE |
| `docs/AI-SWARM-ARCHITECTURE.md` | agent/swarm architecture | ACTIVE |
| `docs/AI-RUNNER-ARCHITECTURE.md` | agent runtime architecture | ACTIVE |
| `docs/SEC-003-SECRET-PROVENANCE.md` | secret-handling contract | ACTIVE |

## Track status

- **TRACK A A1–A8:** CLOSED / protected baseline.
- **TRACK B B1:** COMPLETE — documentation reset and authority map established.
- **TRACK B B2:** COMPLETE for EXT-AUDIT-001 — retained external repository set audited and normalized. Future repositories are separate audit items.
- **TRACK B B3+:** post-baseline implementation work.

## Historical documents

Files named `AUDIT-*`, version-specific `README-*`, old fix plans, patches, round reports and similar snapshots are retained as evidence/history. They are not current instructions unless a canonical document explicitly references them.

Historical status must not override source + tests + CI.

## External repository audit routing

All external-repository research follows:

```text
repository/revision
→ source entry points
→ execution trace
→ mechanism inventory
→ load2 mapping
→ decision tag
→ focused implementation only if justified
→ tests / security review
→ CI
→ canonical documentation sync
```

Central summary: `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`  
Method: `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md`  
Per-repository evidence: `docs/external-repos/*.md`

README claims from an external project are not implementation evidence.

## Synchronization rule

When an implementation changes:

```text
source
→ focused tests
→ CI / security evidence
→ GAP MATRIX
→ BACKLOG
→ relevant architecture/scenario document
→ ROADMAP if priority/status changed
```

Do not maintain a second description of the same feature in multiple active documents.

## Documentation cleanup rule

When an old document conflicts with a canonical document, do not silently rewrite history. Mark or route the old material as historical evidence and update the canonical document with the verified current state.

## Security documentation rule

The documentation may describe controlled defensive simulations of abuse patterns. It must clearly distinguish those simulations from unrestricted third-party abuse capabilities. Do not turn documentation into operational instructions for CAPTCHA/OTP bypass, anti-abuse evasion, real botnets, provider-limit evasion or unrestricted public-target flooding/DoS/DDoS.
