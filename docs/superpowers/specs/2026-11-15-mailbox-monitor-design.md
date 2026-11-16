# Mailbox Monitor — design

Date: 2026-11-15

## Intent

Add an optional, read-only IMAP monitor to MailLoadTester so an operator can inspect a mailbox while a separately configured SMTP test runs. The monitor must never be a prerequisite for SMTP sending: when IMAP is not configured or unavailable, the existing SMTP path remains unchanged and mailbox state is reported as unknown.

## Goals

- Provide a dedicated Mailbox Monitor tab with the agreed visual summary: percentage bar, stored message count, measured size, quota, and monitoring state.
- Allow manual refresh and optional periodic polling while an SMTP run is active.
- Support secure IMAP connection settings (host, port, TLS mode, username, password).
- Read server-reported quota when the IMAP server exposes RFC 2087 quota support.
- Show the INBOX message count and INBOX size separately when the server provides STATUS SIZE; do not mislabel INBOX size as total account usage.
- Allow automatic cancellation of the active SMTP run when a real server-reported STORAGE quota reaches the configured threshold.
- Keep the monitor optional and isolated from SMTP engine behavior.

## Non-goals

- Do not add or change SMTP sending, recipient discovery, message generation, retry, proxy, IP rotation, or delivery behavior.
- Do not attempt to bypass spam filters, reputation systems, rate limits, or provider policies. No tool can guarantee inbox placement.
- Do not delete, move, mark, or modify mailbox messages or folders.
- Do not infer mailbox quota or percentage from SMTP 250 responses.
- Do not claim an exact account-wide percentage when the server exposes only INBOX size or no quota.

## UX and behavior

- New tab: Mailbox Monitor.
- Connection inputs: IMAP host, port (default 993), TLS mode (implicit TLS default; STARTTLS selectable), username and password.
- Actions: Test / Refresh and Clear credentials.
- Summary: percentage and progress bar only when a server STORAGE quota with a positive limit is returned; stored messages and storage usage from quota; INBOX message count and size as separately labelled fallback information; last refresh time and clear error/status text.
- Optional checkbox: Stop SMTP test at quota threshold; threshold defaults to 80% and is configurable from 1–100%.
- Auto-stop is active only when the user explicitly enables it and a real STORAGE quota is available. If quota is unsupported or unavailable, do not silently substitute an estimate; display that auto-stop is unavailable.
- Poll every 30 seconds while SMTP is running. Manual refresh is available independently.
- IMAP credentials remain in memory only; never include them in profiles, logs, telemetry, or JSON reports.
- TLS certificate validation remains enabled. Do not add an ignore-certificate option.

## Technical approach

Use the existing MailKit 4.17.0 dependency. Add a Core service that connects with ImapClient, authenticates, queries INBOX status and (when supported) quota, and returns a credential-free immutable snapshot. The UI owns polling and cancellation; it invokes the service independently of SmtpTestRunner. Keep all IMAP operations cancellation-aware and dispose/disconnect the client on every path.

Quota storage units must be converted explicitly (MailKit 4.x quota storage values are in KiB). A percentage is valid only when StorageLimit > 0. INBOX Size is not account-wide usage and must be labelled accordingly.

## Acceptance criteria

1. SMTP tests can still be configured and run without any IMAP values.
2. The monitor can connect and refresh a mailbox over implicit TLS and STARTTLS.
3. A supported STORAGE quota displays usage, limit, and percentage.
4. Unsupported/unavailable quota is displayed as unavailable; no fabricated percentage is shown.
5. INBOX count/size are visibly distinguished from account-wide quota.
6. Enabling the threshold stop cancels the current SMTP run only after a real quota percentage reaches the configured threshold.
7. IMAP errors never crash or mark the SMTP run as failed; they are shown in monitor status.
8. IMAP passwords are not persisted or logged.
9. Unit tests cover quota conversion, percentage boundaries, missing/zero quota, and stop-threshold behavior.
10. No outbound email or SMTP engine behavior is changed.