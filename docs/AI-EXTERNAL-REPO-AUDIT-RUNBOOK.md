# load2 — AI External Repository Audit Runbook

**Authority:** `sirvan0010-alt/load2` / `main`

This is the operational procedure for an AI continuing external-repository research in load2. It exists so a future AI can resume the work without reconstructing the methodology from chat history.

## 1. Mission

The goal is **technical transfer**, not repository endorsement.

External projects may be named bomber, spammer, flooder, DDoS, scanner, POC, stress tool, or something else. The name does not decide the result. The AI must inspect the implementation and classify mechanisms individually.

If load2 later needs a spam/load/flood/DDoS-style test capability, do not throw the relevant research away. Instead identify the underlying scheduling, transport, concurrency, repetition, endpoint, failure, and observability mechanisms and determine how they can be represented as an explicitly authorized, bounded load2 test scenario.

## 2. Non-negotiable distinction

Separate:

1. **mechanism** — e.g. worker fan-out, repeated-send loop, provider selection, connection reuse, retry, endpoint rotation, timing, queueing;
2. **operational abuse path** — e.g. unrestricted public-target flooding, credential theft, CAPTCHA/OTP bypass, stealth/evasion, defeating provider protections.

The first category is research material and may be ADOPT/ADAPT/HARDEN/SIMULATE/EXTRACT/REFERENCE.

The second category is not imported as an unrestricted abuse feature. If its technical property is useful for authorized testing, redesign it behind load2's scope, authorization, pacing, concurrency, cancellation and safety controls.

## 3. Source-of-truth procedure

Before auditing anything:

1. Read `docs/AI-PROJECT-GUIDE.md`.
2. Read `docs/LOAD2-GAP-MATRIX.md`.
3. Read `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md`.
4. Establish the current `main` commit SHA.
5. Never use `load` or `Load-tester-` as implementation authority.
6. If external source conflicts with load2, load2 `main` wins for architecture.

## 4. Source-level audit — EXT-AUDIT-001

README-only inspection is insufficient.

For every external repository, inspect source files and record exact evidence for:

- repository and exact revision/commit;
- entry point(s);
- source path(s);
- class/function/method/symbol;
- input/configuration;
- target representation and scope;
- network/protocol calls;
- provider/endpoint selection;
- worker/thread/task/async model;
- queue/backpressure model;
- connection/session lifecycle;
- rate limiting and pacing;
- repeat/time/duration logic;
- retries and failure handling;
- timeout/cancellation behavior;
- payload/message generation;
- logging/progress/results;
- persistence/deduplication;
- dependencies/runtime assumptions;
- security-relevant behavior;
- mechanism transferable to load2;
- exact load2 component that could receive it;
- decision tag and reason;
- tests needed to prove the transfer.

If a source cannot be inspected, mark the item `PARTIAL` or `PENDING`; never infer implementation from the README.

## 5. Deep analysis of offensive modules

When a repository contains spam/flood/DDoS/bomber modules, explicitly inspect them rather than skipping them.

Trace the complete execution path:

```text
input target
  -> scenario/loop
  -> queue or thread creation
  -> provider/endpoint selection
  -> connection/session
  -> authentication if present
  -> payload creation
  -> send/request
  -> repeat/retry
  -> result/error
  -> progress/report
```

Record which parts are useful engineering mechanisms and which parts are abuse-specific.

Examples of mechanisms worth investigating:

- bounded versus unbounded fan-out;
- fixed workers versus task-per-item;
- time-based versus count-based scenarios;
- persistent versus per-operation connections;
- provider registry and selection;
- endpoint canonicalization/deduplication;
- dead-endpoint quarantine;
- retry admission;
- global and per-target pacing;
- cancellation and shutdown;
- failure classification;
- deterministic message generation;
- progress counters and structured results;
- replayable run configuration;
- multi-transport orchestration;
- failure injection and recovery testing.

Do not copy public target inventories, stolen credentials, evasion logic, CAPTCHA/OTP bypasses, or unrestricted destructive flooding behavior.

## 6. Decision tags

Use one tag for every important mechanism:

- `ADOPT` — direct conceptual fit; implement using existing load2 abstractions.
- `ADAPT` — useful idea requires redesign for C#/.NET and load2 controls.
- `HARDEN` — external evidence improves an existing load2 implementation.
- `SIMULATE` — implement a bounded/lab equivalent of a stress or failure mechanism.
- `EXTRACT` — retain the pattern/algorithm as design knowledge, not source code.
- `REFERENCE` — useful comparison, no implementation planned.
- `REJECT` — individual mechanism is not imported because it primarily enables credential/token theft, CAPTCHA/OTP bypass, stealth/evasion, arbitrary public-target abuse discovery, unrestricted destructive DoS/DDoS/flooding, or defeating provider abuse controls.

