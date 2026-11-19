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
- Audit and extend the existing `TransportDiagnostics` and `SmtpCapabilityDiscoveryService` implementations; do not create a second parallel capability-discovery subsystem without a demonstrated need. `TransportDiagnostics` already supports SMTP/DNS/MX observations and applies configured proxy settings; `SmtpCapabilityDiscoveryService` performs focused read-only EHLO discovery but currently has a separate direct-connect path. Decide whether to consolidate or clearly scope these services, and add regression tests.
- Keep **Relay Capability Check** as a separate diagnostic, not as a modification of the existing sign-in or SMTP authentication flow.
- In the first, non-authenticated protocol inspection, record the server greeting, EHLO extensions, advertised SASL mechanism names, STARTTLS support, negotiated TLS state/version and certificate details (cipher suite only if the library exposes it reliably), SMTPUTF8, SIZE limit, DSN, PIPELINING, 8BITMIME, CHUNKING/BINARYMIME and REQUIRETLS. Clearly mark which fields the current MailKit implementation can observe and which are not yet exposed; capabilities may change after authentication.
- Keep authentication disabled in the first capability-check UI path even though the existing diagnostic model has an optional authentication switch. Do not collect, persist, or replay credentials; do not alter Google OAuth or classic SMTP sign-in. If a later authenticated capability view is considered, it requires separate approval and must reuse no new login flow.
- Separate facts observed from the SMTP session from provider-policy information sourced from official documentation. Do not infer that EHLO proves sender/domain authorization, reputation, inbox placement, rate limits or anonymity.
- Report whether sender identity and envelope sender constraints are documented by the provider; only test acceptance with an explicitly controlled sender domain and recipient, and only after a separately approved implementation.
- Explain which identity is visible to the relay, which sender identity is presented to recipients, and which metadata is added by the relay.
- Distinguish configured proxy, direct connection and unsupported network paths.
- Fail closed when the user explicitly requires a proxy/Tor route and the connection cannot be proven to use it; never silently fall back to direct. Test both the focused discovery service and the general transport diagnostics so a direct-connect helper cannot accidentally bypass the selected route.
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

### Evidence-based comparison: SimpleLogin and addy.io

Treat both services as **email aliasing/forwarding relays**, not as anonymous SMTP accounts or general-purpose SMTP servers.

| Capability | SimpleLogin | addy.io | MailLoadTester implication |
|---|---|---|---|
| Receive on alias and forward to mailbox | Yes | Yes | Not an SMTP send integration by itself |
| Reply through alias | Yes, via a per-contact reverse alias | Yes, via alias reply routing | The alias provider rewrites/routes the reply; the user's mailbox is still the originating client |
| Start a new conversation from alias | Yes; paid feature, create a reverse alias for the recipient | Yes; Lite/Pro, send to a special encoded address from a verified recipient | Provider-specific composition workflow, not ordinary SMTP credentials |
| Alias management API | Official API documented; verify current supported endpoints and scopes before integration | Official REST API documents alias, recipient, domain and failed-delivery management | API may manage aliases; do not assume it is an API for arbitrary outbound message submission |
| Custom domains | Supported on Premium | Supported on Lite/Pro tiers | Requires domain ownership and correct DNS |
| Self-hosting | Open-source backend; documented Linux/Postfix/PostgreSQL deployment | Open-source application; documented Postfix, PHP, database, Redis and Rspamd deployment | High operational burden: domain, MX, outbound port 25, reverse DNS, SPF, DKIM, DMARC, TLS, monitoring and reputation |
| Message retention | SimpleLogin security page describes encrypted database/backups and temporary bounced-message storage; verify current retention details before relying on them | Provider says message content is stored only for failed delivery when that option is enabled; logs still contain connection metadata/IPs | Never assume alias provider has zero visibility or zero logs |
| Transport/security controls | Provider documents SPF/DKIM signing and TLS/domain protections | Provider documents STARTTLS/DANE, SPF/DKIM/DMARC and related protections | These protect transport/domain authenticity, not anonymity from the relay |
| Content encryption | Optional PGP for mailbox forwarding is documented separately; not equivalent to end-to-end encryption for every recipient | Optional GPG/OpenPGP encryption for forwarded mail | Encryption is a separate property from address masking |
| Provider trust | Hosted service operated by Proton AG; self-hosting reduces dependence on hosted operator but shifts trust to server operator | Hosted service operated by addy.io; self-hosting shifts trust and operational responsibility to the user | Make trust boundary and data path explicit |

