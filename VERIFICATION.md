# Verification evidence — load2

**FIXED** only with commit SHA + green CI run URL.

## Phase A–F — FIXED
See prior CI #42–#60 (execution model, ledger, proxy, IPv4, Direct MX).

## Phase G — Security

### SEC-AUDIT-001 AUTH secret redaction — FIXED

| Item | Detail |
|------|--------|
| Helper | `ProtocolLogRedaction.DecodeClientOrServer` |
| Wired | `SessionProtocolLogger`, `ProtocolPathObserver` |
| Proof | `ProtocolLogRedactionTests` (plain, range, prefix, session file) |
| Commits | `6cb1ac3` helper+tests · `4c74f61` logger wiring |

MailKit sets `IAuthenticationSecretDetector` on the logger; we now mask detected ranges with `*` before any session-log or path-observer text is stored.

### SEC-AUDIT-002 path boundary — OPEN (partial)
`..` rejected for EML/session-log/profile paths. UNC/junction matrix still open.

### SEC-AUDIT-003 secret provenance — OPEN
No hardcoded SMTP passwords found in Core; full provenance still open.

### SEC-AUDIT-004 `--unauthorized` — DEFERRED
Listed in backlog (FEAT-003); app is GUI-primary. DryRun skips `SmtpConnectionPool` construction (`if (!options.DryRun)`).

## Next
CONC / TLS matrix · remaining SEC · Plugin/release
