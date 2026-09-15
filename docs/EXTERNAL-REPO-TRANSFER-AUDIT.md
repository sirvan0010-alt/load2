# External Repository Transfer Audit — `load2`

**Status:** EXT-AUDIT-001 — retained repository set audited  
**Authority:** `sirvan0010-alt/load2` / `main`

## 1. Purpose

External repositories are studied **mechanism-by-mechanism**. A repository name, README claim or intended use does not determine the engineering decision.

The objective is to identify useful scheduling, transport, diagnostics, reliability, observability, reproducibility and test-environment mechanisms and determine whether load2 should `ADOPT`, `ADAPT`, `HARDEN`, `EXTRACT`, `SIMULATE` or `REFERENCE` them.

Unknown facts remain `PENDING`. No revision, symbol or implementation detail is invented.

## 2. Audit method

Every retained repository is evaluated using:

```text
pinned revision
→ entry points / source paths
→ execution trace
→ mechanism inventory
→ load2 comparison
→ security boundary
→ decision
→ focused tests if adopted
```

The detailed method is defined in `docs/AI-EXTERNAL-REPO-AUDIT-RUNBOOK.md`.

## 3. Decision vocabulary

| Decision | Meaning |
|---|---|
| HAVE | load2 already demonstrates the mechanism |
| GAP | load2 has a concrete source-level capability gap |
| ADOPT | mechanism fits the existing architecture with minimal change |
| ADAPT | mechanism is useful but must be redesigned for load2 contracts |
| HARDEN | external evidence is used to strengthen existing behavior/tests |
| EXTRACT | algorithm/data-model knowledge is retained without code transfer |
| SIMULATE | behavior is reproduced safely in a controlled test model |
| REFERENCE | useful comparison only; no current implementation need |
| REJECT | incompatible with product scope or required governance/evidence |

## 4. Retained EXT-AUDIT-001 repository set

| Repository | Mechanism of interest | load2 decision |
|---|---|---|
| `un1cum/Beast_Bomber` | transport/provider dispatch, bounded execution, metrics | ADOPT / ADAPT |
| `bhattsameer/Bombers` | provider registry, health/session lifecycle, repetition | ADOPT / HARDEN |
| `tr0uble-mAker/POC-bomber` | verification/execution separation, plugin registry, workers, evidence | ADOPT / ADAPT |
| `yyasha/smsbomb` | provider abstraction, health, timeout/retry patterns | ADAPT — controlled |
| `devops-kung-fu/bomber` | provider/enrichment/result pipeline, structured reporting | ADOPT architecture |
| `ncorbuk/Python---Email-Bomber` | basic SMTP lifecycle/configuration comparison | REFERENCE |
| `hackerxphantom/X_BOMB` | provider abstraction/orchestration concepts | ADAPT where source evidence supports |
| `anubhavanonymous/XLR8_BOMBER` | capability detection/service-health concepts | ADOPT architecture where evidence supports |
| `juzeon/fast-mail-bomber` | provider/node inventory, deduplication, dead-provider tracking | ADOPT / ADAPT controlled |
| `mohinparamasivam/Email-Bomber` | SMTP lifecycle/failure handling comparison | REFERENCE |
| `OpticHalberdier/sms-bomber-tool` | claimed queue/provider/retry architecture | REFERENCE until source proves claims |
| `brolyklade7/Mail-Bomber-Full-Version` | workflow/configuration comparison | REFERENCE |
| `niushamjd/bomberman-rl-pinkbombers` | experiment/reproducibility concepts | REFERENCE / architecture inspiration |
| `Trehwmm/Email-Bomber-SMTP` | SMTP/TLS/session lifecycle comparison | REFERENCE / security-critical audit |
| `noluckkid/wade-miller-bombers` | comparative orchestration | REFERENCE |

Per-repository source evidence is retained under `docs/external-repos/` and normalized into `docs/RESEARCH-LEDGER.json`.

## 5. Additional network/load references

### `slowhttptest`

Pinned source audit already recorded in `docs/POST-BASELINE-EXTERNAL-MECHANISM-PLAN.md` at revision `bbd33de733ccb5d7c87ffefebe1373d035a573a1`.

Transferable concepts: separation of rate and concurrency, explicit timeout/probe handling, lifecycle observability and authoritative event/report modeling.

Decision: **HARDEN / ADOPT principles only**. HTTP-specific reactor code is not imported into the SMTP engine.

