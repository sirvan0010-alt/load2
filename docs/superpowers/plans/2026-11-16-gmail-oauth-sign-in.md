# Gmail OAuth Sign-in Implementation Plan

> **For agentic workers:** Implement inline task-by-task. Keep each task independently testable and do not merge the draft PR until all checks pass.

**Goal:** Let a MailLoadTester user connect their own Gmail account with Google sign-in and send a single explicitly confirmed message through the Gmail API. Use the public desktop OAuth client flow with PKCE and no manually entered Client Secret.

**Architecture:** Keep the existing SMTP load-test runner and SMTP diagnostic unchanged. Add a separate Google desktop OAuth flow (Authorization Code + PKCE + loopback callback), a per-Windows-user DPAPI-protected refresh-token store, and a Gmail API one-message sender. Add separate connect/disconnect and Gmail API send controls so the UI never labels Gmail API traffic as SMTP.

**Tech Stack:** .NET 8, Windows Forms, `HttpClient`, `TcpListener`, `System.Security.Cryptography.ProtectedData`, existing `MimeKit`, xUnit.

**Spec:** `docs/GOOGLE-OAUTH-IMPLEMENTATION-DECISION.md`

## Global Constraints

- Use the existing desktop OAuth Client ID: `717640306481-tt965q9ubsuv2u60iq1ocu9ook3hodv5.apps.googleusercontent.com`.
- Use the public Desktop Client ID without a Client Secret in authorization-code exchange or refresh-token requests; PKCE remains mandatory.
- Do not collect, store, or transmit a Client Secret. Treat the desktop app as a public client and require PKCE.
- Use Authorization Code + PKCE (S256), random state, system browser, and a loopback listener bound only to `127.0.0.1`.
- Request only `openid email profile https://www.googleapis.com/auth/gmail.send`.
- Persist only the refresh token and verified account identity; encrypt it with Windows DPAPI CurrentUser. Never put tokens in profiles, logs, reports, source control, or plaintext settings.
- Keep SMTP transport, SMTP load metrics, and existing SMTP buttons unchanged.
- Gmail API sends must be labelled as Gmail API sends and must not be represented as SMTP protocol tests.
- Do not automatically retry a send after ambiguous network failure; only retry once after an explicit HTTP 401 and successful token refresh.
- Do not run live Google sign-in or send live mail in CI.

## Review Focus

- OAuth callback with a wrong/missing state or Google error must not exchange a code.
- Local callback listener must accept only loopback traffic and must time out cleanly.
- Missing/expired/revoked refresh token must show a useful reconnect message without exposing secrets.
- HTTP 401 may refresh and retry exactly once; 403, 429, 5xx and network timeouts must not cause duplicate sends.
- A user must not be able to send an unintended message: require valid sender/recipient and a separate confirmation naming the Gmail account and recipient.

---

### Task 1: Desktop OAuth callback flow

**Files:**
- Create: `src/MailLoadTester.Core/GoogleOAuthLoopbackFlow.cs`
- Modify: `src/MailLoadTester.Core/GoogleOAuthClient.cs`
- Test: `tests/MailLoadTester.Tests/GoogleOAuthClientTests.cs`
- Test: `tests/MailLoadTester.Tests/GoogleOAuthLoopbackFlowTests.cs`

**Interfaces:**
- Consumes: `GoogleOAuthClient.CreatePkceChallenge()`, `BuildAuthorizationUri(...)`, `ExchangeCodeAsync(...)`, and `GetUserAsync(...)`.
- Produces: `Task<GoogleOAuthAuthorizationResult> AuthorizeAsync(GoogleOAuthClient client, Func<Uri, CancellationToken, Task> openBrowser, TimeSpan timeout, CancellationToken cancellationToken)`.

- [ ] Add tests for valid callback/state, wrong state, missing code, Google error, and callback timeout.
- [ ] Run focused tests and verify the expected failures.
- [ ] Implement an ephemeral `TcpListener` bound to `IPAddress.Loopback`, parse only a bounded HTTP GET request, validate state using constant-time comparison, and return a small browser success/error page.
- [ ] Start the listener before opening the system browser; use a three-minute timeout and cancellation.
- [ ] Exchange the one-time code and require a verified Google email.
- [ ] Run focused and full test suite.

### Task 2: DPAPI credential persistence

