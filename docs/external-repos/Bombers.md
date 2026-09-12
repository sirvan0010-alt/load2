# bhattsameer/Bombers — source-level mechanism audit

**Audit:** EXT-AUDIT-001  
**Status:** COMPLETE for inspected email/spam sources; non-email modules are identified but not treated as equivalent to SMTP implementation.  
**Repository:** `bhattsameer/Bombers`  
**Reviewed ref:** `master`  

## Repository evidence

The repository is an archived Python collection covering SMS, email, WhatsApp, Twitter/Instagram-style bombers and fake-SMS utilities. GitHub metadata identifies it as archived, with no declared license, and its default branch is `master`. The repository description explicitly advertises bomber/spam functionality and temporary-number utilities. fileciteturn333file0L2-L10

## Source inventory inspected

The root contains, among other files:

- `Email_bomber.py`
- `Email_Bomber_Version2.py`
- `SMS_bomber.py`
- `SMS_bomber_version2.py`
- `sms_bomber_updated.py`
- `Twitter_bomber.py`
- `numspy_bomber.py`
- `spam.py`
- `wbomb.py`
- `email.txt`
- `requirements_WBOMB.txt`

The repository listing confirms these exact source paths. fileciteturn334file0L2-L5

## 1. Email_bomber.py execution trace

Exact source: `Email_bomber.py`, blob `1c1674e3086f2d7c4f4e4573605e2a3fad424773`. fileciteturn335file0L2-L6

Execution flow:

```text
interactive target
  -> sender email + password
  -> message
  -> integer count
  -> provider selection
  -> one SMTP connection
  -> repeated EHLO/STARTTLS/AUTH/sendmail
  -> one-second sleep between sends
  -> close
```

### Mechanisms

| Mechanism | Source evidence | load2 treatment | Decision |
|---|---|---|---|
| Concrete single target | `bomb_email` input | Explicit target model already exists; keep validation/scope controls | HARDEN |
| Count-based repetition | `range(0,counter)` | Scenario count should map to bounded execution | ADAPT |
| Provider selection | Gmail vs Outlook branch | Map to transport/provider registry rather than hard-coded branches | ADAPT |
| Persistent connection within loop | `mail` created once before loop | This is aligned with load2 persistent SMTP-session architecture | ADOPT/HARDEN |
| STARTTLS | `starttls()` | Map to existing MailKit TLS mode/evidence | ADAPT |
| Authentication before each send | `login()` inside loop | Avoid unnecessary re-authentication when session remains authenticated; test server-specific behavior | REFERENCE/HARDEN |
| Fixed one-second delay | `time.sleep(1)` | Replace with global actual-SEND pacing controller | ADAPT |
| Broad exception | outer `except Exception` | Replace with typed failure classification and structured evidence | HARDEN |
| Credentials entered interactively | password input | Use environment/configuration/secret providers; never hardcode | REJECT as an implementation pattern for secret handling |

The source explicitly creates Gmail/Outlook SMTP endpoints, loops for the requested count, calls EHLO/STARTTLS/login/sendmail and sleeps one second. fileciteturn335file0L2-L2

### Important architecture finding

The strongest transferable idea in this file is **persistent SMTP connection + repeated send operations**. That matches a central load2 design goal much better than the other bomber mechanics. The one-second sleep is not itself the right load2 implementation because it is blocking and bypasses adaptive admission; load2 should use the existing actual-SEND gate.

## 2. Email_Bomber_Version2.py execution trace

Exact source: `Email_Bomber_Version2.py`, blob `55559334dbb2e70546d4df163a28f145a0857125`. fileciteturn336file0L2-L6

Flow:

```text
read email.txt
  -> sender credentials/message/count
  -> for each target line
      -> repeat count times
          -> create SMTP connection
          -> EHLO
          -> STARTTLS
          -> LOGIN
          -> sendmail
  -> sleep
  -> close
```

### Mechanisms

| Mechanism | Source evidence | load2 treatment | Decision |
|---|---|---|---|
| Target list from file | `email.txt` + `readlines()` | Useful as an input source, but must pass canonicalization/deduplication/scope validation | ADAPT |
| Per-target repeated count | nested target/count loops | Useful scenario primitive with explicit bounds | ADAPT |
| New connection per message | `SMTP(...)` inside inner loop | Useful benchmark comparison for connection overhead, not preferred production path | SIMULATE/REFERENCE |
| STARTTLS/auth/send sequence | direct `smtplib` calls | Map to MailKit transport layer | ADAPT |
| Fixed sleep after processing target list | `sleep(1)` | Replace with pacing controller tied to actual send | ADAPT |
| File-backed target inventory | local text file | Preserve only as explicit authorized target input; do not import public target inventories | HARDEN |

The exact source shows `email.txt` as the target list, nested repetition, and a new SMTP connection for each message. fileciteturn336file0L2-L2

## 3. spam.py execution trace