### `GoldenEye`

Pinned source audit already recorded at revision `792862f5c8cb98f9ffcb9fab245e2c663e3a1026`.

Transferable concepts: session reuse, worker monitoring/shutdown and explicit TLS policy handling.

Decision: **HARDEN / ADAPT principles only**. No attack orchestration is copied into load2.

### `rojberr/mailcannon`

Decision: **REFERENCE only** for the current one-PC product direction. Its Docker/Swarm/Kubernetes distributed scaling is not a dependency for load2. The existing bounded local execution model is the relevant baseline.

### `StruisICT/smtp-test-tool`

Decision: **REFERENCE / ADOPT architecture concepts** for diagnostics. Relevant concepts include provider presets, SMTP/IMAP/POP3 connectivity diagnostics, DNS authentication checks, TLS diagnostics and machine-readable results. Existing load2 diagnostic primitives remain authoritative; no second transport/diagnostic engine is created.

### `Olib-AI/mailcue`

Decision: **REFERENCE / SIMULATE / LAB INTEGRATION candidate**. Its controlled mail environment is relevant to B6: SMTP/IMAP mailbox testing, authentication and delivery workflows. It should remain an external test environment rather than being embedded into the core SMTP transport layer.

### `stalwartlabs/mail-auth`

Decision: **REFERENCE** for protocol/RFC implementation knowledge around SPF, DKIM, DMARC and related mail authentication. It is not a reason to replace existing .NET diagnostics without a demonstrated requirement.

### `charlesgreen/email`

Decision: **REFERENCE / ADOPT analysis concepts** for parsing and correlating authentication headers and DMARC evidence. Useful primarily for B5/B7 evidence analysis.

### `usnistgov/dmarc-tester`

Decision: **REFERENCE / TEST-FIXTURE source** for controlled SPF/DKIM/DMARC validation concepts.

### `Mailpit` / `MailHog`

Decision: **REFERENCE / LAB** for local mail capture and deterministic test environments. Prefer a maintained local mail-capture solution when B6 implementation starts.

## 6. Mechanisms already present in load2

External research must not create duplicate gaps. The current baseline already demonstrates:

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
- endpoint canonicalization/deduplication;
- `IMailPayloadPlugin` payload architecture;
- verified agent-runtime Docker boundary and CI controls.

These are not new gaps merely because an external repository implements something similar.

## 7. Current transfer priorities

| Priority | Mechanism | Destination |
|---|---|---|
| P1 | scenario definitions | B3 |
| P1 | deterministic provider simulation | B4 |
| P1 | behavioral event normalization/analysis | B5 |
| P1 | controlled mailbox/deliverability lab | B6 |
| P1 | richer auth/transport evidence | B7 |
| P1 | redacted replay artifacts | B8 |
| P1 | explicit execution/security gates | B9 |
| P2 | registration/Double-Opt-In simulation | controlled application lab |
| P2 | anti-bot/anti-abuse control testing | owned application only |
| P2 | distributed lab workers | authorized lab only |

## 8. Explicit exclusions from transfer

Do not import unrestricted operational paths for:

- arbitrary third-party registration automation;
- CAPTCHA or OTP bypass;
- anti-abuse evasion;
- real botnet operation;
- provider-limit evasion;
- credential/token theft;
- reflection/amplification;
- unrestricted public-target flooding or destructive DoS/DDoS.

The corresponding defensive objectives are represented through controlled simulations, owned applications, synthetic providers/recipients and bounded lab workers.

## 9. Required load2 execution pipeline

Every adopted or simulated mechanism must remain compatible with:

```text
TargetSet / Scenario
    ↓
bounded Channel
    ↓
workers / MaxConcurrency
    ↓
recipient/provider pacing
    ↓
adaptive concurrency
    ↓
SMTP/account pool
    ↓
AcquireSendSlotAsync
    ↓
SendAsync
    ↓
outcome classification
    ↓
DeliveryLedger / RetryMetrics / RunReport
    ↓
RunObservability / security analysis
```

No external mechanism may introduce a parallel queue, pacing system, retry policy or source-of-truth result model.

## 10. B2 completion criteria

EXT-AUDIT-001 is complete when each retained repository has a source-backed record, meaningful mechanisms have a decision, unknowns are explicitly marked, load2 mapping is recorded, and the canonical documents are synchronized.

Future repositories are audited as new items and do not reopen B2 or TRACK A automatically.