**Files:**
- Create: `src/MailLoadTester.Core/GoogleOAuthCredentialStore.cs`
- Create: `src/MailLoadTester.Gui/WindowsDpapiSecretProtector.cs`
- Modify: `src/MailLoadTester.Gui/MailLoadTester.Gui.csproj`
- Test: `tests/MailLoadTester.Tests/GoogleOAuthCredentialStoreTests.cs`

**Interfaces:**
- Produces: `IGoogleOAuthSecretProtector`, `GoogleOAuthStoredAccount`, and `GoogleOAuthCredentialStore.SaveAsync/LoadAsync/DeleteAsync`.
- Store only account subject, verified email, and refresh token in the account credential store. Access tokens remain in memory and are refreshed when needed.
- Store only the user's refresh token in a DPAPI-protected file; no OAuth Client Secret is collected or stored.

- [ ] Add tests using a deterministic fake protector for round-trip, missing file, corrupted payload, and deletion.
- [ ] Run focused tests and verify failure before implementation.
- [ ] Implement atomic file replacement under the LocalAppData MailLoadTester folder for the current Windows user.
- [ ] Implement Windows DPAPI CurrentUser protection; do not use a shared machine scope.
- [ ] Add package reference only to the Windows GUI project if required.
- [ ] Run focused and full tests plus Windows GUI build.

### Task 3: Gmail API MIME sender

**Files:**
- Create: `src/MailLoadTester.Core/GoogleGmailApiClient.cs`
- Test: `tests/MailLoadTester.Tests/GoogleGmailApiClientTests.cs`

**Interfaces:**
- Produces: `Task<GoogleGmailSendResult> SendAsync(MimeMessage message, GoogleOAuthClient oauth, GoogleOAuthTokenSet tokens, CancellationToken cancellationToken)`.
- Serialize the message as RFC 5322 MIME and base64url encode it for `POST https://gmail.googleapis.com/gmail/v1/users/me/messages/send`.

- [ ] Add fake-handler tests for MIME raw payload, bearer authorization, success response, 401 refresh-once, and no retry for 403/429/5xx.
- [ ] Run focused tests and verify failure before implementation.
- [ ] Implement API call with `gmail.send` only.
- [ ] Refresh once on explicit 401; do not retry ambiguous network errors or other status codes.
- [ ] Return Gmail message ID and thread ID without logging message content or tokens.
- [ ] Run focused and full test suite.

### Task 4: Simple Windows Forms experience

**Files:**
- Create: `src/MailLoadTester.Gui/MainForm.GoogleOAuth.cs`
- Modify: `src/MailLoadTester.Gui/MainForm.cs`
- Modify: `src/MailLoadTester.Gui/MainForm.Layout.cs`
- Modify: `src/MailLoadTester.Gui/MainForm.Execution.cs`

**Interfaces:**
- Add separate controls: `btnGoogleSignIn`, `btnGoogleSignOut`, `btnSendGoogleTest`, `lblGoogleAccountStatus`.
- Existing `btnSendTest` remains SMTP-only; existing `btnStart` remains SMTP load-test-only.

- [ ] Add UI event tests where feasible for state/validation helpers.
- [ ] Add a clearly labelled Google account area in the SMTP tab with one-click sign-in, signed-in email, and disconnect.
- [ ] On startup, load the DPAPI-protected account metadata and show the connected email without opening a browser.
- [ ] Add a separate Gmail API one-message send button using the existing From/To/CC/BCC/Subject/Body fields and existing attachment list.
- [ ] Require explicit confirmation showing signed-in Gmail account and all recipients before sending.
- [ ] On revoked/expired credentials, clear invalid stored credentials when appropriate and prompt reconnect; never silently fall back to SMTP.
- [ ] Keep Gmail API status/log text distinct from SMTP metrics.
- [ ] Run full tests and Windows GUI build.

### Task 5: Release notes and final verification

**Files:**
- Modify: `docs/USER_GUIDE.md`
- Modify: `docs/SAFETY_AND_PRIVACY.md`
- Modify: `docs/GOOGLE-OAUTH-IMPLEMENTATION-DECISION.md`

- [ ] Document first-time sign-in, disconnect, Gmail API one-message send, test-user limitation, and public verification requirement.
- [x] Verify no Client Secret is collected or transmitted and no tokens are committed or logged.
- [x] Verify authorization-code exchange and refresh requests omit `client_secret`; PKCE is mandatory.
- [ ] Run full CI, Windows packaging, CodeQL, and inspect final PR diff.
- [ ] Keep PR as draft until all verification passes; do not merge without explicit approval.