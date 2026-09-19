# slowhttptest — source mechanism audit

Repository: `shekyan/slowhttptest`
Pinned revision: `bbd33de733ccb5d7c87ffefebe1373d035a573a1`

This audit records mechanisms only. It does not treat attack orchestration as a reusable design requirement.

## M-001 — connection lifecycle control
**Evidence:** `ng/include/slowhttp/attack.hpp`

The source exposes explicit per-connection lifecycle hooks (`on_open`, `on_connect`, `on_timer`, `on_readable`, `on_close`) and keeps attack logic independent of I/O. This is a strong separation boundary.

**Decision: ADAPT**

Use the lifecycle/state-machine idea where it improves load2 connection/session state handling. Do not copy HTTP attack states into the SMTP domain.

## M-002 — connection-rate control
**Evidence:** `ng/include/slowhttp/config.hpp`, `ng/src/engine/engine.cpp`

Configuration separates requested concurrent connections from the rate at which new connections are established. The engine also bounds ramp work per event-loop pass and uses a bounded wake-up interval.

**Decision: HARDEN**

load2 already has `MaxConcurrency` and `SmartPaceController`. Do not add a second pacing system. Extract the invariant that connection establishment rate and active concurrency are separate dimensions, then verify every connection-creating path obeys the existing gate.

## M-003 — partial / slow I/O
**Evidence:** `ng/include/slowhttp/attack.hpp`

The attack interface can suppress automatic readable-event draining and explicitly request read events. This supports controlled partial-I/O experiments.

**Decision: ADAPT**

Keep as a scenario/diagnostic capability only. It must be bounded, cancellable and authorization-gated; no uncontrolled attack orchestration is imported.

## M-004 — timeout and probe model
**Evidence:** `ng/include/slowhttp/config.hpp`, `ng/src/engine/probe.cpp`

The implementation has explicit connection/probe timeouts, independent probe scheduling, baseline probes, and a probe path that can be separated from the load path. A probe timeout is represented as an observed denial rather than silently retried away.

**Decision: ADOPT**

Strengthen load2's observability with an explicit independent health/probe model where useful. Probe results must remain separate from SMTP delivery outcome classification.

## M-005 — worker/concurrency model
**Evidence:** `ng/src/engine/engine.cpp`, `ng/include/slowhttp/reactor.hpp`

The engine uses a reactor/event-loop abstraction and a separate closer pool so potentially blocking socket close work does not block the event loop. Work per maintenance sweep/ramp is explicitly bounded.

**Decision: ADAPT**

Apply the principle of bounded per-pass work and isolation of potentially blocking cleanup to load2's connection/session teardown where measurements show value. Preserve the existing async C# architecture rather than porting the C++ reactor.

## M-006 — scenario parametrization
**Evidence:** `ng/include/slowhttp/config.hpp`

The configuration groups target, mode, connection/rate, timeout, protocol, proxy, probe, capacity and reporting parameters. Defaults and derived values are represented centrally.

**Decision: ADAPT**

Use a single validated scenario model with explicit defaults and derived fields. Keep secrets external and preserve `--unauthorized` and existing hard safety/test gates.

## M-007 — measurement and reporting
**Evidence:** `ng/include/slowhttp/event_log.hpp`, `ng/include/slowhttp/report.hpp`

`EventLog` is the common source for human and machine reports. It records probe samples, connection samples, capacity levels, run metadata and a four-state verdict. JSON and HTML are rendered from the same collected data.

**Decision: ADOPT**

The architectural principle maps directly to `MailTestResult`, `RunObservability` and `RunReport`: one authoritative run model, multiple projections, and explicit inconclusive states when evidence is insufficient.

## Transfer summary

| Mechanism | Decision | load2 direction |
|---|---|---|
| M-001 lifecycle | ADAPT | explicit connection/session state boundaries |
| M-002 rate vs concurrency | HARDEN | one pacing system + separate concurrency gate |
| M-003 partial I/O | ADAPT | bounded authorized diagnostic scenario |
| M-004 timeout/probe | ADOPT | independent health/probe evidence |
| M-005 bounded scheduling/cleanup | ADAPT | bounded async work and non-blocking teardown |
| M-006 scenario model | ADAPT | central validated scenario configuration |
| M-007 measurement/reporting | ADOPT | authoritative run model + projections |

All decisions above are mechanism-level decisions. No attack-specific orchestration is being claimed as a load2 requirement.
