# MailLoadTester – implementation roadmap

## Scope and non-goals

This roadmap covers UI responsiveness/redesign, migration to .NET 10, ongoing GitHub module/dependency maintenance, SMTP credential persistence, privacy auditing of messages, SMTP route verification, privacy-service comparison, safe alias/relay interoperability and post-delivery verification.

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

## Phase 2 – .NET 10 migration

- Confirm current target frameworks, SDK pinning, runtime assumptions, Windows support matrix and deployment model before changing project files.
- Migrate application and test projects to .NET 10 in a dedicated, reviewable change; update `global.json`, target frameworks, build scripts, CI workflows and Windows packaging consistently where applicable.
- Audit WinForms, MailKit, MimeKit, OAuth/Gmail API, proxy, charting and other third-party library compatibility with .NET 10; do not assume compatibility from package version alone.
- Resolve obsolete APIs and compiler/analyzer warnings without changing authentication behavior, SMTP semantics or user-visible defaults.
- Test clean restore, Debug/Release builds, all automated tests, Windows packaging and supported Windows runtime behavior.
- Keep the previous known-good branch/commit available for rollback; document required .NET 10 runtime/SDK installation for contributors and end users.

**Acceptance:** CI builds and tests all target projects on .NET 10; Windows package starts on a supported Windows machine; no OAuth or SMTP sign-in changes; migration notes and rollback reference are documented.

## Phase 3 – GitHub module and dependency maintenance

- Inventory direct and transitive NuGet dependencies, SDK/tool versions, Git submodules, vendored modules, GitHub Actions and reusable workflows.
- Track upstream releases, security advisories, end-of-support notices, breaking changes and license changes across NuGet packages, Git submodules, reusable workflows and external modules.
- Prioritize security fixes and supported patch/minor updates; group related upgrades into small pull requests with changelog and compatibility notes.
- For major upgrades, review migration guides and API changes before implementation; do not batch unrelated major-version jumps.
- Run restore, tests, analyzers, CodeQL, dependency review, architecture checks and Windows packaging for each update group.
- Pin GitHub Actions to reviewed immutable commit SHAs where practical; periodically refresh pins and verify permissions are least-privilege.
- Produce a dependency status report (current version, candidate version, reason, risk, tests and decision); do not auto-merge or auto-deploy unreviewed updates.
- Preserve a rollback path and record any intentionally deferred vulnerable/outdated dependency with owner and rationale.

**Acceptance:** reproducible dependency inventory, no unreviewed critical/high vulnerability, CI green for each accepted update, and documented update cadence.

## Phase 4 – SMTP credential persistence review

- Inspect the existing `SmtpCredentialStore` and its GUI integration (already present in the current branch).
- Verify opt-in default, DPAPI CurrentUser protection, save-after-success behavior, restore behavior, deletion on opt-out, error handling and secret zeroing where feasible.
- Do not redesign authentication or change Google OAuth.
- Correct only defects proven by tests or source review.

**Acceptance:** unit tests plus Windows-user smoke-test instructions; profile JSON remains free of secrets.

## Phase 5 – Privacy Audit model and MIME inspection

- Add a deterministic audit over the final composed `MimeMessage` before SMTP submission.
- Inspect From, Sender, Reply-To, To/Cc/Bcc exposure, Message-ID, custom headers and known identifying headers (for example X-Originating-IP, X-Mailer, User-Agent, X-Client-IP and X-Forwarded-For).
- Inspect both plain-text and HTML bodies for user-configured identifying strings, personal data patterns, hidden HTML content, external images/tracking pixels and tracking parameters in links. Report findings; do not silently rewrite message content.
- Inspect attachment filenames and available document metadata (EXIF, PDF Author/Creator and Office document properties). Clearly distinguish metadata that can be read locally from metadata that cannot be reliably inspected.
- Inspect SMTP envelope recipients and envelope sender separately from visible MIME headers. Verify that Bcc recipients are handled as envelope recipients and are not exposed as a visible Bcc header.
- Use configurable expected sender domains, protected addresses and prohibited strings; do not guess that every Gmail/Seznam/Outlook address is necessarily personal.
- Classify findings as Info/Warning/Critical, explain evidence and provide remediation. Default to reporting or blocking before send; never silently rewrite signed or sensitive content.
- Distinguish headers authored by the application from headers added later by SMTP relays/receivers.
- Do not claim to predict Received, Return-Path, DKIM, SPF, DMARC or Authentication-Results before delivery.

**Acceptance:** unit tests for clean messages, personal-address matches, custom headers, malformed values and false-positive handling.

## Phase 6 – Origin Preview

- Display the exact MIME headers/body as submitted by the application, clearly labeled **Pre-send preview**.
- Mark server-added headers as unknown/not yet observable; do not fabricate them.
- Add copy/export for diagnostic review with secrets and message content redaction options.
- Provide a dry-run preview that performs no network operation.

**Acceptance:** preview is generated from the same MimeMessage instance that is submitted; tests verify parity.

## Phase 7 – SMTP Route Check