Exact source: `spam.py`, blob `663242a0ff46a36967272300143590b65bb5d0e0`. fileciteturn337file0L2-L6

This module is primarily GUI/keyboard automation rather than SMTP transport. It supports three user-selected modes:

1. messages read from a file;
2. a repeated text message;
3. generated numeric-message mode.

It asks for an interval and a message count. A count of zero is treated as an infinite loop in the repeated-message modes. The implementation uses `pyautogui`, clipboard paste and keyboard Enter to submit messages through an already-active UI rather than a protocol-specific network client. fileciteturn337file0L2-L2

### Transferable mechanisms

| Mechanism | load2 treatment | Decision |
|---|---|---|
| File-backed message sequence | useful deterministic payload source | ADAPT |
| Explicit interval | useful scenario parameter | ADAPT |
| Explicit finite count | useful scenario parameter | ADOPT |
| Infinite repetition (`0 = inf.`) | useful stress-test concept but unsafe as an unrestricted default | SIMULATE with mandatory cancellation/hard limits |
| Interactive UI automation | not part of SMTP network layer | REFERENCE |
| GUI-driven submission | decouple from load2 transport layer | REFERENCE |

The source is valuable mainly as evidence for a **scenario model**: payload source + interval + count/duration. It should not be copied as GUI automation.

## 4. Spam/bomber mechanics that should influence load2 design

The repository demonstrates several recurring primitives that are worth representing explicitly in load2:

### A. Count scenario

```text
Repeat N times
```

Map to a bounded scenario property such as `MaxAttempts`/`MessageCount` and route every attempt through the existing execution pipeline.

### B. Duration scenario

The repository family contains repeated-operation patterns; for load2 this should be represented as a cancellation/deadline-driven scenario rather than an unbounded loop.

### C. Target-set scenario

A file can provide multiple recipients. In load2 this should become an explicit authorized target set with:

- canonicalization;
- deduplication;
- validation;
- scope enforcement;
- deterministic ordering where reproducibility matters.

### D. Interval scenario

An external interval should never call `Task.Delay` around the network operation in a way that bypasses the global pacing gate. The correct load2 placement remains:

```text
worker
 -> recipient limiter
 -> adaptive concurrency
 -> SMTP pool
 -> actual-SEND pacing gate
 -> SendAsync
```

### E. Connection strategy

The two email implementations provide a useful A/B benchmark:

- connection reused for repeated sends;
- connection recreated for every send.

Load2 should retain persistent sessions as the normal path, while a deliberately controlled connection-churn scenario could be useful for performance/robustness testing.

## 5. Security / abuse-specific behavior

The repository is explicitly a bomber collection. Its source therefore demonstrates repeated delivery against supplied targets. That fact does **not** invalidate the engineering mechanisms above.

Mechanisms that should not be imported as unrestricted abuse paths include:

- public-target bombing workflows;
- credential collection/handling patterns copied from old scripts;
- fake-SMS or temporary-number verification bypass functionality;
- unlimited/unbounded public flooding;
- provider abuse-control evasion.

The correct load2 equivalent is a controlled scenario operating on an explicitly authorized target/scope and remaining inside the existing pacing, concurrency, cancellation and observability architecture.

## 6. Required load2 follow-up

This audit identifies these concrete backlog candidates:

1. **Scenario model** — count/duration/rate/concurrency/payload profile.
2. **Target-set input abstraction** — file/stdin/list with canonicalization and deduplication.
3. **Connection strategy test mode** — persistent-session baseline versus controlled connection churn.
4. **Failure classification** — distinguish DNS, connect, TLS, AUTH, SMTP command, timeout, cancellation and policy/rate-limit failures.
5. **Replayable run configuration** — record scenario parameters without secrets.

These are architectural transfers, not copies of the original scripts.

## 7. Tests required before implementation is considered complete

- finite count executes exactly N logical attempts;
- duration scenario stops at cancellation/deadline;
- target list is canonicalized and deduplicated;
- mixed/invalid targets are rejected according to current scope rules;
- persistent SMTP session is reused where supported;
- connection-churn scenario is bounded;
- interval is enforced by the actual-SEND pacing gate;
- retry re-enters pacing/admission;
- cancellation releases worker, limiter, adaptive-concurrency, pool and send-gate resources;
- delivery ledger prevents duplicate logical deliveries;
- secrets never appear in logs or run artifacts.

## 8. Conclusion

`bhattsameer/Bombers` is a useful source-level reference for **counted repetition, target-set iteration, payload sources, interval-driven scenarios, and two different SMTP connection strategies**. Its offensive intent does not justify ignoring those mechanisms.

The correct load2 transfer is primarily `ADAPT`/`HARDEN`/`SIMULATE`: build explicit bounded stress scenarios on top of load2's existing authorization, scope, worker, limiter, adaptive-concurrency, SMTP-pool, actual-send pacing, cancellation and evidence infrastructure. Do not import unrestricted bombing or verification-bypass workflows.
