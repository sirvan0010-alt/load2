# Reconciliation: execution model (BUG-001/003/007/009)

## Policy

- **Do not force-push** `main` or `fix/bugs-001-003-007-009-execution-model`.
- Work happens on **`reconcile/execution-model`**, branched from current `main`.

## What main already had

- Bounded `Channel<int>` workers (`SmtpTestRunner`)
- `DeliveryLedger` + AutoRestart skip-accepted
- `AcquireSendSlotAsync` around real `SendAsync` (retry re-enters gate)
- `WaitBeforeSendAsync` for absolute blocks + per-recipient reserve
- SEC gates, plugins, full CI suite

## What the old fix branch uniquely contributed

- Exclusive `SemaphoreSlim` around actual-SEND spacing (prevents concurrent claim of the same slot)
- Focused ActualSendGate regression tests

## What this branch does

1. Starts from **current `main`** (not the divergent fix tip).
2. Ports the **exclusive send gate** into `SmartPaceController`.
3. Adds gate-focused tests without dropping main’s existing suite.

## Next steps

1. Wait for CI on `reconcile/execution-model`.
2. If green → PR into `main` (no force-push).
3. Only then mark BUG-001/003/007/009 FIXED with CI evidence.
4. Then BUG-002 work continues on a *new* branch from that merged main if further ledger work is needed.

## Obsolete path

`fix/bugs-001-003-007-009-execution-model` remains historical; do not merge it wholesale onto main (106 commits behind, older runner without ledger).
