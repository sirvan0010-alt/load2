# yyasha/smsbomb — source-level mechanism audit

- Repository: `yyasha/smsbomb`
- Reviewed ref: `master`
- Reviewed revision: `36c907d1d296b20b2204a99f99ecce6fab42497a`
- Repository state: archived
- Audited sources: `sms_with_threading.py`, `sms_with_while.py`
- Audit status: COMPLETE for the executable SMS dispatch sources inspected.

## Verified mechanisms

| Mechanism | Evidence | Decision | load2 mapping |
|---|---|---|---|
| Multi-provider fan-out | `sms_with_threading.py` contains many concrete HTTP provider calls in one send operation | ADAPT | Provider/transport registry only; concrete public endpoints are not imported. |
| Thread-per-request execution | file creates `num_requests` Python threads and joins them | HARDEN | Retain bounded Channel/fixed workers; never copy unbounded thread-per-request fan-out. |
| Per-operation request calls | `requests.get/post` calls | REFERENCE | Generic transport abstraction only. |
| Broad exception handling | each provider call catches bare exceptions | HARDEN | Typed outcome classification and observable failures. |
| Input normalization | phone number is normalized into several formats | ADAPT | Canonical target normalization before queue admission. |
| Synthetic payload values | random username/password/name generation | REFERENCE | Deterministic test payload generation belongs in plugins. |

## Important finding

The source demonstrates a large provider fan-out and an explicit thread-per-request model. Neither is superior to load2's current architecture. The transferable mechanism is the **provider abstraction/fan-out concept**, redesigned around bounded workers, authorization, pacing, cancellation and health state.

No public SMS endpoint, OTP flow or provider credential is transferred into load2.

## Required tests for any later adaptation

- provider selection remains scope-validated;
- canonicalized targets are deduplicated;
- worker count remains bounded;
- cancellation stops all workers and releases resources;
- provider failures become typed outcomes;
- no concrete third-party abuse endpoints are embedded.
