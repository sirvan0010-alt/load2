# Mailbox Capacity Test and Adaptive SMTP Discovery

**Status: proposed implementation specification**

## Goal
Add an explicit, opt-in mailbox-capacity workflow that distinguishes SMTP acceptance from actual mailbox storage. Preserve the existing SMTP test path: IMAP credentials remain optional and SMTP-only runs continue to work without them.

## Core invariants
1. SMTP 2xx acceptance is never reported as proof that a message was stored in the recipient mailbox.
2. Without an authenticated mailbox API (IMAP or provider-specific API), mailbox quota, free space, Inbox/Junk placement, and stored message count are Unknown, not estimated.
3. Public SMTP discovery is read-only and limited to the configured recipient domain's MX endpoints and the configured/authorized SMTP submission endpoint.
4. A server's advertised SIZE capability is a per-message maximum, not mailbox quota.
5. A single successful message cannot establish rate limits, storage quota, filtering policy, or sustainable throughput.
6. Permanent policy/authentication/content rejections are not retried as if they were transient throttling.
7. No IP/account rotation, identity spoofing, filter evasion, or unbounded retry behavior is introduced.

## Proposed workflow
### Stage 1 — Read-only discovery (no IMAP required)
- Resolve MX records for the recipient domain.
- Probe only relevant SMTP endpoints: TCP reachability, banner, EHLO capabilities, STARTTLS/TLS certificate metadata.
- Parse the advertised SIZE extension when present and retain the raw EHLO response.
- Record provider-specific public limits only when backed by a known provider preset or explicit operator-supplied value.
- Mark mailbox quota and mailbox placement as Unknown when no authenticated mailbox source is configured.

Direct-MX probing and authenticated submission are separate paths; do not silently switch between them.

### Stage 2 — One-message validation
- Send one small, explicitly addressed test message to a mailbox the operator is authorized to test.
- Record SMTP command/reply stage, response code/text, elapsed time, and message size.
- Treat 2xx as AcceptedBySmtp, not Stored.
- If IMAP is configured, refresh selected folders and quota after delivery; allow for delivery latency and delayed indexing.
- Do not infer all recipient limits from this single sample.

### Stage 3 — Optional size/rate calibration
- Respect advertised SIZE as an upper bound when present.
- Use operator-selected bounded test sizes and a conservative sequence; never automatically probe huge payloads.
- A 5xx size rejection is a per-message limit signal; stop increasing size.
- A 4xx response is temporary; apply bounded backoff and reduce concurrency/rate.
- A 5xx policy/authentication/reputation response stops the affected path and asks for configuration review; do not retry-loop it.
- Report Unknown when the server does not publish a limit and a safe test cannot establish one.

### Stage 4 — Capacity run
- Operator sets target volume and/or target message count, maximum message size, rate ceiling, and safety stop.
- Optional IMAP monitor samples STORAGE quota, message quota, and selected folders.
- Auto-stop only on a real server-reported quota/threshold or explicit operator limit.
- Polling intervals create overshoot risk; show the interval and conservative headroom in the UI.
- Keep cleanup out of scope unless separately and explicitly enabled with confirmation.

## Data model / state vocabulary
Use separate counters and units:
- AttemptedMessages
- SmtpAcceptedMessages
- SmtpTransientRejectedMessages
- SmtpPermanentRejectedMessages
- MailboxStoredMessages (nullable; only when verified)
- BytesPrepared (MIME bytes prepared for SMTP)
- BytesAcceptedBySmtp (nullable/estimated only if the protocol path exposes exact DATA bytes; clearly label if estimated)
- MailboxUsedBytes / MailboxQuotaBytes (nullable; only server-reported)
- InboxMessageCount, JunkMessageCount (nullable; only queried folders)

Never sum counters with different meanings into one Delivered value.

## Live graph
Add a bounded time-series chart (ring buffer, e.g. last 10–30 minutes) with:
1. SMTP accepted messages/minute.
2. SMTP transient responses and permanent rejections/minute (separate series).
3. Verified mailbox stored messages/minute (only with IMAP; otherwise series absent).
4. Verified mailbox used bytes / quota percentage (only with IMAP quota; otherwise absent).
5. Current adaptive sending rate and concurrency as a secondary diagnostic series or separate small plot.

The chart must show gaps/Not verified rather than zero when IMAP is unavailable. Use separate axes for message rate and bytes/quota. Retain exact numeric labels and a legend; do not smooth away spikes or rejection bursts.

## IMAP scope
- Extend read-only monitoring to discover/list folders and identify likely Inbox/Junk/Spam folders without assuming localized folder names.
- Query message counts and supported folder sizes per folder.
- Query STORAGE and MESSAGE quota resources when supported.
- Do not download message bodies or attachments by default.
- Clearly distinguish per-folder sizes from account-wide quota.

## Large attachments
- MIME Base64 encoding increases binary attachment size by approximately one third, before headers and boundaries.
- A 1 GiB attachment is therefore roughly 1.37 GiB of encoded payload and will exceed many providers' per-message limits.
- Enforce the server-advertised SIZE limit where available; otherwise use a conservative operator-configured ceiling.
- Do not automatically send 1 GiB probes. Use bounded test payloads and explicit confirmation for unusually large messages.
- A 20 GiB mailbox quota does not imply that a single 1 GiB message is accepted.

## Acceptance tests
- SMTP-only run works with all IMAP fields blank.
- SMTP 250 increments SMTP accepted only; mailbox stored remains unknown without IMAP evidence.
- Server with no SIZE extension reports per-message maximum unknown.
- Server with SIZE extension reports that value as a per-message maximum, never as mailbox quota.
- 4xx causes bounded backoff and visible transient-series activity.
- 5xx permanent policy failure is not retried indefinitely.
- IMAP quota/folder samples update mailbox series; unavailable quota produces gaps, not zero.
- Capacity threshold auto-stop uses only server-reported quota.
- Large attachment size calculations include Base64 overhead and do not overflow integer arithmetic.
- Chart remains bounded in memory and UI telemetry exceptions cannot fail SMTP sending.

## Delivery plan
1. Add discovery result models and tests.
2. Add read-only SMTP capability discovery and tests.
3. Extend IMAP folder/quota snapshots and tests.
4. Add a bounded time-series model and WinForms chart control.
5. Integrate adaptive policy using explicit SMTP response classification and hard caps.
6. Add documentation, UI safety labels, and CI verification.