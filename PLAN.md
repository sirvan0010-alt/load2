# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| G | Security | 🟡 SEC-001 FIXED; SEC-002 partial |
| H | Concurrency / TLS | 🟡 partial FIXED — CI #66 |
| **I** | **Plugin / release** | 🟡 **IN PROGRESS — I-1…I-5 done; I-6 next** |

## Phase I — Plugin / release
- I-1 plugin contract: ✅
- I-2 deterministic cancellation-aware pipeline: ✅
- I-3 optional `plugins/` discovery: ✅
- I-4 integrate pipeline into `BuildMessage` → SMTP SEND: ✅
- I-5 lifecycle/error policy: ✅ (`MailPayloadPluginException`, fail-fast, no plugin-only retry)
- I-6 integration/regression tests: ⏳
- I-7 release/package documentation: ⏳

## Remaining deferred
- NET-AUDIT-001 live source-IP/proxy/IPv6 matrix
- SEC-003 / SEC-004 full CLI gate

## Execution order after Phase I
1. Complete I-6 through I-7.
2. Complete SEC-002 and full SEC-003/SEC-004 CLI gate.
3. Close remaining Phase H audit evidence without changing already-fixed concurrency/TLS code unless fresh regression evidence appears.
4. Execute NET-AUDIT-001 only with real evidence; never infer network behavior from source code alone.
5. Run final security audit against `main`.
6. Run full Release build and complete regression suite in GitHub Actions.
7. Reconcile `PLAN.md` and `BUGS-AUDIT.md` to the evidence-backed final state.

## Safety boundary
The project remains an authorized, bounded mail-load tester. Plugin execution must not bypass authorization, pacing, concurrency, retry, or other safety gates and must not be used to implement uncontrolled mailbombing, flooding, spam, DoS, or DDoS behavior.
