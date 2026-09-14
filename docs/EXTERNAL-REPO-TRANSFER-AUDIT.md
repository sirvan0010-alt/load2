# External Repository Transfer Audit — `load2`

**Status:** EXT-AUDIT-001 — retained repository set audited  
**Authority:** `sirvan0010-alt/load2` / `main`

## 1. Interpretation

External repositories are studied **mechanism-by-mechanism**, including repositories whose primary purpose is spam, bombing, flooding, scanning or DoS/DDoS. The repository label does not determine the engineering decision.

Reusable mechanisms can be `ADOPT`, `ADAPT`, `HARDEN`, `EXTRACT`, `SIMULATE` or `REFERENCE` when they improve a load2 capability. A mechanism's eventual execution context, authorization model, scope, limits and verification requirements are evaluated separately from the mechanism itself.

The Research Ledger is the machine-readable state for this research. Unknown facts remain `PENDING`; no source revision or mechanism may be invented.

## 2. Audited repository set

All 15 repositories in the current EXT-AUDIT-001 list have repository-level source audit records in this documentation set. The ledger additionally tracks the two network research repositories explicitly queued for the current collaboration work: slowhttptest and GoldenEye.

| Repository | Main transferable mechanism(s) | Decision |
|---|---|---|
| `un1cum/Beast_Bomber` | transport/provider dispatch, bounded execution patterns, metrics | ADOPT / ADAPT |
| `bhattsameer/Bombers` | provider registry, health, session lifecycle, bounded repetition | ADOPT / HARDEN |
| `tr0uble-mAker/POC-bomber` | verify/execute separation, plugin registry, bounded workers, evidence | ADOPT / ADAPT |
| `yyasha/smsbomb` | provider abstraction, health, timeout/retry patterns | ADAPT controlled |
| `devops-kung-fu/bomber` | provider/enrichment/result pipeline, structured reporting | ADOPT architecture |
| `ncorbuk/Python---Email-Bomber` | basic SMTP lifecycle/configuration comparison | REFERENCE |
| `hackerxphantom/X_BOMB` | provider abstraction, bounded orchestration concepts | ADAPT where source evidence supports |
| `anubhavanonymous/XLR8_BOMBER` | capability detection and service-health concepts | ADOPT architecture where evidence supports |
| `juzeon/fast-mail-bomber` | provider/node inventory, deduplication, dead-provider tracking | ADOPT / ADAPT controlled |
| `mohinparamasivam/Email-Bomber` | SMTP lifecycle and failure handling comparison | REFERENCE / compare |
| `OpticHalberdier/sms-bomber-tool` | claimed queue/provider/retry architecture | REFERENCE until source proves claims |
| `brolyklade7/Mail-Bomber-Full-Version` | workflow/configuration comparison | REFERENCE |
| `niushamjd/bomberman-rl-pinkbombers` | experiment reproducibility concepts | REFERENCE / ADOPT architecture where useful |
| `Trehwmm/Email-Bomber-SMTP` | SMTP/TLS/session lifecycle comparison | REFERENCE / SECURITY-CRITICAL source audit |
| `noluckkid/wade-miller-bombers` | comparative orchestration review | REFERENCE |

The exact source evidence and decision rationale are maintained in `docs/external-repos/*.md` and are progressively normalized into `docs/RESEARCH-LEDGER.json`.

## 3. Mechanisms already represented in load2

The external research has already informed or corresponds to capabilities now present in the baseline:

- bounded worker/channel execution;
- actual-SEND pacing;
- persistent SMTP sessions;
- multi-account SMTP pools;
- endpoint health/quarantine;
- connection churn scenarios;
- target/scenario limits;
- retry policy and retry metrics;
- unified SMTP outcome classification;
- queue metrics;
- RunReport and RunObservability;
- DNS/MX/SPF/DMARC and SMTP/TLS diagnostics;
- FEAT-022 phase timing.

These are **not** new backlog gaps merely because an external repository also contains them. Any claimed gap requires current-source evidence.

## 4. Post-baseline transfer candidates

Only concrete source-backed gaps should become new implementation work. Current candidates are:

1. provider/transport abstraction refinements where existing types genuinely benefit;
2. endpoint canonicalization/deduplication if a real source-level gap is demonstrated;
3. controlled failure-injection scenarios;
4. replayable/redacted run artifacts if required by the product;
5. GUI summary projection using `RunObservability.FromReport`;
6. authorized `NET-AUDIT-001` fixtures;
7. further external ADOPT/ADAPT work supported by pinned source evidence.

No candidate should bypass the existing execution pipeline.

## 5. Required load2 pipeline

```text
TargetSet / Scenario
    ↓
bounded Channel
    ↓
fixed workers
    ↓
recipient/provider pacing
    ↓
adaptive concurrency
    ↓
SMTP pool / account pool
    ↓
AcquireSendSlotAsync
    ↓
SendAsync
    ↓
outcome classification
    ↓
DeliveryLedger / RetryMetrics / RunReport
    ↓
RunObservability
```

## 6. Capability transfer boundary

There is no blanket repository-name-based prohibition on researching or extracting a mechanism. Instead, the implementation decision must record the mechanism, source evidence, target context, authorization requirements, resource bounds, cancellation model, observability and verification plan.

High-risk capabilities require explicit authorization and technical governance before execution. This is a **governance and execution boundary**, not a claim that the capability can never exist in load2.

A mechanism may therefore be:

- `REFERENCE` when useful for comparison only;
- `EXTRACT` when isolated as reusable engineering knowledge;
- `SIMULATE` when a controlled test model is appropriate;
- `ADOPT` / `ADAPT` when the mechanism fits the load2 architecture and gates;
- `REJECT` when it does not fit the documented product scope or cannot meet the required evidence/governance contract.

## 7. Audit rule for future repositories

For every new repository:

```text
source revision
→ entry point / symbol map
→ execution trace
→ mechanism inventory
→ load2 mapping
→ decision tag
→ focused tests if adopted
→ CI / CodeQL
→ documentation sync
```

README claims alone are never proof. Unknown or unavailable source remains `PENDING` rather than being presented as implemented.

## 8. Relationship to TRACK A

TRACK A A1–A8 is closed. External-repository research is a **parallel post-baseline track** and must not reopen completed engine milestones without concrete regression evidence.