A repository can contain all of these tags simultaneously.

## 7. Mapping into load2

Always compare external execution against the load2 execution pipeline:

```text
bounded Channel
  -> fixed workers
  -> PerRecipientLimiter
  -> adaptive concurrency
  -> SMTP pool
  -> actual-SEND pacing gate
  -> SendAsync
```

A useful external mechanism must not bypass this pipeline accidentally.

Examples:

| External mechanism | load2 treatment |
|---|---|
| repeated-send loop | `ADAPT` to an explicit count/duration scenario with hard bounds |
| high concurrency | `ADAPT` to bounded workers + adaptive concurrency |
| provider fan-out | `ADAPT` to provider/transport registry within explicit scope |
| dead provider tracking | `ADOPT/HARDEN` as endpoint health/quarantine |
| retry storm | `SIMULATE` as bounded retry/failure scenario |
| connection-per-message | `REFERENCE/HARDEN` comparison against persistent SMTP sessions |
| timing interval | `ADAPT` into the actual-SEND pacing controller |
| target list | `ADAPT` only as explicit authorized scope, with canonicalization/deduplication |
| public target discovery for abuse | `REJECT` |
| credential theft | `REJECT` |
| CAPTCHA/OTP bypass | `REJECT` |
| stealth/evasion | `REJECT` |

## 8. Special rule for future spam/DDoS modules

If the project owner later requests a dedicated stress/flood/DDoS test module, treat it as a **scenario-engineering problem** rather than importing a bomber unchanged.

The architecture should be:

```text
Authorized target/scope
      |
      v
Scenario definition
(count / duration / rate / concurrency / payload profile)
      |
      v
Safety + scope validation
      |
      v
Bounded scheduler
      |
      v
Transport/provider adapter
      |
      v
Existing pacing + adaptive concurrency + cancellation
      |
      v
Observable execution
      |
      v
Evidence / report / replay artifact
```

The test must have explicit limits and cancellation. It must be observable and reproducible. The module must not silently turn a controlled test into unrestricted public-target flooding.

For network-layer DoS research, prefer controlled lab infrastructure, local test services, disposable endpoints, synthetic targets, or other explicitly authorized environments.

## 9. Audit document format

Create `docs/external-repos/<RepositoryName>.md` with:

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

Keep exact file paths and symbols. Do not replace source evidence with general descriptions.

## 10. Update the central matrix

After each completed audit:

1. Update the per-repository document.
2. Update `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` with the important mechanisms.
3. Update `docs/LOAD2-GAP-MATRIX.md` when a real capability gap is discovered.
4. Do not add a gap merely because another repository has a feature; verify that load2 actually lacks it.

## 11. Implementation order after research

Do not immediately copy code from an external repository.

Use this order:

1. extract mechanism;
2. compare against existing load2 implementation;
3. identify the exact abstraction to extend;
4. design the smallest compatible change;
5. add focused unit/integration tests;
6. verify cancellation/concurrency/rate-limit invariants;
7. verify secret and target handling;
8. update documentation;
9. verify CI and CodeQL;
10. record the real commit SHA and status;
11. continue with the next unresolved mechanism.

## 12. Current audit sequence

The current EXT-AUDIT-001 source list contains:

1. `un1cum/Beast_Bomber`
2. `bhattsameer/Bombers`
3. `tr0uble-mAker/POC-bomber`
4. `yyasha/smsbomb`
5. `devops-kung-fu/bomber`
6. `ncorbuk/Python---Email-Bomber`
7. `hackerxphantom/X_BOMB`
8. `anubhavanonymous/XLR8_BOMBER`
9. `juzeon/fast-mail-bomber`
10. `mohinparamasivam/Email-Bomber`
11. `OpticHalberdier/sms-bomber-tool`
12. `brolyklade7/Mail-Bomber-Full-Version`
13. `niushamjd/bomberman-rl-pinkbombers`
14. `Trehwmm/Email-Bomber-SMTP`
15. `noluckkid/wade-miller-bombers`

Audit them in order unless a higher-priority load2 bug blocks progress.

## 13. Definition of done for an audit

An audit is complete only when:

- source-level execution flow was inspected;
- offensive modules were not skipped;
- mechanisms were separated from abuse-specific behavior;
- every significant mechanism has a decision tag;
- load2 mapping is explicit;
- unknowns are marked rather than guessed;
- the per-repository document is committed;
- the central audit/matrix is updated when needed;
- no unsupported implementation claim is made.

## 14. Handoff rule

The next AI should be able to open this file, open the current `main`, and continue from the next uncompleted repository without needing the previous chat transcript.

**Principle:**

> Study broadly. Trace source precisely. Classify mechanisms individually. Preserve useful engineering. Convert stress/abuse-origin mechanics into controlled authorized tests where appropriate. Never invent evidence.
