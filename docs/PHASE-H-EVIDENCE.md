# Phase H — residual audit evidence (main)

Evidence is drawn only from tests and code on `main` as of this document.
Status vocabulary: **IMPLEMENTED + EVIDENCE** | **NOT VERIFIED** | **NOT IN SCOPE**.

## CONC-AUDIT-001 — Cross-component concurrency

**IMPLEMENTED + EVIDENCE**

| Evidence | Location |
|----------|----------|
| Adaptive + RateLimiter parallel cancel → no hang, `Active == 0` | `CrossComponentConcurrencyTests.AdaptiveAndRateLimiter_ParallelCancel_NoHangOrLeak` |
| ProxyRotator concurrent select never returns blocked endpoint after ban | `CrossComponentConcurrencyTests.ProxyRotator_AfterBan_ConcurrentSelectNeverReturnsBlocked` |
| Worker count ≤ MaxConcurrency under delayed DATA | `SmtpTestRunnerTests.BoundedConcurrency_DoesNotExceedConfiguredWorkerCount` |
| SmartPace global spacing with concurrency | `SmartPaceControllerTests.GlobalSpacing_WithConcurrency_RespectsInterval` |
| Adaptive limiter reset / permit accounting | `AdaptiveConcurrencyLimiterTests` |
| Circuit breaker sliding window + recovery | `CircuitBreakerTests` |
| Pool: cancelled rent, dispose while waiting, no permit leak | `SmtpConnectionPoolTests` |

Remaining stress of the *entire* runner state machine under multi-proxy + multi-IP is left under NET-AUDIT-001 (requires live sockets).

## CONC-AUDIT-002 — Core cancellation

**IMPLEMENTED + EVIDENCE**

| Evidence | Location |
|----------|----------|
| Cancel during transient retry → `Cancelled=true`, no hang | `SmtpTestRunnerTests.Cancellation_DuringRetryPath_CompletesWithoutHang` |
| Cancel prevents AutoRestart | `SmtpTestRunnerTests.Cancellation_PreventsAutoRestart` |
| Cancel returns partial Sent+Failed | `SmtpTestRunnerTests.Cancellation_ReturnsPartialResults` |
| SmartPace cancel releases reservation | `SmartPaceControllerTests.Cancellation_*` |
| RateLimiter cancelled reservation does not stack phantom delay | `RateLimiterTests.Cancelled*` |
| Pool: cancelled rent does not consume slot; dispose unblocks waiter | `SmtpConnectionPoolTests.CancelledRent_*`, `DisposeWhileRentIsWaiting_*` |
| Plugin pipeline observes cancellation | `MailPayloadPluginTests.Pipeline_ObservesCancellationBeforePluginExecution` |

## NET-AUDIT-002 — TLS matrix (offline)

**IMPLEMENTED + EVIDENCE** (validation + mapping; no live TLS handshake)

| Evidence | Location |
|----------|----------|
| Security mode → `SecureSocketOptions` mapping | `TlsMatrixTests.ToSocketOptions_MapsEachSecurityMode` |
| Implicit TLS requires port 465; STARTTLS rejects 465 | `TlsMatrixTests.Validation_*` |
| DryRun connectivity path returns without network | `TlsMatrixTests.DryRun_TestAsync_ReturnsWithoutNetwork` |

Live cert validation / client-cert handshake remains **NOT VERIFIED** without a real TLS peer (deferred with NET-AUDIT-001).

## NET-AUDIT-001 — Network interaction matrix

**NOT VERIFIED**

Requires real source-IP binding, proxy path, and IPv4/IPv6 matrix against reachable endpoints.
Source-only unit coverage exists for rotators and binding helpers (`IpV4RotatorTests`, `IpV6RotatorTests`, `IpBindingHelperTests`, `ProxyRotatorTests`) but is **not** end-to-end network evidence.

## SEC residual (linked to G)

| Item | Status | Evidence |
|------|--------|----------|
| SEC-AUDIT-001 AUTH redaction | **IMPLEMENTED + EVIDENCE** | `ProtocolLogRedactionTests` (detector + session log never writes plaintext secret) |
| SEC-AUDIT-002 path boundary | **IMPLEMENTED + EVIDENCE** (basic `..`) | `PathBoundaryTests`, `Validation.ContainsPathTraversal` — UNC/junction/symlink **NOT VERIFIED** |
| SEC-AUDIT-003 secret provenance | **NOT VERIFIED** | Needs full-tree credential scan beyond unit tests |
| SEC-AUDIT-004 `--unauthorized` | **IMPLEMENTED + EVIDENCE** | `AuthorizationGateTests`, Validation gate, GUI `MainForm.BuildOptions`, CI #106 |

## Decision for Phase H

Phase H is **closed for offline/runtime unit evidence** on CONC-001, CONC-002, and offline TLS mapping.
Open items that must not be marked FIXED without new evidence:

1. NET-AUDIT-001 live matrix
2. Live TLS handshake / client cert
3. UNC/symlink path edge cases
4. SEC-AUDIT-003 full secret provenance scan

No production code change is required solely to close the offline H evidence set.
