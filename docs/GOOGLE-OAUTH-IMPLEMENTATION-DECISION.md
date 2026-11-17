# Google OAuth – implementation decision

Status: accepted technical direction for implementation
Target branch: `feature/google-oauth-gmail-api`
Date: 2026-11-16

## Decision summary

Do not implement Google sign-in by obtaining the broad `https://mail.google.com/` scope and passing its access token to Gmail SMTP as XOAUTH2 for a public release.

Google's current OAuth verification guidance explicitly says that an application using SMTP only must not request the broad `mail.google.com` scope just to send messages. For send-only functionality, Google directs developers to migrate to the Gmail API and request the narrower `https://www.googleapis.com/auth/gmail.send` scope. The SMTP-only scope is restricted and creates an avoidable verification/security-assessment burden.

Therefore:
- Keep the existing SMTP transport for providers and credentials that support normal SMTP authentication (including provider app passwords).
- Add Google account sign-in as a distinct Gmail API transport, not as a disguised SMTP mode.
- Use the Gmail API `users.messages.send` endpoint for Google-authenticated sends.
- `gmail.send` is classified by Google as a **sensitive** scope (not non-sensitive and not restricted); a public app should plan for sensitive-scope OAuth verification. It avoids the much broader restricted `mail.google.com` scope and its additional security-assessment requirements.
- Gmail API sending is not an SMTP test. It does not measure SMTP connection/authentication/SMTP command behavior, and Gmail API quotas and anti-abuse limits must be respected; the app must not use the API path to evade Gmail sending limits.
- Label metrics and UI honestly: Gmail API sends are not SMTP sessions and must not be reported as SMTP protocol tests.
- Do not silently fall back from Gmail API to SMTP or to password authentication.

## OAuth client model

- A desktop OAuth client ID is a public identifier and may be embedded in the distributed desktop application.
- Desktop applications are public clients: any Client Secret distributed with them must not be treated as confidential. PKCE remains mandatory.
- The desktop application is a public OAuth client. It sends the public Client ID and uses Authorization Code + PKCE; it does not request, store, or transmit a Client Secret. Google's current Desktop Apps documentation marks `client_secret` as optional for the authorization-code token exchange.
- Each Google account authorizes independently and receives its own refresh token. Refresh tokens are stored per Windows user using DPAPI (`CurrentUser`) and are not written to source control, logs, reports, or plaintext settings.
- Provide a disconnect/revoke action and remove locally stored refresh-token credentials on sign-out.
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
   - Random OAuth state validation (OpenID userinfo is used; no ID-token nonce is consumed).
   - Loopback callback bound only to 127.0.0.1 with a short timeout.
   - Exchange and refresh tokens using the public Client ID without a Client Secret; keep PKCE mandatory.
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
- Gmail API send method:
  https://developers.google.com/workspace/gmail/api/reference/rest/v1/users.messages/send
- Google OAuth sensitive-scope verification:
  https://developers.google.com/identity/protocols/oauth2/production-readiness/sensitive-scope-verification
- Google OAuth verification FAQ (explicit SMTP-only / minimum-scope guidance):
  https://support.google.com/cloud/answer/13463817
- Gmail API sending scope:
  https://developers.google.com/workspace/gmail/api/auth/scopes
