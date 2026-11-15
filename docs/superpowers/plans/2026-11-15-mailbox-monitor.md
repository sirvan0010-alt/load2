# Mailbox Monitor Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking.

**Goal:** Add an optional read-only IMAP mailbox monitor with quota visualization and safe SMTP-run cancellation at a configured quota threshold.

**Architecture:** A small Core service connects to IMAP using the existing MailKit dependency and returns a credential-free snapshot. A new WinForms tab displays quota and INBOX metrics, performs manual refresh, and polls during SMTP runs; it cancels the existing run only when a server-reported STORAGE quota reaches the threshold.

**Tech Stack:** C# 12, .NET 8, WinForms, MailKit 4.17.0, xUnit.

**Spec:** docs/superpowers/specs/2026-11-15-mailbox-monitor-design.md

## Global Constraints

- Do not change SMTP sending, recipient handling, retries, proxy, IP rotation, or message generation.
- IMAP monitoring is optional; missing credentials must never block SMTP configuration or execution.
- IMAP is read-only; do not mutate mailbox messages or folders.
- Do not log, persist, export, or report IMAP credentials.
- Keep TLS certificate validation enabled.
- Show account-wide percentage only when a real STORAGE quota and positive limit are available.
- Label folder size/count as INBOX metrics, never as total account usage.
- Auto-stop only when explicitly enabled and based on a real quota percentage.

## Review Focus

- IMAP host resolves to multiple addresses or connection is interrupted: report a monitor error without affecting SMTP.
- Server supports quota but omits STORAGE or returns a zero limit: show quota unavailable and never divide by zero.
- INBOX STATUS SIZE is unsupported: preserve message count if available and show size as unavailable rather than guessing.
- Cancellation occurs during an IMAP command: dispose the connection and do not mark the SMTP send itself as failed.
- The monitor is refreshed while the form is closing or the SMTP run is stopping: avoid cross-thread UI updates and disposed-control access.

---

### Task 1: Quota model and pure calculations

**Files:**
- Create: src/MailLoadTester.Core/MailboxMonitorModels.cs
- Test: tests/MailLoadTester.Tests/MailboxMonitorTests.cs

**Interfaces:**
- Produces: MailboxMonitorOptions, MailboxMonitorSnapshot, MailboxMonitorMath.GetUsagePercent(FolderQuota), and MailboxMonitorMath.ShouldStop(snapshot, thresholdPercent).

- [ ] Write failing tests for KiB-to-byte conversion, 0/100/over-100 percentage handling, missing quota, zero quota limit, and threshold comparison.
- [ ] Run the focused test and confirm it fails because the model/calculator does not exist.
- [ ] Implement immutable option/snapshot records and pure quota calculations. Use MailKit FolderQuota.StorageLimit and CurrentStorageSize, both in KiB.
- [ ] Run focused tests and confirm they pass.
- [ ] Commit the model and tests.

### Task 2: Read-only IMAP service

**Files:**
- Create: src/MailLoadTester.Core/MailboxMonitorService.cs
- Modify: tests/MailLoadTester.Tests/MailboxMonitorTests.cs

**Interfaces:**
- Consumes: MailboxMonitorOptions and MailboxMonitorSnapshot from Task 1.
- Produces: Task<MailboxMonitorSnapshot> MailboxMonitorService.ReadAsync(MailboxMonitorOptions options, CancellationToken cancellationToken = default).

- [ ] Add tests for validation of empty host/username, invalid port, and unsupported TLS selection without requiring an external IMAP server.
- [ ] Run focused tests and confirm they fail before implementation.
- [ ] Implement MailKit ImapClient connect/authenticate/read-only INBOX STATUS, quota retrieval when SupportsQuotas is true, explicit KiB conversion, and cancellation-aware cleanup.
- [ ] Treat absent quota as unavailable; capture INBOX Count and Size only as folder-specific metrics.
- [ ] Run focused tests and confirm they pass.
- [ ] Commit service and tests.

### Task 3: WinForms Mailbox Monitor tab and run integration

**Files:**
- Modify: src/MailLoadTester.Gui/MainForm.cs
- Modify: src/MailLoadTester.Gui/MainForm.Layout.cs
- Create: src/MailLoadTester.Gui/MainForm.MailboxMonitor.cs

**Interfaces:**
- Consumes: MailboxMonitorService.ReadAsync, MailboxMonitorSnapshot, and MailboxMonitorMath.ShouldStop.
- Produces: dedicated UI controls, manual refresh, 30-second polling during SMTP runs, and safe cancellation through the existing _cts.

- [ ] Add monitor controls and default values; ensure password is not wired into profile persistence.
- [ ] Add refresh/test handler with independent cancellation and status/error reporting.
- [ ] Add polling lifecycle tied to SMTP run start/finish and form closing.
- [ ] Add quota progress bar, message count, measured usage, quota, last refresh, and an explicit unavailable state.
- [ ] Cancel the current SMTP run only when the opt-in threshold is reached using real quota data.
- [ ] Run Windows GUI build and the full test suite in CI; inspect all results.
- [ ] Commit UI integration.

### Task 4: Documentation and final verification

**Files:**
- Create: docs/MAILBOX-MONITOR.md

**Interfaces:**
- Documents the user-visible configuration, quota limitations, SMTP 250 distinction, and credential handling.

- [ ] Document setup and the distinction between account quota and INBOX-only metrics.
- [ ] Verify no IMAP secrets are included in profiles, logs, or reports.
- [ ] Run the full CI workflow and inspect build, tests, CodeQL, dependency review, and architecture checks.
- [ ] Create/update the pull request description with scope, limitations, and verification evidence.