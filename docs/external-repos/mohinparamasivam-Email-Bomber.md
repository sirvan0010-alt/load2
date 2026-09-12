# External Repo Audit — mohinparamasivam/Email-Bomber

**Status:** SOURCE-AUDITED — execution source inspected; reusable SMTP/session mechanisms mapped to load2. No unrestricted bombing behavior imported.

## Source of evidence
- `README.md` — blob `bd3d12d1aecaec76daf6ff5d905ad770ff699e6c`
- `emailbomber3.py` — blob `6ebd67323280bedfb4db077507e934a363c01ce4`

## Verified execution model
`emailbomber3.py` implements an interactive SMTP sender with:

- target address input
- sender address and password input
- configurable message count
- message/body input
- configurable SMTP server and port
- default Gmail SMTP configuration using port 587
- EHLO → STARTTLS → LOGIN session setup
- one persistent SMTP connection reused for repeated `sendmail` calls
- one-second delay between sends
- explicit handling for `SMTPAuthenticationError`
- explicit handling for `SMTPConnectError`
- `KeyboardInterrupt` handling
- continuation path when STARTTLS fails

The README explicitly describes the project as an email bomber/spam utility and presents it as educational. The source therefore must be evaluated mechanism-by-mechanism rather than copied operationally.

## Mechanism mapping to load2

| Mechanism | Decision | Load2 treatment |
|---|---|---|
| Persistent SMTP connection | ADOPT/HARDEN | Matches the existing SMTP session/pool architecture. |
| EHLO / STARTTLS lifecycle | ADAPT | MailKit/MimeKit transport lifecycle; TLS policy remains explicit. |
| Configurable SMTP host/port | ADAPT | Transport configuration, never hard-coded credentials. |
| Repeated sends over one session | ADAPT | Controlled scenario execution through the bounded worker pipeline. |
| One-second fixed delay | ADAPT | Replace fixed sleep with `SmartPaceController` and policy-based pacing. |
| Configurable count | ADAPT | Scenario count bounded by authorization/scope/cancellation controls. |
| SMTP authentication failures | ADOPT/HARDEN | Map to typed/structured failure classification and telemetry. |
| SMTP connection failures | ADOPT/HARDEN | Feed retry/circuit-breaker/provider-health logic. |
| Keyboard interrupt | ADAPT | `CancellationToken` and coordinated shutdown. |
| Interactive password entry | REJECT pattern | Environment/secret-provider configuration only. |
| Unrestricted bomber loop | REJECT operational behavior | Do not import public-target spam semantics. |
| STARTTLS failure → continue insecurely | REJECT | Load2 should obey configured TLS/security policy rather than silently downgrade. |

## Important architectural finding

The strongest transferable feature is the **persistent SMTP session** combined with a clear setup/send lifecycle. This is already aligned with load2's current architecture and reinforces the decision to keep connection establishment, authentication and message generation separate from the actual send gate.

The fixed one-second sleep is not transferred literally. Load2's global pacing must remain centralized so retries, concurrent workers and adaptive concurrency cannot bypass the actual-send gate.

## Security findings

1. Credentials are requested interactively by the external project. Load2 must not adopt that pattern.
2. The external source allows continuing after a STARTTLS failure. Load2 must not silently downgrade from the configured TLS security policy.
3. The external project is designed for repeated unsolicited delivery. Only the underlying controlled execution mechanisms are transferable.
4. Error handling is more useful than a generic exception because authentication and connection failures have different recovery implications.

## Load2 implementation targets

- Keep `SmtpSessionPool`/persistent-session behavior.
- Keep `SmartPaceController` as the central actual-send gate.
- Keep retry pacing through the same gate.
- Map SMTP authentication/connectivity failures into structured `DeliveryResult`/telemetry categories.
- Keep credentials sourced from environment/secret configuration.
- Preserve `CancellationToken` throughout connection, pacing and send paths.
- Treat TLS downgrade as a policy violation rather than an automatic fallback.

## Conclusion

**Decision: ADOPT/HARDEN the engineering mechanisms; REJECT the abuse-oriented execution semantics.** This repository confirms that persistent SMTP sessions, explicit TLS/authentication phases, configurable transport endpoints, structured SMTP failure handling and controlled repetition are useful load2 mechanisms. They should be implemented through the existing bounded, authorized scenario engine rather than as a standalone bomber path.
