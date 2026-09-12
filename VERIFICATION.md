# Verification evidence — load2

Living log. A defect is **FIXED** only with commit SHA + green CI run URL.

## Phase A — Execution model (BUG-001, BUG-003, BUG-007, BUG-009)

**Status: FIXED** (2026-09-12)

| Sub | Requirement | Evidence |
|-----|-------------|----------|
| **1.1** bounded workers | `MaxConcurrentData ≤ MaxConcurrency` | commit `20a3f19` · [CI #43](https://github.com/sirvan0010-alt/load2/actions/runs/34663469078) · `BoundedConcurrency_DoesNotExceedConfiguredWorkerCount` |
| **1.2** first SEND immediate | first slot ≪ interval | `76ac896` · [CI #42](https://github.com/sirvan0010-alt/load2/actions/runs/34663325465) · `FirstSend_IsImmediate_SecondRespectsInterval` |
| **1.3** retry → real SEND | 451 then 2nd DATA | `09f31cf` · [CI #44](https://github.com/sirvan0010-alt/load2/actions/runs/34663883453) · `TransientRetry_IssuesSecondDataAttempt_ThroughSendPath` |
| **1.4** actual-SEND gate | interval between SEND | CI #42 · SmartPace `AcquireSendSlotAsync` before `SendAsync` |
| **1.5** cancellation | `Cancelled=true` mid-flight; no AutoRestart | fix `d09bb6f` · tests `Cancellation_DuringRetryPath_*`, `Cancellation_PreventsAutoRestart` · [CI #51](https://github.com/sirvan0010-alt/load2/actions/runs/34664532342) |
| **1.6** build integrity | full suite green | CI #51 (122 passed, 0 skipped, no filter) |

### Implementation anchors
- `SmtpTestRunner`: `Channel.CreateBounded` + `workerCount = MaxConcurrency`
- `SmartPaceController.AcquireSendSlotAsync` inside retry loop, immediately before `SendAsync`
- Worker OCE sets `cancelled = true` (not swallowed silently)

---

## Phase B — Delivery ledger / AutoRestart (BUG-002)

**Status: FIXED** (2026-09-12) — with residual note on ambiguous network outcomes (conservative Failed, not Accepted)

| Sub | Requirement | Evidence |
|-----|-------------|----------|
| **2.1** message identity | stable index 1..N | `DeliveryLedger` |
| **2.2** states | Pending→InFlight→Accepted/Failed | unit tests |
| **2.3** skip Accepted on restart | `TryClaim` false after Accept | `Accepted_message_cannot_be_claimed_again` |
| **2.4** aggregate counters | Sent = `CountAccepted()` | `RunAsync` return |
| **2.5** no duplicate send | 2 msgs, 1 transient, restart | `AutoRestart_DoesNotResendAlreadyAcceptedLogicalMessage` · DataAttempts=3, Accepted=2 |
| **2.6** cancel recovery | InFlight→Failed reclaimable; cancel blocks restart | `InFlight_MarkFailed_AllowsReclaim_*` · `Cancellation_PreventsAutoRestart` · CI #51 |

### Residual (not blocking FIXED)
Ambiguous network drop after DATA without response remains **Failed** (not Accepted) by design — conservative, matches BUG-002 wording.

---

## Next (Phase C+)

3. BUG-004 Proxy rotation  
4. BUG-005 IPv4 /31 /32  
5. BUG-006 Repo integrity (delete one-shot patch workflows)  
6. BUG-008 Direct MX  
7. SEC-AUDIT  
8. CONC/NET/TLS  
9. Plugin / release  
