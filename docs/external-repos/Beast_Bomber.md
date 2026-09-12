# Beast_Bomber — source-level deep dive

- URL: https://github.com/un1cum/Beast_Bomber
- Audited ref: `main`
- Evidence reviewed: `beast.py`, `core/email_spam/email_attack.py`, `core/etc/settings.py`, repository tree
- Audit status: **SOURCE-AUDITED (partial transport scope; entry point, email path and settings inspected)**
- License: repository contains `LICENSE.md`; exact license text should be checked before copying any implementation.

## Why this repository is retained

This repository is useful because it puts several execution transports behind one interactive orchestrator and exposes a simple lifecycle from menu selection to transport-specific execution. The source confirms that the project contains separate modules for SMS, email, Telegram, Discord and a DDoS component. The root entry point constructs those components and dispatches to them from one menu.

This is valuable to `load2` primarily as an **orchestration reference**, not as code to copy.

## Source map

| Area | File / symbol | Observed behavior | load2 relevance | Decision |
|---|---|---|---|---|
| Global orchestration | `beast.py` / `BeastBomber.__init__` | Instantiates one object per transport plus settings; keeps the UI entry point separate from transport modules. | Supports a future transport/provider registry instead of hard-coding transport selection throughout the runner. | **ADOPT architecture** |
| Dispatch | `beast.py` / `BeastBomber.main` | Numeric menu dispatches to `start_sms`, `email_start`, `start_telegram`, `start_discord`, `start_ddos`, or settings. | Demonstrates a common scenario/command dispatcher. | **EXTRACT** |
| Email execution | `core/email_spam/email_attack.py` / `EmailAttack.email_thread` | Iterates configured accounts and targets for a time window; derives an SMTP host from sender-domain suffixes; constructs MIME message; opens SMTP connection; EHLO/STARTTLS/login/send/quit; counts success/failure. | Useful comparison for transport lifecycle, error classification and sender-account abstraction. | **REFERENCE / ADAPT concepts only** |
| Email configuration | `core/email_spam/email_attack.py` / `EmailAttack.email_start` | Interactive target/message/subject/thread-count/time inputs. | Confirms the useful concept of a scenario containing target set, payload, concurrency and duration. | **ADOPT architecture** |
| Parallel execution | `EmailAttack.email_start` | Starts one Python `Thread` per requested thread and each worker loops until the time limit. | Historical comparison with `load2`'s bounded `Channel<T>` worker model. | **HARDEN / do not copy** |
| Synchronization | `EmailAttack.__init__`, `stat` | Uses a `Lock` around status output, but counters are also mutated as strings outside a clearly centralized atomic model. | Reinforces need for thread-safe counters/metrics in `load2`. | **HARDEN** |
| Settings | `core/etc/settings.py` / `Settings.settings_main` | Recursive menu flow, proxy update, language change and cache cleanup. | UI/settings separation is useful; recursive menu and filesystem cleanup are not suitable as a core execution pattern. | **EXTRACT / REFERENCE** |

## Exact email execution flow observed

The source shows the following sequence inside `EmailAttack.email_thread`:

```text
worker thread
  -> time-window loop
  -> configured sender accounts
  -> configured targets
  -> infer SMTP host from account domain
  -> split account into sender/password
  -> create MIME multipart message
  -> SMTP(host, 587)
  -> EHLO
  -> STARTTLS
  -> EHLO
  -> AUTH LOGIN
  -> sendmail
  -> quit
  -> success/failure counters
```

The implementation creates and tears down an SMTP connection for each attempted delivery. `load2` already has a substantially stronger persistent-session/pool architecture, so this is a comparison point rather than an implementation target.

## What is technically interesting for load2

### 1. Transport-specific adapter boundary

`beast.py` proves a simple pattern: the top-level application does not implement SMTP/SMS/etc. itself; it calls transport-specific objects. `load2` should retain this separation while using its existing SMTP and Direct-MX implementations and plugin architecture.

**Potential implementation:** introduce a transport/provider registry only after auditing the current runner interfaces. Do not create a duplicate abstraction merely because this repository has one.

### 2. Scenario-oriented execution parameters

The email UI collects a target list, message, subject, thread count and duration before starting execution. Conceptually this maps well to a `Scenario` model containing:

- target scope;
- payload/plugin;
- message count or duration;
- concurrency;
- rate policy;
- retry policy;
- transport;
- stop/cancellation policy;
- output/report policy.

