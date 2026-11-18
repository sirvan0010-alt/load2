# MailLoadTester – implementation roadmap

## Scope and non-goals

This roadmap covers UI responsiveness/redesign, SMTP credential persistence, privacy auditing of messages, SMTP route verification, alias/relay support and post-delivery verification.

**Authentication is frozen and out of scope for this work.** Do not modify, refactor, reorder, or redesign:
- Google OAuth sign-in/sign-out, consent, token exchange, refresh, revocation or DPAPI token storage.
- The current separation/visibility of Google OAuth and classic SMTP authentication.
- SMTP authentication methods or credential-entry flow, except strictly necessary bug fixes approved separately.

No change in this roadmap may silently alter authentication behavior. Any unavoidable interaction must be isolated and regression-tested.

## Principles

1. Work in small, reviewable commits on the existing feature branch; do not commit to `main`.
2. Inspect the real source and tests before editing. No illustrative stand-alone replacement files presented as integrated work.
3. Preserve existing SMTP runner, safety gate, Dry-run/Test Mode distinction, authorization controls and delivery metrics.
4. Do not claim anonymity. Report exactly what the application can observe and what only a receiving server can add.
5. Never strip or forge authentication/routing headers such as Received, Return-Path, DKIM-Signature, Authentication-Results, SPF or DMARC. Do not rewrite From to impersonate an unrelated domain.
6. No real bulk delivery during CI. Tests use synthetic MIME messages and local/mock SMTP; live tests require an explicitly controlled test domain and recipient.
7. Every phase must include automated tests, build verification and a concise user-facing change note.

## Phase 0 – Baseline and regression boundary

- Record current branch/commit and PR state.
- Map the WinForms layout construction, helper methods, control sizing, resize handlers, DPI settings and all tab layouts.
- Map MIME creation and SMTP send pipeline; identify where custom headers, Message-ID, From/Reply-To and envelope sender are set.
- Map proxy/SOCKS5 transport creation and every network path that can bypass it.
- Record existing tests and CI status.
- Add regression checks ensuring the authentication files and behavior remain untouched in subsequent feature commits.

**Exit criteria:** a source-backed inventory and baseline build/test result; no behavior changes.

## Phase 1 – Responsive UI and usability redesign (first implementation)

- Replace fixed-width controls and non-wrapping FlowLayoutPanel arrangements where they cause clipping or horizontal scrolling.
- Use responsive TableLayoutPanel layouts with percentage/fill columns, sensible minimum widths and wrapping labels.
- Add responsive reflow for the SMTP tab: three groups side-by-side when space allows, stacked when narrow.
- Audit every other tab for fixed widths, fixed MaximumSize labels, horizontal scroll and resize behavior; fix the same pattern consistently.
- Verify DPI behavior (PerMonitorV2 where supported) without changing unrelated startup behavior.
- Keep existing features and control names/handlers where practical to minimize regressions.

**Acceptance:** at 100%, 125%, 150% and 200% scaling; narrow, normal and maximized windows; no clipped labels/inputs, unintended horizontal scroll or inaccessible controls. Add tests where practical and provide manual verification checklist.

## Phase 2 – SMTP credential persistence review

- Inspect the existing `SmtpCredentialStore` and its GUI integration (already present in the current branch).
- Verify opt-in default, DPAPI CurrentUser protection, save-after-success behavior, restore behavior, deletion on opt-out, error handling and secret zeroing where feasible.
- Do not redesign authentication or change Google OAuth.
- Correct only defects proven by tests or source review.

**Acceptance:** unit tests plus Windows-user smoke-test instructions; profile JSON remains free of secrets.

## Phase 3 – Privacy Audit model and MIME inspection

- Add a deterministic audit over the final composed `MimeMessage` before SMTP submission.
- Inspect From, Sender, Reply-To, To/Cc/Bcc exposure, Message-ID, custom headers and known identifying headers (for example X-Originating-IP, X-Mailer, User-Agent, X-Client-IP and X-Forwarded-For).
- Use configurable expected sender domains and explicitly supplied protected addresses; do not guess that every Gmail/Seznam/Outlook address is necessarily personal.
- Classify findings as Info/Warning/Critical, explain evidence and provide remediation.
- Distinguish headers authored by the application from headers added later by SMTP relays/receivers.
- Do not claim to predict Received, Return-Path, DKIM, SPF, DMARC or Authentication-Results before delivery.

**Acceptance:** unit tests for clean messages, personal-address matches, custom headers, malformed values and false-positive handling.

## Phase 4 – Origin Preview

- Display the exact MIME headers/body as submitted by the application, clearly labeled **Pre-send preview**.
- Mark server-added headers as unknown/not yet observable; do not fabricate them.
- Add copy/export for diagnostic review with secrets and message content redaction options.
- Provide a dry-run preview that performs no network operation.

**Acceptance:** preview is generated from the same MimeMessage instance that is submitted; tests verify parity.

## Phase 5 – SMTP Route Check

- Trace the actual transport path used by the SMTP connection.
- Distinguish configured proxy, direct connection and unsupported network paths.
- Fail closed when the user explicitly requires a proxy/Tor route and the connection cannot be proven to use it; never silently fall back to direct.
- Clearly distinguish the SMTP server RemoteEndPoint from the client's public egress IP.
- Audit auxiliary network features separately (DNS/RBL, webhooks, IMAP, Gmail API) and report if they do not use the SMTP proxy.

**Acceptance:** mocked tests for proxy success, proxy failure, direct mode and no-fallback behavior. No external IP checks in ordinary unit tests.

## Phase 6 – Alias and dedicated relay support

- Document supported alias providers and their actual outbound SMTP/API capabilities and limits; do not assume forwarding aliases are outbound relays.
- Support ordinary SMTP relay configuration through existing SMTP settings, without changing authentication UI or OAuth.
- Document custom-domain DNS requirements (SPF, DKIM, DMARC, MX as applicable) and safe test-volume limits.
- Provide a controlled test relay profile and explicit recipient/domain authorization.
- Never allow arbitrary From spoofing; validate alignment and explain provider-enforced sender identity.

**Acceptance:** integration tests against a local SMTP test server and documented manual test with a domain/account the operator controls.

## Phase 7 – Post-delivery forensic audit

- Retrieve a delivered test message only from a mailbox/server the operator controls (or accept an uploaded .eml).
- Parse actual received headers and compare them with the pre-send MIME.
- Identify relay-added Received, Return-Path, DKIM, SPF, DMARC and Authentication-Results; distinguish observed facts from inferences.
- Report whether personal addresses, client IPs or device identifiers were exposed in the delivered message.
- Never promise absolute anonymity.

**Acceptance:** fixture-based .eml tests plus optional controlled end-to-end test.

## Phase 8 – Documentation, packaging and release

- Update user guide and threat-model limitations.
- Add screenshots only from the real updated application.
- Run full CI, Windows package workflow and security checks.
- Publish a versioned GitHub Release only after successful validation; include SHA-256 checksums and clear upgrade notes.

## Current execution order

1. Phase 0 baseline audit.
2. Phase 1 responsive UI redesign.
3. Phase 2 verify existing SMTP DPAPI persistence without touching sign-in flows.
4. Phases 3–5 privacy and route diagnostics.
5. Phases 6–7 relay and post-delivery validation.
6. Phase 8 release.

## Explicit exclusions

The following are not part of this roadmap: changing Google OAuth, changing the division between Google OAuth and classic SMTP sign-in, spoofing third-party identities, hiding or falsifying SMTP routing/authentication evidence, bypassing provider rate limits, or sending unsolicited bulk mail.
