# AUDIT ROUND 4 — progress

Source of truth: `main`.

## Closed this round

### Security
- PathSecurity on EML, attachments, session log, client cert, profile, plugin loader.
- SEC-001: `ProtocolLogRedaction` + `SessionProtocolLogger` / `ProtocolPathObserver`.
- **AUTH detector wiring** (`AuthSecretDetectorWiringTests`): `SmtpClient(IProtocolLogger)` attaches `AuthenticationSecretDetector` to our logger — commit `7671a3c`, [CI #147](https://github.com/sirvan0010-alt/load2/actions/runs/34718413544).

### Concurrency composition (Phase 4)
Runner order remains:
`WaitBeforeSend → AdaptiveConcurrency → SMTP pool → AcquireSendSlot → SendAsync`

New tests in `CrossComponentConcurrencyTests`:
- `AdaptiveAndSendPace_ParallelCancel_NoPermitOrGateLeak` — cancel mid-flight → `Active==0`, send gate recoverable
- `AdaptiveAndSendPace_NoCancel_NeverExceedsConcurrency` — peak ≤ MaxConcurrency

Evidence: commit `7671a3c` · **CI #147 success**

## Still deferred / optional
- Full fake-SMTP AUTH LOGIN/PLAIN end-to-end log capture (MailKit detector heuristics on live AUTH lines).
- NET-AUDIT-001 live source-IP / proxy / IPv6 matrix (needs real endpoints).
- Phase 5 performance stress at production-scale MessageCount (optional).

## Next recommended
1. Optional: fake-SMTP AUTH E2E if deeper SEC-001 runtime proof is required.
2. Phase 5 light performance dry-run (high MessageCount, low concurrency) if desired.
3. Otherwise Phase 8 release gate checklist is largely already covered by existing CI + regression suite.