- Trace the actual transport path used by the SMTP connection.
- Implement **Relay Capability Check** as a separate diagnostic, not as a modification of the existing sign-in or SMTP authentication flow.
- In a non-authenticated protocol inspection, record the server greeting, EHLO extensions, advertised SASL mechanism names, STARTTLS support, negotiated TLS state/version/cipher where available, SMTPUTF8, SIZE limit, DSN, PIPELINING, 8BITMIME, CHUNKING/BINARYMIME and REQUIRETLS. Clearly mark that capabilities may change after authentication.
- Keep authentication optional and out of the first implementation. Do not collect, persist, or replay credentials; do not alter Google OAuth or classic SMTP sign-in. If a later authenticated capability view is considered, it requires separate approval and must reuse no new login flow.
- Separate facts observed from the SMTP session from provider-policy information sourced from official documentation. Do not infer that EHLO proves sender/domain authorization, reputation, inbox placement, rate limits or anonymity.
- Report whether sender identity and envelope sender constraints are documented by the provider; only test acceptance with an explicitly controlled sender domain and recipient, and only after a separately approved implementation.
- Explain which identity is visible to the relay, which sender identity is presented to recipients, and which metadata is added by the relay.
- Distinguish configured proxy, direct connection and unsupported network paths.
- Fail closed when the user explicitly requires a proxy/Tor route and the connection cannot be proven to use it; never silently fall back to direct.
- Clearly distinguish the SMTP server RemoteEndPoint from the client's public egress IP.
- Audit auxiliary network features separately (DNS/RBL, webhooks, IMAP, Gmail API) and report if they do not use the SMTP proxy.

**Acceptance:** mocked tests for proxy success, proxy failure, direct mode and no-fallback behavior. No external IP checks in ordinary unit tests.

## Phase 8 – Alias and dedicated relay support

- Document supported alias providers and their actual outbound SMTP/API capabilities and limits; do not assume forwarding aliases are outbound relays.
- Support ordinary SMTP relay configuration through existing SMTP settings, without changing authentication UI or OAuth.
- Document custom-domain DNS requirements (SPF, DKIM, DMARC, MX as applicable) and safe test-volume limits.
- Provide a controlled test relay profile and explicit recipient/domain authorization.
- Never allow arbitrary From spoofing; validate alignment and explain provider-enforced sender identity.

**Acceptance:** integration tests against a local SMTP test server and documented manual test with a domain/account the operator controls.

## Phase 9 – Privacy-service comparison and interoperability

- Build a documented capability matrix for legitimate privacy services and architectures, including email alias/forwarding providers (for example SimpleLogin and addy.io), browser/email masking services (for example Firefox Relay and DuckDuckGo Email Protection), self-hosted alias/relay options, encrypted mailbox providers and Tor as a network transport layer.
- Compare what each option actually protects: primary-address exposure, sender identity, reply-from-alias, metadata visibility, content encryption, IP exposure, logging/retention, self-hosting, SMTP/API availability, domain control, abuse controls, deliverability and operational trust.
- Distinguish an alias/forwarder from an outbound SMTP relay, disposable inbox, encrypted mailbox and network anonymizer; do not present them as interchangeable.
- Verify each candidate against its official documentation and current terms before claiming SMTP/API integration. Add only documented, authorized integrations; otherwise provide configuration guidance and a clear boundary.
- Add an interoperability test profile using a mailbox/domain controlled by the operator, with explicit recipient allowlists, conservative rate/volume caps, a hard stop, and no arbitrary recipient-list import.
- Do not implement sender spoofing, provider-limit bypass or mechanisms intended to conceal abusive bulk sending.
- Explain that Tor can conceal the client IP from the SMTP endpoint when the actual SMTP socket uses Tor, but it does not conceal account identity, message content, relay identity or provider-side records.

**Acceptance:** sourced comparison matrix, threat-model notes, tested safe interoperability against controlled endpoints, and explicit abuse-prevention controls.

## Phase 10 – Post-delivery forensic audit

- Retrieve a delivered test message only from a mailbox/server the operator controls (or accept an uploaded .eml).
- Parse actual received headers and compare them with the pre-send MIME.
- Identify relay-added Received, Return-Path, DKIM, SPF, DMARC and Authentication-Results; distinguish observed facts from inferences.
- Report whether personal addresses, client IPs or device identifiers were exposed in the delivered message.
- Never promise absolute anonymity.

**Acceptance:** fixture-based .eml tests plus optional controlled end-to-end test.

## Phase 11 – Documentation, packaging and release

- Update user guide and threat-model limitations.
- Add screenshots only from the real updated application.
- Run full CI, Windows package workflow and security checks.
- Publish a versioned GitHub Release only after successful validation; include SHA-256 checksums and clear upgrade notes.

## Current execution order

1. Phase 0 baseline audit.
2. Phase 1 responsive UI redesign.
3. Phase 2 migrate to .NET 10.
4. Phase 3 review and update GitHub modules/dependencies.
5. Phase 4 verify existing SMTP DPAPI persistence without touching sign-in flows.
6. Phases 5–7 privacy, MIME and route diagnostics.
7. Phase 8 alias/relay support.
8. Phase 9 compare privacy services and validate safe interoperability.
9. Phase 10 post-delivery forensic validation.
10. Phase 11 documentation, packaging and release.

## Explicit exclusions

The following are not part of this roadmap: changing Google OAuth, changing the division between Google OAuth and classic SMTP sign-in, spoofing third-party identities, hiding or falsifying SMTP routing/authentication evidence, bypassing provider policies, concealing abusive bulk sending, or sending unsolicited bulk mail.
