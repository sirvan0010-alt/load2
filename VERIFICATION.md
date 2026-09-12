# Verification evidence — load2

**FIXED** only with commit SHA + green CI run URL.

## Phase A — Execution model (001/003/007/009) — FIXED
CI #42–#51.

## Phase B — DeliveryLedger / AutoRestart (002) — FIXED
CI #51.

## Phase C — Proxy (004) — FIXED
`a6d59a1` · [CI #54](https://github.com/sirvan0010-alt/load2/actions/runs/34664736578)

## Phase D — IPv4 /31 /32 (005) — FIXED
`4f89659` · [CI #56](https://github.com/sirvan0010-alt/load2/actions/runs/34664949650)

## Phase F — Direct MX (008) — FIXED (candidate pending this CI)

| Layer | Behavior |
|-------|----------|
| `Validation` | rejects mixed recipient domains when `DirectMxDelivery` |
| `DirectMxRouting.RequireSingleRecipientDomain` | same check at runner entry |
| `SmtpTestRunner` | resolves MX for that unique domain only (not `Recipients[0]` alone) |

Commits: `6d753b2` (helper+tests) · `49aa332` (runner)  
Tests: `DirectMxRoutingTests` (single/mixed/normalize + Validation boundary)

## Phase E — Repo hygiene — partial
One-shot patch workflows removed where possible.

## Next
SEC-AUDIT · CONC/TLS · Plugin/release
