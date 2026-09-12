# Verification evidence — load2

Living log. A defect is **FIXED** only with commit SHA + green CI run URL.

## Phase A — Execution model (BUG-001, BUG-003, BUG-007, BUG-009)

**Status: FIXED** (2026-09-12)

| Sub | Evidence |
|-----|----------|
| **1.1** bounded workers | `20a3f19` · [CI #43](https://github.com/sirvan0010-alt/load2/actions/runs/34663469078) |
| **1.2** first SEND immediate | `76ac896` · [CI #42](https://github.com/sirvan0010-alt/load2/actions/runs/34663325465) |
| **1.3** retry → 2nd DATA | `09f31cf` · [CI #44](https://github.com/sirvan0010-alt/load2/actions/runs/34663883453) |
| **1.4** SEND gate ordering | CI #42 · `AcquireSendSlotAsync` before `SendAsync` |
| **1.5** cancellation | `d09bb6f` · [CI #51](https://github.com/sirvan0010-alt/load2/actions/runs/34664532342) |
| **1.6** build | CI #51 full suite, no filter |

---

## Phase B — Delivery ledger / AutoRestart (BUG-002)

**Status: FIXED** (2026-09-12)

| Sub | Evidence |
|-----|----------|
| **2.1–2.4** ledger + aggregates | `DeliveryLedger` + unit tests |
| **2.5** no duplicate send | `AutoRestart_DoesNotResendAlreadyAcceptedLogicalMessage` |
| **2.6** cancel recovery | `InFlight_MarkFailed_*` · `Cancellation_PreventsAutoRestart` · CI #51 |

Residual: ambiguous network drop after DATA stays **Failed** (conservative).

---

## Phase C — Proxy rotation (BUG-004)

**Status: FIXED** (2026-09-12)

| Item | Evidence |
|------|----------|
| Random samples only eligible | `PickRandomEligible` reservoir over unblocked |
| Never returns blocked | `Random_NeverReturnsBlockedEndpoint` |
| Exhaustion → null | `AllBlocked_ReturnsNull_RandomAndRoundRobin` |
| Single eligible | `Random_WithSingleEligible_AlwaysReturnsThatOne` |
| Commit | `a6d59a1` · [CI #54](https://github.com/sirvan0010-alt/load2/actions/runs/34664736578) |

---

## Phase E — Repo hygiene (BUG-006 partial)

**Status: partial** — one-shot `patch-*.yml` removed (`154621d`). Keep `ci.yml` only.

---

## Next

4. **BUG-005** IPv4 `/31` `/32` (IpV4Rotator)  
6. **BUG-008** Direct MX hardening  
7. SEC-AUDIT  
8. CONC / TLS  
9. Plugin / release  
