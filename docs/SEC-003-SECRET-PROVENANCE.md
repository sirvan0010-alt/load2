# SEC-003 — Secret provenance (main)

**Status: IMPLEMENTED + EVIDENCE** (static full-tree scan, 2026-09-12)

## Scope

Scanned `main` working tree (C#, csproj, GitHub Actions, JSON/config) for:

- hardcoded `password` / `secret` / `api_key` / `token` string assignments
- connection-string style credentials
- AWS/GCP key shapes and PEM private keys
- `.env` / `*secret*` / `*credential*` files
- GUI default host/user/password fields

## Findings

| Check | Result |
|-------|--------|
| Hardcoded live passwords | **None** |
| PEM / cloud API keys | **None** |
| `.env` / credential files in repo | **None** |
| GUI defaults | `smtp.example.com`, empty password fields; secrets only when loaded from profile with explicit content |
| ProfileStore | passwords stripped on save unless `includeSecrets` |
| Protocol logs | redaction via `ProtocolLogRedaction` + `AuthenticationSecretDetector` (see `ProtocolLogRedactionTests`) |
| Test fixtures | intentional markers only (`SuperSecretPassword`, `secret-value`) inside redaction unit tests |
| Random attachment blobs | fixed 1×1 PNG/JPEG base64 placeholders in `RandomTestData` — not credentials |

## Residual risk (accepted)

- Operator-supplied passwords at runtime are expected; they must not be committed.
- Profiles saved with `includeSecrets=true` can persist secrets on disk — by design, opt-in.
- Live NET-AUDIT and environment-specific config outside the repository are out of scope.

## Related evidence

- `AuthorizationGate` / `--unauthorized` (SEC-004)
- `ProtocolLogRedactionTests` (SEC-001)
- Package upgrade MailKit/MimeKit 4.17.0 (NU1902)
