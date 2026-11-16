# Google OAuth – implementation decision

Status: accepted technical direction for implementation
Target branch: `feature/tor-socks5-safe-mode`
Date: 2026-11-16

## Decision summary

Do not implement Google sign-in by obtaining the broad `https://mail.google.com/` scope and passing its access token to Gmail SMTP as XOAUTH2 for a public release.

Google's current OAuth verification guidance explicitly says that an application using SMTP only must not request the broad `mail.google.com` scope just to send messages. For send-only functionality, Google directs developers to migrate to the Gmail API and request the narrower `https://www.googleapis.com/auth/gmail.send` scope. The SMTP-only scope is restricted and creates an avoidable verification/security-assessment burden.

Therefore:
- Keep the existing SMTP transport for providers and credentials that support normal SMTP authentication (including provider app passwords).
- Add Google account sign-in as a distinct Gmail API transport, not as a disguised SMTP mode.
- Use the Gmail API `users.messages.send` endpoint for Google-authenticated sends.
- Label metrics and UI honestly: Gmail API sends are not SMTP sessions and must not be reported as SMTP protocol tests.
- Do not silently fall back from Gmail API to SMTP or to password authentication.

## OAuth client model

- A desktop OAuth client ID is a public identifier and may be embedded in the distributed desktop application.
- Never ship or request a client secret for the desktop client. Use Authorization Code with PKCE and a loopback redirect URI.
- Each Google account authorizes independently and receives its own tokens.
- Refresh tokens must be stored per Windows user using Windows DPAPI (CurrentUser) or Windows Credential Manager; never store them in profiles, source control, logs, reports, or plaintext settings.
- Provide a disconnect/revoke action and remove locally stored credentials on sign-out.
- The OAuth client ID for the current project is for development/testing. Create a separate Google Cloud project/client for production before public release.

## Google Cloud testing and production

The current OAuth consent configuration is External / Testing. It is suitable for development, but:
- only explicitly listed test users can authorize;
- external Testing refresh tokens expire after seven days for Gmail scopes;
- it is not a public distribution configuration.

For public distribution, use a separate production project, complete Branding and Audience, publish to Production, and submit the requested scopes for Google's verification as required. The app must also publish a truthful privacy policy and explain why it needs the Gmail send permission.

## Required implementation phases

1. OAuth foundation:
   - Desktop Authorization Code + PKCE.
   - Random state and nonce validation.
   - Loopback callback bound only to 127.0.0.1 with a short timeout.
   - Exchange and refresh tokens without a client secret.
   - Verify the signed-in Google account identity and email.
2. Credential security:
   - Per-Windows-user encrypted refresh-token store.
   - Redaction tests for access/refresh tokens and authorization headers.
   - Sign-out/revoke and credential deletion.
3. Gmail API transport:
   - Build RFC 5322/MIME content using the existing MimeKit message builder.
   - Serialize MIME and encode base64url.
   - Send through Gmail API with `gmail.send`.
   - Handle API quotas, 401 refresh/retry once, 403 scope/policy errors, 429 and transient 5xx with bounded retry/backoff.
   - Do not route Gmail API requests through the SMTP connection pool or count them as SMTP protocol events.
4. GUI and tests:
   - Add a clearly labelled “Přihlásit přes Google” action and signed-in account state.
   - Keep Gmail API transport separate from SMTP host/port/auth method controls.
   - Add deterministic tests using a fake HTTP handler; no live Google credentials in CI.
5. Release readiness:
   - Use a separate production OAuth project/client.
   - Complete Google verification and policy materials before claiming public Gmail OAuth support.

## Official Google references

- OAuth 2.0 for native apps / desktop clients:
  https://developers.google.com/identity/protocols/oauth2/native-app
- OAuth refresh-token behavior in Testing:
  https://developers.google.com/identity/protocols/oauth2
- Gmail SMTP/IMAP OAuth XOAUTH2:
  https://developers.google.com/workspace/gmail/imap/xoauth2-protocol
- Google OAuth restricted-scope verification:
  https://developers.google.com/identity/protocols/oauth2/production-readiness/restricted-scope-verification
- Google OAuth verification FAQ (explicit SMTP-only / minimum-scope guidance):
  https://support.google.com/cloud/answer/13463817
- Gmail API sending scope:
  https://developers.google.com/workspace/gmail/api/auth/scopes
