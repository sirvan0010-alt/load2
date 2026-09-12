# MailLoadTester (load2) — plán

| Phase | Topic | Status |
|-------|--------|--------|
| A–F | Core bugs | ✅ FIXED |
| G | Security | 🟡 SEC-001 FIXED; **SEC-002 FIXED** (path traversal + X-* headers); SEC-003/004 next |
| H | Concurrency / TLS | 🟡 partial FIXED — CI #66/#68 |
| **I** | **Plugin / release** | ✅ **COMPLETE** |

## Phase I — Plugin / release
- I-1 plugin contract: ✅
- I-2 deterministic cancellation-aware pipeline: ✅
- I-3 optional `plugins/` discovery: ✅
- I-4 integrate pipeline into `BuildMessage` → SMTP SEND: ✅
- I-5 lifecycle/error policy: ✅ (`MailPayloadPluginException`, fail-fast, no plugin-only retry)
- I-6 integration/regression tests: ✅ (pipeline + loader isolation; CI full suite)
- I-7 release/package documentation: ✅ (`PLUGINS.md` + `src/MailLoadTester.Core/Plugins/README.md`)

## Phase G — Security
- SEC-001: ✅
- SEC-002: ✅ path traversal on file inputs + custom headers must be `X-*`
- SEC-003 / SEC-004: ⏳ CLI gate `--unauthorized`

## Remaining deferred
- NET-AUDIT-001 live source-IP/proxy/IPv6 matrix
- SEC-003 / SEC-004 full CLI gate

## Execution order after Phase I
1. Complete full SEC-003/SEC-004 CLI gate.
2. Close remaining Phase H audit evidence without changing already-fixed concurrency/TLS code unless fresh regression evidence appears.
3. Execute NET-AUDIT-001 only with real evidence; never infer network behavior from source code alone.
4. Run final security audit against `main`.
5. Run full Release build and complete regression suite in GitHub Actions.
6. Reconcile `PLAN.md` and `BUGS-AUDIT.md` to the evidence-backed final state.

## Safety boundary
The project remains an authorized, bounded mail-load tester. Plugin execution must not bypass authorization, pacing, concurrency, retry, or other safety gates and must not be used to implement uncontrolled mailbombing, flooding, spam, DoS, or DDoS behavior.
