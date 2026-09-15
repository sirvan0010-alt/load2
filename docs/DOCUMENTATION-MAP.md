# load2 — Documentation Map and Authority

The repository contains a large historical audit trail. It must not become a collection of competing plans.

## Canonical documents

| Document | Role |
|---|---|
| `docs/AI-GUIDE.md` | single entry point for engineering/AI work |
| `docs/LOAD2-ROADMAP.md` | current product direction and priorities |
| `docs/LOAD2-GAP-MATRIX.md` | current capability/gap status |
| `docs/IMPLEMENTATION-BACKLOG.md` | executable backlog |
| `docs/A8-BASELINE.md` | immutable TRACK A completion record |
| `docs/MAIL-SECURITY-ARCHITECTURE.md` | TRACK B architecture |
| `docs/SECURITY-SCENARIO-CATALOG.md` | TRACK B scenario definitions |
| `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` | normalized external mechanism decisions |
| `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md` | external repository audit method |
| `docs/RESEARCH-LEDGER.json` | machine-readable research state |
| `docs/AI-SWARM-ARCHITECTURE.md` | agent/swarm architecture |
| `docs/AI-RUNNER-ARCHITECTURE.md` | agent runtime architecture |
| `docs/SEC-003-SECRET-PROVENANCE.md` | secret-handling contract |

## Historical documents

Files named `AUDIT-*`, version-specific `README-*`, old fix plans, patches and round reports are retained as evidence/history. They are not current instructions unless explicitly referenced by a canonical document.

Historical status must not override source + tests + CI.

## Synchronization rule

When an implementation changes:

```text
source
→ focused tests
→ CI / security evidence
→ GAP MATRIX
→ BACKLOG
→ relevant architecture/scenario document
→ roadmap if priority/status changed
```

Do not maintain a second description of the same feature in multiple active documents.

## Source-of-truth hierarchy

1. source code + tests on `main`;
2. verified CI/security evidence;
3. canonical documentation above;
4. historical audit documents.

The current working branch may contain proposed changes, but a feature is not part of the authoritative product baseline until the change is merged into `main` and verified.