### Specific findings and design decisions

- **SimpleLogin outbound flow:** replies use a per-contact reverse alias. Starting a new conversation requires creating a reverse alias for the intended contact and sending to that special address from the user's normal mailbox. The hosted service then sends from the alias. This is not equivalent to giving MailLoadTester an SMTP host/user/password for SimpleLogin.
- **addy.io outbound flow:** the user composes in their normal mail client, uses a verified recipient address as From, and places an encoded special send-from address in To. addy.io validates the verified recipient and sends onward from the alias. New sends require Lite or Pro. This is also not a generic SMTP submission endpoint.
- **API boundary:** addy.io's public API documents account, alias, recipient, domain and failed-delivery operations. Do not infer that this API accepts arbitrary MIME messages for outbound sending. For SimpleLogin, verify the current API scope and documented reverse-alias operations before implementing anything; do not reverse-engineer undocumented endpoints.
- **Hosted-provider visibility:** the alias service necessarily processes routing information and message delivery. Address masking hides the mailbox address from the external recipient, not from the alias provider, and it does not conceal the user's upstream mailbox account from that mailbox provider.
- **IP boundary:** Tor only protects the client-to-service network path if that particular connection uses Tor. It does not hide the identity/account authenticated to the mailbox provider, the alias-provider account, message content, custom-domain ownership, or server-side logs.
- **Self-hosting is not automatically more anonymous:** it reduces reliance on the third-party alias operator but exposes the operator's hosting provider, IP, domain registration, DNS, reverse DNS, mail reputation and server logs as a new trust/identification surface. A home-hosted SMTP server may also have poor deliverability or blocked port 25.
- **Deliverability/authentication:** forwarding and alias routing can interact with SPF, DKIM and DMARC. Do not chain alias/forwarding services casually. Preserve original authentication evidence and test real outcomes only with a domain and mailbox controlled by the operator.
- **No credential harvesting:** integrations must not ask for or store the user's primary mailbox password. Any optional provider API token must be explicitly supplied, stored securely, scoped minimally, revocable, and never used to submit arbitrary mail.
- **No direct send integration in the first iteration:** first implement documentation, provider capability profiles and a configuration/compatibility assistant. A provider-specific alias-management API integration is a later, separately reviewed feature. Do not modify the existing OAuth or classic SMTP sign-in paths.

### Other services for comparison

- Firefox Relay: primarily masks addresses and forwards incoming mail; paid reply-from-mask functionality is constrained by the service's reply workflow. Treat as an address-masking reference, not an SMTP relay.
- DuckDuckGo Email Protection: primarily forwards masked addresses and removes supported tracking elements from incoming mail. Treat as a reference for tracker inspection, not outbound SMTP.
- Proton Mail/Proton Pass: distinguish encrypted mailbox capabilities from Pass alias capabilities. Proton Mail Bridge/SMTP access and alias sending are separate product paths with plan and provider constraints.
- Tor: network transport/privacy layer only; not an email identity or mail relay.

### Implementation sequence

1. Add a sourced provider capability matrix and threat-boundary explanation to the user documentation.
2. Add provider profile metadata (capabilities, required plan, official docs URL, DNS requirements, API availability, known limitations and last-verified date) without storing provider credentials.
3. Add a read-only compatibility assistant: validate entered domain/DNS records and explain whether the chosen provider flow is an alias, mailbox or SMTP relay. Do not probe or send to third-party recipients.
4. Only after separate approval, consider a minimal alias-management API adapter for SimpleLogin or addy.io. Keep API-token storage separate from SMTP credentials and use least-privilege scopes.
5. Any future end-to-end test must use an operator-controlled domain, mailbox and recipient and explicitly report which providers can observe the account, content and connection metadata.

- Do not implement sender spoofing, provider-policy bypass or mechanisms intended to conceal abusive bulk sending.
- Explain that Tor can conceal the client IP from the SMTP endpoint when the actual SMTP socket uses Tor, but it does not conceal account identity, message content, relay identity or provider-side records.

**Acceptance:** sourced comparison matrix, clear provider trust boundaries, a documented no-direct-send decision for the first iteration, and tested read-only compatibility checks.
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
