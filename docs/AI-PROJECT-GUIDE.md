# load2 — AI Project Guide

**SOURCE OF TRUTH:** `sirvan0010-alt/load2` / branch `main`

This document is the orientation contract for any AI that continues work on this project.

## 1. Repository authority

- `main` is authoritative for architecture, code, tests and documentation.
- `load` is a secondary/parallel copy only.
- `Load-tester-` is legacy and must not be used for new implementation.
- Never resolve conflicting implementations by guessing. Inspect `main` first.
- Never invent a feature, interface, test result or implementation status. Mark unknown items as `AUDIT PENDING`.

## 2. What load2 is

`load2` is an SMTP/email testing framework. A user may provide a concrete target address and a test scenario. The application is intended to exercise SMTP delivery, message generation, transport behavior, performance, reliability, diagnostics and security characteristics of the selected target within an explicitly authorized test scope.

The project is not defined by the labels used by external projects. Repositories called `bomber`, `flooder`, `scanner`, `POC` or similar are valid research sources when their implementations contain useful mechanisms.

## 3. How external code must be interpreted

The first question is always:

> What concrete mechanism does this implementation provide, and how does that mechanism map to an authorized test in load2?

Audit the source, not only the README.

Useful mechanisms may include:

- high-throughput scheduling;
- bounded/unbounded queue strategies and backpressure;
- concurrency control;
- provider/transport abstraction;
- endpoint discovery and normalization;
- provider health and quarantine;
- retry and failure classification;
- repeated-send scenarios;
- multi-target orchestration;
- SMTP/TLS handling;
- DNS diagnostics;
- protocol robustness/failure injection;
- structured results and evidence;
- reproducible runs;
- automation and CI integration.

Offensive origin does not automatically invalidate a mechanism. Where an offensive capability is useful for security testing, prefer a controlled equivalent that operates within explicit target scope and the existing load2 execution controls.

## 4. Decision vocabulary

Use these tags per **mechanism**, not merely per repository:

- `ADOPT` — implement the mechanism in load2 after mapping it to current abstractions.
- `ADAPT` — implement the idea after redesign for C#/.NET/load2.
- `HARDEN` — improve an existing load2 mechanism using the external evidence.
- `SIMULATE` — implement a bounded/lab failure or stress equivalent.
- `EXTRACT` — preserve the architecture/pattern without importing the original code.
- `REFERENCE` — useful comparison only.
- `REJECT` — reject the individual mechanism when its primary purpose is credential/token theft, CAPTCHA/OTP bypass, stealth/evasion, arbitrary public-target abuse discovery, unrestricted destructive DoS/DDoS/flooding, or defeating provider abuse controls.

A repository may have several different decisions. `REJECT` must never be used as shorthand for "this repository is offensive".

## 5. Mandatory load2 execution invariants

Preserve the existing architecture unless source evidence and tests justify a change:

```text
bounded Channel
    -> fixed workers
    -> per-recipient limiter
    -> adaptive concurrency
    -> SMTP pool
    -> actual-SEND pacing gate
    -> SendAsync
```

Requirements:

- persistent SMTP sessions where supported;
- `SemaphoreSlim` for exclusive/shared critical sections as appropriate;
- adaptive rate limiting with thread-safe state (`Volatile`/interlocked primitives where already used);
- `CancellationToken` propagated through waits, network operations and retries;
- global pacing immediately before actual send;
- retries re-enter the same pacing/admission path;
- no task-per-message explosion;
- delivery ledger prevents duplicate logical deliveries during restart/retry scenarios;
- network layer remains separate from MIME/message-generation plugins;
- plugin extension uses `IMailPayloadPlugin` and existing plugin pipeline abstractions;
- credentials/configuration come from supported configuration/environment mechanisms, never hardcoded secrets.

## 6. Authorization and modes

`--unauthorized` is an explicit authorization acknowledgement for live sending; it is not a replacement for target validation or an execution mode.

`DryRun` and `TestMode` have their existing semantics and must not be silently changed.

The controls are boundaries around execution. They do not redefine the purpose of the testing framework.

## 7. External repository audit record

For every retained repository, create `docs/external-repos/<name>.md` and record:

- exact revision/commit reviewed;
- entry point(s);
- source file path(s);
- classes/functions/methods/symbols;
- configuration and inputs;
- protocols/network calls;
- concurrency and queue model;
- provider/endpoint selection;
- rate limiting and retries;
- timeout/cancellation behavior;
- payload/message generation;
- target handling;
- logging/reporting/persistence;
- dependencies/runtime assumptions;
- security-relevant behavior;
- each useful mechanism;
- exact mapping to load2;
- decision tag and reason;
- required tests.

A README-only result is not a source-level conclusion. If source cannot be inspected, mark it `PARTIAL` or `PENDING`.

The central index is `docs/EXTERNAL-REPO-TRANSFER-AUDIT.md` and the mechanism matrix is `docs/LOAD2-GAP-MATRIX.md`.

## 8. Current engineering backlog

Consult `docs/LOAD2-GAP-MATRIX.md` before starting a new feature. Current documented gaps include:

- FEAT-HEALTH — SMTP endpoint soft health score;
- FEAT-REPORT — JSON run summary;
- FEAT-RUNID — RunId + configuration snapshot;
- FEAT-VERIFY — plugin VerifyAsync / diagnostic separation;
- provider/transport registry;
- endpoint canonicalization/deduplication;
- formal failure classification;
- replayable run artifacts;
- DNS MX/SPF/DKIM/DMARC enrichment;
- TLS evidence enrichment;
- controlled failure injection;
- EXT-AUDIT-001 — source-level audit of every retained external repository.

Do not implement a backlog item solely because another AI suggested it. Verify the current `main` source first.

## 9. Definition of done

A change is not complete because code was written. Before declaring it complete:

1. inspect the current `main` implementation;
2. preserve compatibility with existing architecture;
3. add/update focused tests;
4. check cancellation and concurrency behavior;
5. check security and secret handling;
6. update the relevant project documentation;
7. run/verify CI and CodeQL where applicable;
8. record the commit SHA and actual result;
9. never report green status without evidence.

## 10. Working style for future AI sessions

When continuing work:

1. establish current `main` SHA;
2. inspect relevant source and tests;
3. inspect the existing documentation/backlog;
4. implement the smallest coherent change;
5. test it;
6. update documentation so another AI can continue without reconstructing history;
7. verify CI/security state;
8. continue to the next highest-priority unresolved item.

If something blocks or slows the project, investigate the cause and fix it rather than working around it silently.

## 11. External project philosophy

The goal of external-repository research is **technical transfer**. We want to learn from implementations regardless of language, protocol, file extension, project naming or original strategy.

Transfer:

- the algorithm when appropriate;
- the architecture when useful;
- the reliability pattern;
- the concurrency model;
- the diagnostics;
- the automation;
- the testing methodology;
- the observability;
- the failure handling.

Do not transfer unrelated secrets, stolen credentials, arbitrary public target inventories, evasion mechanisms or destructive abuse paths.

The distinction is therefore:

**study broadly → classify precisely → implement useful mechanisms → preserve explicit authorization/scope and engineering controls.**
