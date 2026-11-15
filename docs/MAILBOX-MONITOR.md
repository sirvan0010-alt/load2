# Mailbox Monitor

Mailbox Monitor is an optional, read-only IMAP view. It can observe a mailbox while the existing SMTP test runs, but it does not send, delete, move, or modify messages.

## Setup

1. Open the Mailbox Monitor tab.
2. Enter the IMAP host, port, security mode, username, and password or app password.
3. Use Test / Refresh to verify the connection and load the available metrics.
4. If the server exposes a STORAGE quota, the monitor shows used storage, quota limit, and percentage.
5. Optionally enable Stop SMTP test at quota threshold and choose a percentage such as 80%. The active SMTP run is cancelled only after a real IMAP STORAGE quota reaches that threshold.

Typical IMAP settings are port 993 with implicit TLS or port 143 with STARTTLS. Use the exact settings required by the mailbox provider. Certificate validation remains enabled.

## Metrics and limitations

- Account-wide usage percentage is displayed only when the server returns a STORAGE quota with a positive limit through the IMAP QUOTA extension.
- The STORAGE usage and limit values returned by MailKit 4.x are in KiB and are converted to bytes for display.
- INBOX message count, unread count, and STATUS SIZE are folder-specific. INBOX size is not a substitute for account-wide quota usage.
- If quota is unsupported, missing, denied, or has a zero limit, the monitor shows quota as unavailable. It does not invent a percentage.
- Some providers expose account-wide quota differently or do not expose it through IMAP. Provider-specific APIs would be required for those cases.
- The monitor refreshes every 30 seconds while an SMTP run is active when IMAP credentials are configured. Manual refresh is also available.
- IMAP errors are shown in the monitor and do not by themselves mark the SMTP test as failed.

## SMTP acceptance is not mailbox delivery

An SMTP response of 250 OK means that the receiving SMTP server accepted the message for further processing. It does not confirm that the message was stored in the intended mailbox. The message may later be rejected, filtered as spam, routed elsewhere, or delayed. IMAP observation is an independent check of what is visible in the mailbox.

Mailbox Monitor does not bypass spam filtering, sender reputation, provider rate limits, or recipient policies. SPF, DKIM, DMARC, correct TLS, authenticated submission, reverse DNS, and a permitted sending rate can improve legitimate deliverability, but no sender-side option guarantees inbox placement.

## Privacy and safety

- IMAP credentials are held in memory for the current application session only.
- They are not added to profiles, SMTP logs, session logs, telemetry, or JSON run reports.
- The monitor uses read-only status and quota operations. It does not fetch message bodies.
- Use the monitor only with a mailbox you own or are explicitly authorized to inspect.
- Leaving IMAP fields blank does not prevent the existing SMTP test from running; it only means that actual mailbox storage cannot be verified.