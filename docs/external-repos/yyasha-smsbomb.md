# yyasha/smsbomb — external repository audit

**Status:** SOURCE-AUDITED

## Source of evidence

Repository: `yyasha/smsbomb`
Branch: `master`

Verified tree contains:
- `README.md` — blob `19a0b6f8bead1b6127ed32b3c83ef4d99e4f57e0`
- `sms_with_threading.py` — blob `c46fbff3b43985b2b8022c0c9a2ed86f90d985f2`
- `sms_with_while.py` — blob `cfd1b0bcb8e0c060df7355b792ce79947c268d17`

README explicitly documents two execution modes: multithreading and continuous/circular execution.

## Verified execution model

### `sms_with_threading.py`

The source imports `requests` and `threading`, accepts a phone number interactively, normalizes several number formats, and defines a `sent()` routine containing a large sequence of direct HTTP requests to third-party registration/authentication endpoints. The source therefore implements an endpoint-fan-out SMS/OTP abuse mechanism rather than a generic SMS transport.

The execution section is explicit:
- `num_requests = 1000`
- creates one `threading.Thread(target=sent)` for every request iteration
- starts every thread immediately
- stores every thread in `all_threads`
- joins all threads afterwards
- a mutex protects only the demonstration thread counter

This is an unbounded/uncontrolled thread-per-request fan-out pattern from the perspective of load2 architecture. It is not a bounded worker pool.

The source also uses broad bare `except` blocks around individual HTTP operations and does not expose a cancellation mechanism, structured transport result, central rate limiter, or timeout policy in the shown execution path.

### `sms_with_while.py`

The source implements a `while True` execution loop around the same class of direct third-party HTTP endpoint calls. It therefore represents continuous repetition without a finite termination condition or cancellation boundary.

## Mechanism extraction

| Mechanism | Decision for load2 | Rationale |
|---|---|---|
| Phone/target normalization | HARDEN / ADAPT | Useful as canonical target normalization, but must remain inside explicit authorized scope. |
| Multiple transport/provider endpoints | ADAPT | Generalize into a provider/transport registry, not a static public endpoint list. |
| Endpoint fan-out | ADAPT | Reuse as bounded provider fan-out through the existing worker architecture. |
| Thread-per-request | REJECT | Replace with bounded workers/Channel-based execution. |
| Mutex-protected shared counters | HARDEN | Prefer thread-safe structured telemetry/Interlocked/Volatile where appropriate. |
| Continuous `while True` repetition | SIMULATE / ADAPT | Model as duration/deadline scenario with CancellationToken and hard limits. |
| Finite request count | ADAPT | Already fits controlled scenario limits. |
| HTTP transport | EXTRACT / ADAPT | Generic HTTP transport can be useful for authorized diagnostics/provider integrations. |
| Broad `except` | HARDEN | Replace with typed failures and structured result classification. |
| Static public SMS/OTP endpoints | REJECT | Do not import third-party abuse targets or their request recipes. |
| Browser-like headers / service-specific payloads | REFERENCE | Only generic protocol-header support belongs in a transport abstraction. |
| CAPTCHA/OTP-related request parameters | REJECT | Not transferable as an abuse/bypass mechanism. |
| Direct public-target bombing semantics | REJECT | Keep execution behind load2 authorization, target scope, pacing, cancellation and telemetry. |

## What is genuinely useful for load2

The useful engineering lesson is the **fan-out abstraction**, not the concrete bomber endpoints:

```text
Scenario
  -> Authorized TargetSet
  -> ProviderRegistry
  -> Bounded WorkQueue
  -> Bounded Workers
  -> Transport
  -> Retry / timeout / pacing
  -> Structured Result
  -> Telemetry / Ledger
```

For an SMS-capable future transport this means `SmsTransport` should be provider-neutral and provider-specific request formats should live behind a plugin/provider boundary. Health, quarantine, retry/backoff and rate limiting belong to the shared orchestration layer rather than being duplicated per endpoint.

## Security / quality findings

1. The repository directly targets many third-party authentication/registration flows.
2. `sms_with_threading.py` creates 1000 OS threads rather than using bounded concurrency.
3. `sms_with_while.py` has an unconditional infinite loop.
4. Network operations are wrapped in broad exception handlers, obscuring actual failure causes.
5. There is no CancellationToken-equivalent cancellation boundary.
6. There is no structured result ledger or reliable provider health model.
7. Service-specific endpoint recipes are tightly coupled to the executable code.

## Final classification

**REFERENCE / ADAPT / HARDEN** for reusable architecture.

The repository is fully source-audited. Its concrete public-target SMS/OTP bombing implementation is not transferred into load2. The transferable mechanisms are target normalization, provider abstraction, bounded fan-out, cancellation/deadline modeling, transport separation, structured outcomes, health-aware orchestration and central pacing.