`load2` should model these values explicitly rather than reproducing interactive input loops.

### 3. Transport lifecycle comparison

The SMTP sequence is useful as a regression checklist: connection, EHLO, TLS negotiation, authentication, DATA/send and close. `load2` should additionally measure and report the phases already represented by its timing breakdown, and should retain persistent sessions where appropriate.

### 4. Failure accounting

The source increments success/failure counts on the broad exception boundary. That is too coarse for `load2`. The transferable idea is the existence of per-attempt outcome accounting; the implementation should classify failures into the existing/desired taxonomy such as transient network, SMTP transient, rate-limited, authentication, TLS, policy, permanent and cancelled.

### 5. Concurrency lesson

The repository demonstrates why "threads" must not be confused with controlled concurrency. It starts one OS/Python thread for every configured thread count and lets each worker repeatedly execute the whole account × target loop for a duration. `load2`'s bounded `Channel<T>` + fixed workers + adaptive concurrency + actual-SEND gate is the preferred architecture.

## Security / reliability observations

The audited email source has several patterns that must **not** be imported into `load2`:

- credentials are parsed directly from an account string using `account.split(':')`;
- authentication secrets are therefore part of the input representation;
- SMTP host selection is hard-coded to a small set of sender domains with a fallback;
- exceptions are swallowed by a bare `except` and converted only to a failure counter;
- SMTP connections are recreated for each send;
- there is no visible `CancellationToken` equivalent;
- execution is time-based and continuously repeats the account/target loops;
- concurrency is unbounded relative to system resources beyond the user-provided thread count;
- the entry point contains a DDoS transport in addition to application-layer messaging transports.

These observations are evidence for `HARDEN`, not reasons to discard the repository as a whole.

## Controlled capabilities worth extracting

1. **Scenario model** — target + payload + concurrency + duration/count + transport.
2. **Transport registry** — common discovery/dispatch boundary for SMTP and future authorized transports.
3. **Per-transport capability metadata** — transport declares what it supports instead of the UI guessing.
4. **Outcome aggregation** — common success/failure/result reporting across transports.
5. **Lifecycle metrics** — connect/TLS/auth/send/close timing where applicable.
6. **Cancellation-aware worker execution** — retain the useful worker model while replacing raw threads with bounded async workers.
7. **Health state** — transport endpoint/account health should feed quarantine/cooldown rather than repeatedly retrying a known-dead endpoint.

## What is explicitly not imported

The repository's multi-channel spam/DDoS execution paths are not copied into `load2` as unrestricted public-target launchers. The audit nevertheless records their existence because the architectural lessons—transport separation, dispatch, common results, concurrency control and scenario modeling—are relevant.

No public provider inventory, credential collection, rate-limit bypass, stealth behavior or arbitrary target-discovery mechanism is imported.

## Mapping to current load2

| Beast_Bomber observation | Current load2 state / target |
|---|---|
| Separate transport objects | Existing SMTP / Direct-MX separation; future provider abstraction candidate |
| Interactive scenario inputs | Existing options/GUI; future explicit Scenario model |
| Per-thread loops | Replaced by bounded `Channel<T>` workers |
| SMTP connect per message | Improved by persistent SMTP session / connection pool |
| Broad exception counter | Improve with structured failure classification |
| Basic status counters | Extend with timing breakdown / JSON reporting |
| Settings/menu recursion | Keep UI concerns outside network execution |
| Multiple transport families | Study as provider/transport architecture; implement only supported authorized transports |

## Evidence

The source tree exposes `beast.py`, `core/email_spam/email_attack.py`, `core/etc/settings.py`, and separate `ddos_attack`, `discord_spam`, `sms_spam`, and `telegram_spam` directories. The root class constructs these components and dispatches to their start methods. The email source explicitly uses `Thread`, `Lock`, `smtplib.SMTP`, STARTTLS, login, MIME construction and repeated account/target iteration.

## Audit conclusion

**Decision: ADOPT architecture + HARDEN existing concepts.**

The strongest transferable value is the separation of transport modules and the scenario/dispatch concept. The email implementation itself is materially weaker than `load2` in connection reuse, cancellation, pacing, structured failures and concurrency control. It should remain a reference for comparison and should not replace the current SMTP execution engine.

**Next audit target:** continue through the remaining requested repositories using the same source-level format; do not mark a repository "fully audited" until its relevant entry points and execution modules have been inspected.