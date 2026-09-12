# Trehwmm/Email-Bomber-SMTP — source-level audit

Status: SOURCE-AUDITED — relevant SMTP execution source inspected on `main`.

## Verified source
- `Email Bomber/Form1.vb` — SHA `d47f5ecdcba8105f8fee6b933c2afb4e5c43c820`
- `README.md` — SHA `6c03b91c7a4ae124746d4ef2243a71acfc8ed546`

## Verified mechanisms
- Windows VB.NET desktop GUI.
- Up to five configured SMTP identities are started as separate `BackgroundWorker` instances.
- Each worker creates an SMTP client, configures port 587, credentials and TLS, then repeatedly calls `Send` until its configured count is reached.
- Cancellation is exposed through `CancelAsync()` and checked through `CancellationPending` inside each send loop.
- README explicitly describes simultaneous sending for multiple users and a configurable email count.

## Defects / observations
- Five workers and five sets of fields/counters are manually duplicated instead of represented by a bounded worker abstraction.
- Mutable shared fields (`sa1..sa5`, account names/passwords) make lifecycle/state handling fragile.
- Completion handling is attached to `BackgroundWorker1.RunWorkerCompleted`; it does not aggregate completion of all workers.
- `BackgroundWorker4_DoWork` builds the message `From` address from `name3` instead of `name4`.
- `BackgroundWorker5_DoWork` builds the message `From` address from `name3` instead of `name5`.
- SMTP endpoint is hardcoded to Gmail and port 587.
- Credentials are stored in mutable application fields; not suitable for load2's environment/secret configuration model.
- No central pacing, retry policy, structured result classification or adaptive concurrency.

## Transfer decisions
| Mechanism | Decision | Load2 treatment |
|---|---|---|
| Multiple SMTP identities | ADAPT | Bounded sender/session pool instead of five hard-coded workers |
| Persistent SMTP session | ADOPT/HARDEN | Keep MailKit persistent sessions and lifecycle management |
| Parallel workers | ADOPT/HARDEN | Existing bounded Channel/worker and concurrency controls |
| Finite message count | ADAPT | Scenario count with target scope, cancellation and limits |
| Cancellation | ADOPT | CancellationToken throughout pipeline |
| TLS / port configuration | ADAPT | Transport configuration, no hardcoded credentials |
| Central pacing | ADOPT/HARDEN | SmartPaceController / adaptive rate limiting |
| Structured outcomes | ADOPT/HARDEN | Delivery ledger / RunResult |
| Manual duplicated worker state | REJECT | Replace with reusable worker/session abstraction |
| Unrestricted bombing semantics | REJECT | Do not import as unrestricted public-target behavior |

## Conclusion
Useful evidence for multi-account SMTP concurrency, persistent sessions, cancellation and finite execution. The implementation itself should not be copied. The mechanisms map onto load2's async, bounded-worker, MailKit, CancellationToken and telemetry architecture.
