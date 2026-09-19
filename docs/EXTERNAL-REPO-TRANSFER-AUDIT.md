# External Repository Transfer Audit — `load2`

**Status:** EXT-AUDIT-001 — retained repository set audited  
**Authority:** `sirvan0010-alt/load2` / `main`

## 1. Interpretation

External repositories are studied **mechanism-by-mechanism**, including repositories whose primary purpose is spam, bombing, flooding, scanning or DoS/DDoS. The repository label does not determine the decision.

Reusable mechanisms can be `ADOPT`, `ADAPT`, `HARDEN`, `EXTRACT`, `SIMULATE` or `REFERENCE` when they improve an authorized load2 test. Abuse-specific paths remain separate from the engineering mechanism.

## 2. Audited repository set

All 15 repositories in the current EXT-AUDIT-001 list have been source-audited at the level recorded in their individual audit documents. No repository is treated as authoritative for load2 implementation.

| Repository | Main transferable mechanism(s) | Decision |
|---|---|---|
| `un1cum/Beast_Bomber` | transport/provider dispatch, bounded execution patterns, metrics | ADOPT / ADAPT |
| `bhattsameer/Bombers` | provider registry, health, session lifecycle, bounded repetition | ADOPT / HARDEN |
| `tr0uble-mAker/POC-bomber` | verify/execute separation, plugin registry, bounded workers, evidence | ADOPT / ADAPT |
| `yyasha/smsbomb` | provider abstraction, health, timeout/retry patterns | ADAPT controlled |
| `devops-kung-fu/bomber` | provider/enrichment/result pipeline, structured reporting | ADOPT architecture |
| `ncorbuk/Python---Email-Bomber` | basic SMTP lifecycle/configuration comparison | REFERENCE |
| `hackerxphantom/X_BOMB` | provider abstraction, bounded orchestration concepts | ADAPT where source evidence supports |
| `anubhavanonymous/XLR8_BOMBER` | capability detection and service-health concepts | ADOPT architecture; unsafe paths excluded |
| `juzeon/fast-mail-bomber` | provider/node inventory, deduplication, dead-provider tracking | ADOPT / ADAPT controlled |
| `mohinparamasivam/Email-Bomber` | SMTP lifecycle and failure handling comparison | REFERENCE / compare |
| `OpticHalberdier/sms-bomber-tool` | claimed queue/provider/retry architecture | REFERENCE until source proves claims |
| `brolyklade7/Mail-Bomber-Full-Version` | workflow/configuration comparison | REFERENCE |
| `niushamjd/bomberman-rl-pinkbombers` | experiment reproducibility concepts | REFERENCE / ADOPT architecture where useful |
| `Trehwmm/Email-Bomber-SMTP` | SMTP/TLS/session lifecycle comparison | REFERENCE / SECURITY-CRITICAL source audit |
| `noluckkid/wade-miller-bombers` | comparative orchestration review | REFERENCE |
| `AIPentest/CyberStrikeAI` | Plan/Execute, Supervisor routing, specialist agents, Skills, policy/HITL, structured evidence | ADAPT / EXTRACT / REFERENCE |

The exact source evidence and decision rationale are maintained in `docs/external-repos/*.md`.

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

These are **not** new backlog gaps merely because an external repository also contains them.

## 4. Post-baseline transfer candidates

Only concrete gaps should become new implementation work. Current candidates are:

1. provider/transport abstraction refinements where the existing types genuinely benefit;
2. endpoint canonicalization/deduplication if a real source-level gap is demonstrated;
3. controlled failure-injection scenarios;
4. replayable/redacted run artifacts if required by the product;
5. GUI summary projection using `RunObservability.FromReport`;
6. authorized `NET-AUDIT-001` fixtures;
7. further external ADOPT/ADAPT work supported by source evidence;
8. AI orchestration layer: Supervisor + bounded Plan/Execute + pre-action guard, specified in `docs/AI-ORCHESTRATION-DESIGN.md`.

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

## 6. Non-transfer boundary

Do not import mechanisms whose primary purpose is credential/token theft, CAPTCHA/OTP bypass, stealth/evasion for abuse, arbitrary public-target discovery for flooding, provider-abuse bypass, reflection/amplification or unrestricted destructive DoS/DDoS.

This boundary does **not** prohibit controlled stress/load mechanisms. A high-concurrency or distributed test can be legitimate when its target, scope, limits, cancellation and observability are explicit and authorized.

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
