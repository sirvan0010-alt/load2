# EXT-AUDIT-001 — 15-repository mechanism matrix

Authority: `sirvan0010-alt/load2` / `main`

This matrix records the final disposition of all 15 repositories in the retained EXT-AUDIT-001 set. It does not replace the per-repository source audits; it provides the completion index and prevents README claims from being promoted to source facts.

| # | Repository | Source status | Highest-value verified/retained mechanism | Decision | Load2 action |
|---|---|---|---|---|---|
| 1 | `un1cum/Beast_Bomber` | SOURCE-AUDITED / partial transport scope | transport separation, scenario dispatch, lifecycle/outcome accounting | ADOPT / ADAPT / HARDEN | Keep provider/transport boundary, bounded async workers and typed outcomes. |
| 2 | `bhattsameer/Bombers` | SOURCE-AUDITED | persistent SMTP session, finite count/rate, provider abstraction | ADOPT / ADAPT / HARDEN | Reinforces persistent sessions, scenario model, provider registry and rate-limit observability. |
| 3 | `tr0uble-mAker/POC-bomber` | SOURCE-AUDITED | verify/execute separation, bounded pool, plugin discovery, structured evidence | ADOPT / ADAPT | Map to scenario/plugin/evidence layers; keep explicit authorization gate. |
| 4 | `yyasha/smsbomb` | SOURCE-AUDITED | provider fan-out, target normalization, request timeout concept | ADAPT / HARDEN | Provider abstraction only; retain bounded workers and centralized pacing. |
| 5 | `devops-kung-fu/bomber` | SOURCE-AUDITED | provider interface, validation, enrichment, structured results, fake-provider tests | ADOPT / ADAPT | Strengthen provider registry, validation, enrichment and test seams. |
| 6 | `ncorbuk/Python---Email-Bomber` | SOURCE-AUDITED | persistent SMTP session, configurable endpoint/port, typed connect/auth failures | ADOPT / ADAPT / HARDEN | Reinforce current MailKit session lifecycle and outcome taxonomy. |
| 7 | `hackerxphantom/X_BOMB` | SOURCE-AUDITED | provider registry, provider health/quarantine, bounded executor, timeout | ADAPT / HARDEN | Reuse concepts with thread-safe health state and existing Channel workers. |
| 8 | `anubhavanonymous/XLR8_BOMBER` | SOURCE-AUDITED; core obfuscated | only architectural/capability claims are reliable | REFERENCE / HARDEN | No hidden concurrency/retry/provider behavior is accepted as proven. |
| 9 | `juzeon/fast-mail-bomber` | SOURCE-AUDITED | provider/node discovery, deduplication, dead-provider tracking, bounded pool | ADOPT / ADAPT / HARDEN | Apply discovery→validation→normalization separation and health state. |
| 10 | `mohinparamasivam/Email-Bomber` | SOURCE-AUDITED | persistent SMTP loop, configurable server/port, explicit auth/connect errors | ADOPT / ADAPT / HARDEN | Use current persistent MailKit session and safe TLS policy; reject plaintext fallback. |
| 11 | `OpticHalberdier/sms-bomber-tool` | SOURCE-AUDITED; executable sources empty | README-claimed queue/provider/retry architecture only | REFERENCE / ADAPT | Claims are corroboration only; implementation must come from source-proven repos. |
| 12 | `brolyklade7/Mail-Bomber-Full-Version` | SOURCE-AUDITED; no mail-sender source | no SMTP engine source-proven | REFERENCE / REJECT | No binary/download or marketing claim is imported. |
| 13 | `niushamjd/bomberman-rl-pinkbombers` | SOURCE UNAVAILABLE at current audit | no mechanism source-proven | PENDING | Re-audit only from readable pinned source; no mechanism invented. |
| 14 | `Trehwmm/Email-Bomber-SMTP` | SOURCE-AUDITED | SMTP worker/cancellation comparison; hostile build-loader and CI patterns identified | REFERENCE / HARDEN / REJECT | Preserve cancellation; reject obfuscated native MSBuild loader and unsafe CI patterns. |
| 15 | `noluckkid/wade-miller-bombers` | SOURCE-AUDITED as classification | scheduled content pipeline, validation and external secret handling; not an SMTP load engine | REFERENCE | No mail-load mechanism transferred. |

## Cross-repository conclusions

### Already stronger in load2

- bounded `Channel` + fixed workers;
- `SmartPaceController` as the single pacing system;
- actual-SEND admission gate;
- persistent SMTP sessions and session pools;
- multi-account SMTP pools;
- endpoint health/quarantine;
- retry budget and unified SMTP outcome classification;
- `CancellationToken` propagation;
- `DeliveryLedger` / `RunReport` / `RunObservability`;
- DNS/MX/SPF/DMARC and SMTP/TLS diagnostics.

External repositories do not justify duplicating these mechanisms.

### Genuine post-baseline candidates

1. provider/transport capability registry refinement;
2. target/endpoint canonicalization and deduplication before queue admission;
3. controlled failure-injection scenarios;
4. replayable/redacted run artifacts;
5. explicit transport/session lifecycle evidence;
6. payload/scenario diversity through `IMailPayloadPlugin`;
7. cancellation-aware external evidence correlation.

Each candidate requires a source-backed gap, implementation decision, focused tests and green CI before merge.

## Non-negotiable research rule

`README → claim` is not evidence. The accepted chain is:

`pinned source → symbol/file evidence → mechanism → decision → load2 mapping → test → CI`

Unknown or unavailable source remains `PENDING`. No credentials, public abuse endpoints, unrestricted bombing/flooding orchestration or opaque executable payloads are transferred merely because an external repository contains them.
