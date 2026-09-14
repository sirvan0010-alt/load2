# load2 — AI External Repository Audit Runbook

**Authority:** `sirvan0010-alt/load2` / `main`  
**Current status:** EXT-AUDIT-001 retained repository set audited; future repositories follow this procedure.

## 1. Mission

The goal is technical transfer, not repository endorsement. External projects may be named bomber, spammer, flooder, DDoS, scanner, POC or stress tool. The name does not decide the result.

Audit the implementation and classify **individual mechanisms**. Useful engineering mechanisms may be transferred into authorized load2 testing; abuse-specific execution paths are not imported as unrestricted functionality.

## 2. Non-negotiable distinction

Separate:

1. **Mechanism** — worker fan-out, queueing, repeated-send loop, provider selection, connection reuse, retry, endpoint rotation, timing, diagnostics, result aggregation.
2. **Abuse path** — unrestricted public-target flooding, credential theft, CAPTCHA/OTP bypass, stealth/evasion, provider-control bypass, reflection/amplification or destructive DDoS behavior.

The first category may be `ADOPT`, `ADAPT`, `HARDEN`, `EXTRACT`, `SIMULATE` or `REFERENCE`. The second is not imported as an unrestricted path.

## 3. Source-of-truth procedure

Before auditing:

1. Read `docs/AI-GUIDE.md`.
2. Read `docs/LOAD2-GAP-MATRIX.md`.
3. Read `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`.
4. Establish the current `main` SHA.
5. Never use `load` or `Load-tester-` as implementation authority.
6. For load2 architecture, `main` source + tests win over external sources and historical notes.

## 4. Source-level audit requirements

README-only inspection is insufficient. Record, where source supports it:

- exact repository revision;
- entry points and source paths;
- class/function/method/symbol;
- inputs/configuration;
- target representation and scope;
- network/protocol calls;
- provider/endpoint selection;
- worker/thread/task/async model;
- queue/backpressure model;
- connection/session lifecycle;
- pacing/rate limiting;
- count/duration/repetition logic;
- retry/failure handling;
- timeout/cancellation;
- payload/message generation;
- logging/progress/results;
- persistence/deduplication;
- dependencies/runtime assumptions;
- security-relevant behavior;
- transferable mechanism;
- exact load2 mapping;
- decision tag and rationale;
- tests required before adoption.

If source cannot be inspected, mark the repository/mechanism `PARTIAL` or `PENDING`; never infer implementation from a README.

## 5. Execution trace

For aggressive or stress-oriented projects, trace the complete path:

```text
input target
  → scenario/loop
  → queue/worker creation
  → provider/endpoint selection
  → connection/session
  → authentication if present
  → payload creation
  → send/request
  → repeat/retry
  → result/error
  → progress/report
```

Inspect offensive modules rather than skipping them. Extract reusable scheduling, transport, failure and observability mechanisms while excluding abuse-specific behavior.

## 6. Decision tags

- `ADOPT` — direct conceptual fit using existing load2 abstractions.
- `ADAPT` — useful mechanism requires redesign for C#/.NET/load2 controls.
- `HARDEN` — external evidence strengthens an existing implementation.
- `SIMULATE` — bounded/lab equivalent of a stress or failure mechanism.
- `EXTRACT` — algorithm/data model retained as design knowledge.
- `REFERENCE` — useful comparison with no current implementation.
- `REJECT` — mechanism primarily enables theft, bypass, stealth/evasion, arbitrary public-target abuse discovery, unrestricted destructive DoS/DDoS/flooding or provider-abuse evasion.

A repository may receive several different decisions for different mechanisms.

## 7. Mapping into load2

Compare every adopted mechanism with the existing execution path:

```text
TargetSet / Scenario
  → bounded Channel
  → fixed workers
  → recipient/provider pacing
  → adaptive concurrency
  → SMTP/account pool
  → AcquireSendSlotAsync
  → SendAsync
  → outcome classification
  → DeliveryLedger / RetryMetrics / RunReport
  → RunObservability
```

An external mechanism must not accidentally bypass this pipeline.

## 8. Stress / distributed testing rule

Controlled SMTP/application stress may include repeated sends, rate/concurrency tests, connection churn, retry/failure stress and multi-target scenarios when the target and scope are explicitly authorized and existing controls remain intact.

Network transport stress belongs in controlled/lab/authorized environments. A distributed authorized test is not automatically an attack, but an unrestricted public-target DDoS launcher, botnet orchestrator, reflection/amplification workflow or equivalent destructive path is outside the load2 execution model.

## 9. Audit document format

Create `docs/external-repos/<RepositoryName>.md` containing:

```markdown
# <Repository>

## Status
- Audit: COMPLETE / PARTIAL / PENDING
- Revision: <exact SHA or ref>

## Entry points
...

## Source map
...

## Execution trace
...

## Mechanisms
| Mechanism | Evidence | load2 mapping | Decision |
|---|---|---|---|

## Security / abuse-specific behavior
...

## Tests required
...

## Conclusion
...
```

Keep exact paths and symbols. Do not replace source evidence with generic descriptions.

## 10. Central documentation update

After a completed audit:

1. update the per-repository audit;
2. update `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` when the mechanism matters centrally;
3. update `docs/LOAD2-GAP-MATRIX.md` only when load2 genuinely lacks the capability;
4. do not create a gap merely because another repository contains a feature.

## 11. Implementation order

```text
extract mechanism
→ compare with load2
→ identify exact abstraction
→ smallest compatible change
→ focused tests
→ verify cancellation/concurrency/pacing
→ verify target/secret handling
→ CI + CodeQL
→ documentation sync
```

Do not copy external code simply because it is shorter or faster.

## 12. Current EXT-AUDIT-001 result

The current retained list contains 15 repositories and has been audited at the depth recorded by the individual `docs/external-repos/*.md` documents. The central transfer table is the current summary.

The next repository, if added, is a **new audit item**, not an unfinished TRACK A item.

## 13. Definition of done

An audit is complete only when source-level execution was inspected, offensive modules were not skipped, mechanisms were separated from abuse-specific behavior, significant mechanisms received a decision tag, load2 mapping is explicit, unknowns are marked rather than guessed, and the relevant documentation is synchronized.

## 14. Handoff rule

Future AI work must start from the current `main`, `docs/AI-GUIDE.md`, `docs/LOAD2-GAP-MATRIX.md` and `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`. Do not reconstruct current status from old chat transcripts.
