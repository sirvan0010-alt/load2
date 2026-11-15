# Mailbox Capacity Test — implementation roadmap

**Branch:** `feature/mailbox-capacity-adaptive`  
**Parent:** PR #37 (`feature/comprehensive-tooltips`)  
**Goal:** determine how much data and how many messages an authorized recipient mailbox can actually store, while preserving SMTP-only operation when no IMAP/API credentials are available.

## Product rules

- SMTP acceptance, mailbox storage, and mailbox quota are three different facts and must remain separate in code, UI, charts, and reports.
- Without IMAP/provider API evidence, mailbox quota, stored-message count, and Inbox/Junk placement are Unknown.
- IMAP credentials are optional. SMTP-only sending remains usable without them.
- Read-only discovery never sends a message. A test send is a separate explicit action.
- Do not automatically probe huge message sizes. Respect advertised per-message SIZE limits and operator-defined ceilings.
- No filter evasion, identity/IP rotation, or endless retries. Legitimate authentication, diagnostics, bounded backoff, and controlled pacing are in scope.

## Milestones

### M1 — Read-only SMTP and recipient-domain discovery
- Resolve recipient-domain MX records (not the sender's domain).
- Probe a bounded number of MX hosts on TCP/25 using greeting + EHLO only.
- Probe the configured submission endpoint separately (465/587/other explicitly configured port).
- Capture advertised SIZE, STARTTLS, AUTH mechanisms, PIPELINING, 8BITMIME, SMTPUTF8 and CHUNKING.
- Record TLS protocol and certificate subject when TLS is negotiated and certificate validation succeeds.
- Never interpret SIZE as mailbox quota.
- Tests: EHLO with/without SIZE, unreachable MX, cancellation, malformed recipient, and no message commands sent.

### M2 — SMTP response classification and adaptive response
- Preserve raw SMTP status code, enhanced status code and command stage.
- Classify temporary 4xx (including throttling/greylisting) separately from permanent 5xx.
- Treat policy/authentication/reputation 5.7.x refusals as stop-and-review, not blind retry.
- Apply bounded exponential backoff to retryable temporary failures; reduce concurrency/rate after pressure.
- Tests for 421/450/451/452/454 and representative 5xx/5.7.x cases.

### M3 — IMAP mailbox inventory
- Discover folders using server metadata/special-use flags, not English-only folder names.
- Read Inbox and likely Junk/Spam folders without downloading bodies or attachments.
- Query STORAGE and MESSAGE quotas where supported; retain unsupported/unknown values as null.
- Keep per-folder size separate from account-wide quota.
- Tests for localized folder names, missing quota, unsupported STATUS SIZE, and read-only behavior.

### M4 — Capacity-run configuration and accounting
- Add target total bytes, target message count, maximum message size, rate ceiling and safety stop.
- Track attempted, SMTP-accepted, transient-rejected, permanent-rejected, verified-stored messages separately.
- Track prepared MIME bytes, server-accepted SMTP bytes (only if measured), mailbox used/quota bytes separately.
- Estimate Base64 MIME expansion and show the estimate before sending large attachments.
- Auto-stop only on a real quota measurement or explicit operator limit; disclose polling overshoot.

### M5 — Adaptive pacing and bounded calibration
- Begin with one small validation message only when the operator explicitly starts the run.
- Use actual response codes, latency and verified mailbox deltas to adjust rate/concurrency.
- Do not infer quota or sustained throughput from a single message.
- Never exceed discovered SIZE, configured per-message cap, or operator rate ceiling.
- Distinguish unknown capability from a negative capability.

### M6 — Live observability, reports and release
- Extend the live chart with SMTP accepted/minute, 4xx/minute, 5xx/minute, verified stored/minute, quota used/percent, adaptive rate and concurrency.
- Missing IMAP data appears as a gap/Unknown, never zero.
- Keep charts bounded in memory and independent of the SMTP send path.
- Add end-to-end tests, Windows GUI verification, docs, CI and packaged release validation.

## Acceptance criteria for final release

1. SMTP-only mode works with every IMAP field empty.
2. No SMTP 2xx is labelled as stored/delivered-to-mailbox without independent evidence.
3. Recipient MX discovery is based on the recipient domain; sender-domain SPF/DMARC checks remain separate.
4. A server SIZE value is displayed as a maximum for one message only.
5. A 1 GiB binary attachment estimate includes Base64/MIME overhead and is blocked by the per-message cap when too large.
6. Missing quota/folder support is represented as Unknown, not zero.
7. 4xx backoff is bounded; permanent policy refusals are not retried indefinitely.
8. Quota stop uses only a real server-reported quota.
9. Graph history is bounded and telemetry failures cannot break SMTP sending.
10. Required CI, CodeQL/dependency checks, Windows publish, and manual GUI verification pass before merge.

## Current implementation status (2026-11-15)

- **M1 in progress:** read-only SMTP capability discovery service is implemented for a configured submission endpoint and up to three recipient MX hosts. It reads greeting/EHLO capabilities, advertised SIZE, TLS protocol and certificate subject. It never authenticates or sends MAIL/RCPT/DATA.
- **M1 UI slice:** Mailbox Monitor has separate buttons to inspect recipient MX and the configured SMTP submission endpoint. The recipient lookup uses the first valid address in the Komu field.
- **MIME sizing:** Base64 line-wrapping and conservative maximum-attachment estimates are implemented with overflow checks.
- **Existing diagnostic correction:** MX lookup now targets the first recipient domain instead of incorrectly using the sender domain; TLS certificate subject is captured during certificate validation.
- **Tests added:** fake local SMTP server verifies SIZE/EHLO parsing and that discovery issues no MAIL or DATA command; MIME sizing tests cover 1 GiB and the 25 MiB message ceiling.
- **M2 in progress:** enhanced SMTP status codes now distinguish mailbox full (5.2.2), message too large (5.3.4), and policy refusal (5.7.x). These are non-retryable; temporary 4xx remain bounded-retry candidates. The observed-response guidance now explains the correct capacity/size/policy action.
- **Not yet complete:** IMAP folder inventory/Junk discovery, capacity-run controls, integration of verified mailbox deltas into adaptive pacing, and multi-series live chart.
- **Verification state:** CI and Windows package workflows are pending for the current branch head. Do not merge until they pass and a Windows GUI run is performed.
