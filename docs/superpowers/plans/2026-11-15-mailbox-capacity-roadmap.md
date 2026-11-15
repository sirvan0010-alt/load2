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
- [x] Add optional, profile-persisted capacity-run settings: logical message-count ceiling, cumulative MIME-byte budget, per-message MIME-byte ceiling, and messages/minute ceiling.
- [x] Apply count/rate ceilings before run startup. The rate ceiling is global and also applies to retry SEND attempts; it never accelerates an already slower configured interval.
- [x] Serialize the fully built MIME message after payload plugins and measure its actual serialized size immediately before SMTP SEND. SMTPUTF8 format is used for the measurement when enabled.
- [x] Enforce per-message and cumulative MIME-byte budgets atomically across workers and AutoRestart attempts. If the next SEND would exceed a configured budget, stop the run rather than send it.
- [x] Count every admitted SEND attempt against the cumulative MIME budget, including retries. This is conservative because a timeout/rejection may occur after some or all DATA bytes reached the server.
- [x] Add unit tests for count/rate planning, exact byte-budget boundaries, per-message ceiling, retry accounting, validation and profile round-trip.
- [x] Add a live status-strip counter for reserved SMTP-prepared MIME bytes and the configured cumulative budget; surface the capacity stop reason in progress and final run results.
- [x] Compare the configured per-message ceiling with the connected SMTP client's post-authentication SIZE capability and enforce the stricter positive limit. SIZE remains a per-message ceiling, not mailbox quota.
- [x] Report actual SmtpClient.SendAsync calls (including retries and AutoRestart) separately from pre-send capacity reservations, unique logical SMTP-accepted/failed messages, and SMTP 4xx/5xx outcome classes.
- [x] Capture IMAP quota/INBOX/Junk snapshots immediately before and after the run and report observed deltas only for metrics present in both snapshots. Label these as observed changes, not messages causally attributed to the run.
- [x] Track conservatively reserved SMTP-prepared MIME bytes separately from observed IMAP quota storage deltas. Actual SMTP wire bytes remain Unknown unless independently measured.
- [x] Estimate Base64/MIME expansion before send for operator preview, including each attachment's own Base64 line wrapping and the effective logical message count. Mark the estimate incomplete when template/generated/custom content is not fully accounted for; runtime guard uses actual SMTP-prepared MIME bytes.
- [x] Auto-stop only on a real server-reported IMAP STORAGE quota. The 30-second polling interval and possible overshoot between checks are disclosed in the GUI tooltip.

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
- **M3 in progress:** IMAP monitor now enumerates a bounded set of folders (up to 100), reads supported STATUS count/unread/size values without fetching message bodies, uses SPECIAL-USE Junk/Sent/Trash/Inbox flags with localized-name fallback for Junk, and keeps unknown folder metrics null. Restricted folder listing does not block the existing quota/INBOX check. GUI separates quota message count, INBOX count and Junk/Spam count.
- **M4 implementation:** optional CapacityRunLimits are persisted in profiles; count/rate ceilings are applied at startup; serialized MIME message sizes are guarded per-message and cumulatively across concurrent workers and AutoRestart attempts; the stricter positive per-message ceiling from authenticated SMTP SIZE is applied automatically. Capacity byte budgets count retries and are MIME-byte budgets, not SMTP wire bytes or mailbox growth. The UI exposes all four controls, a live MIME-budget counter/stop reason, and a conservative Base64/MIME preflight estimate. Results separate actual SMTP SEND calls, capacity reservations, unique logical SMTP outcomes, and observed IMAP before/after quota/folder deltas. Automated tests cover planner, exact budget boundaries, retry accounting, validation, profile persistence, SMTP-prepared MIME sizing, mailbox-delta math, and concurrent capacity-stop behavior.
- **M4 remaining:** perform manual Windows GUI verification on a real Windows workstation and compare MIME-size accounting against representative production SMTP providers. The cloud Windows build/package verifies compilation and publication, not interactive GUI behavior.
- **M5/M6 not yet implemented:** verified mailbox deltas into adaptive pacing and the multi-series live chart.
- **Verification state:** commit `cd5715d4ed72fd93e517dd876e0674bcbbcc9541` passed CI with 435/435 tests, Windows Forms GUI Release build, and the self-contained Windows x64 package. Artifact SHA-256: `552b488d7a2e014cf3129095ed422a65ebd2cf0ef9d442cbabb2b33812e25abb`. Manual GUI/provider validation remains outstanding. Keep PR #38 Draft; do not merge until all release acceptance criteria pass.
