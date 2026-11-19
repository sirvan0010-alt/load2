# Baseline audit – 2026-11-18

Branch: `feature/google-oauth-gmail-api`
Audited commit after roadmap: `7188f7ec8bbb6565578f2eeed6fc7bdfe334c404`
Pull request: #44 (draft)

## Confirmed UI findings

1. DPI support is already enabled:
   - `Program.Main` calls `Application.SetHighDpiMode(HighDpiMode.PerMonitorV2)`.
   - `MainForm` uses `AutoScaleMode.Dpi`.
   These settings should be preserved, not reimplemented.

2. A responsive-layout attempt already exists in `MainForm.Responsive.cs`, but it is limited:
   - It only reflows the SMTP layout referenced by `_smtpLayout`.
   - The breakpoint is a fixed client width of 1120 px.
   - Compact mode uses absolute row heights of 470, 620 and 250 px and sets the table height to 1340 px.
   - `ResizeFlowContent` only resizes selected controls inside top-down FlowLayoutPanels; it does not provide a general responsive layout system for all tabs.
   - It changes child widths at runtime, while many controls are initially created with fixed widths and labels with fixed MaximumSize values.

3. The SMTP tab still uses three equal percentage columns and several nested FlowLayoutPanels with `WrapContents = false` and `AutoScroll = true`. Labels include fixed MaximumSize values (for example 420, 520 and 340 px), and action buttons use fixed widths. This is consistent with the reported clipping/empty-space behavior at different window sizes.

4. MainForm has a 1000 x 750 minimum window size. This may be unnecessarily restrictive for smaller displays and must be evaluated alongside layout reflow, not reduced blindly.

5. The top command bar uses fixed Left coordinates for buttons and checkboxes. This is another likely source of clipping on narrow windows and should be converted to a layout container during the UI phase.

## Confirmed SMTP credential persistence findings

- `SmtpCredentialStore` exists and uses Windows DPAPI `CurrentUser`.
- The serialized cleartext buffer and protected byte buffer are zeroed after save; loaded buffers are zeroed after deserialization.
- `MainForm.SmtpCredentials.cs` restores host, port, username, password, TLS and auth method and sets the remember checkbox.
- Saving is opt-in and skips incomplete/unauthenticated credentials.
- The user guide states that save is triggered after successful connection/test/send paths. The exact event wiring and failure paths still need a targeted review before declaring this phase complete.

## Confirmed SMTP transport findings

- `SmtpConnectionPool` rejects simultaneous proxy configuration and source-IP binding/rotation, with an explicit fail-closed exception to prevent a custom socket path bypassing the proxy.
- `ProxyClientFactory` creates MailKit HTTP/SOCKS4/SOCKS5 proxy clients.
- This is a useful base for Route Check, but it does not alone prove the externally observed egress IP or cover auxiliary HTTP/DNS/IMAP/Gmail API operations. Each path must be audited separately.

## Not yet verified

- Full responsive behavior of every tab at multiple window widths and DPI scales.
- Actual build/test result for commit `7188f7e` (workflow run was in progress at audit time).
- Exact MIME construction location and all header mutation points.
- Exact save-after-success event wiring for SMTP credentials.
- All non-SMTP network operations and whether they honor the configured proxy.

## Authentication freeze

No authentication implementation files were changed by this audit. Future UI work must not modify Google OAuth or the current placement/visibility of Google OAuth versus classic SMTP sign-in.
