# Development guide

## Project layout

| Path | Responsibility |
|---|---|
| `src/MailLoadTester.Core` | SMTP execution, validation, connection pools, pacing, retry, health, observability and reporting |
| `src/MailLoadTester.Gui` | Windows Forms UI; partial files divide layout, execution, progress, profiles, accounts, theme and reporting |
| `tests/MailLoadTester.Tests` | Unit and loopback SMTP integration tests |
| `docs` | Current design notes, security invariants and historical audit records |

The main execution path is:

`MainForm` → immutable `MailTestOptions` snapshot → `SmtpTestRunner` → bounded work channel → MIME construction → `SmtpConnectionPool` / MailKit → `MailTestResult` → `RunObservability` / `RunReportBuilder`.

Key Core components:

- `SmtpTestRunner`: orchestration, bounded workers, retry and delivery ledger.
- `SmartPaceController`: SEND-start pacing, warm-up, jitter and backoff.
- `SmtpConnectionPool`: reusable SMTP connections and bounded pool access.
- `DeliveryLedger`: prevents already accepted logical messages from being resent after restart.
- `TransportHealthRegistry`: endpoint health and quarantine state.
- `ScenarioQueueMetrics`, `RetryMetrics`, `SmtpOutcomeCounters`: bounded operational counters.
- `LatencyHistogram`, `RunObservability`, `RunReportBuilder`: final metrics, summary projection and secret-scrubbed JSON export.

## Local validation

Requires .NET 8 SDK. From the repository root:

```bash
dotnet restore src/MailLoadTester.Core/MailLoadTester.Core.csproj
dotnet build src/MailLoadTester.Core/MailLoadTester.Core.csproj -c Release --no-restore
dotnet test tests/MailLoadTester.Tests/MailLoadTester.Tests.csproj -c Release
```

On Windows, also build the GUI:

```powershell
dotnet restore src/MailLoadTester.Gui/MailLoadTester.Gui.csproj
dotnet build src/MailLoadTester.Gui/MailLoadTester.Gui.csproj -c Release --no-restore
```

The GitHub Actions workflow runs Core/tests on Ubuntu and the WinForms GUI build on Windows. The test suite uses a loopback SMTP server; it does not need real recipients or public SMTP services.

## Change discipline

1. Read the relevant current design note and tests before changing behavior.
2. Keep Core logic independent from WinForms.
3. Preserve backward compatibility of optional `MailTestOptions` and `MailTestResult` parameters where practical.
4. Add regression tests for each discovered bug; prefer loopback integration tests for SMTP behavior.
5. Keep live sending behind `AuthorizationGate`. Do not use public providers for load tests without explicit authorization.
6. Do not log SMTP credentials, OAuth tokens, proxy passwords, certificate passwords or AUTH payloads.
7. Update documentation that describes the affected behavior; do not add another audit snapshot instead.
8. Keep work on a feature branch and open a draft PR. Do not commit directly to `main`.

## GUI source organization

`MainForm.cs` owns controls, runtime state, construction and close/cancellation lifecycle. Functionality is split into partial files:

- `MainForm.Layout.cs`: dynamic WinForms layout and event wiring.
- `MainForm.Execution.cs`: option snapshot, run start/stop and connection test.
- `MainForm.Progress.cs`: progress, delivery path, provider preset and log handling.
- `MainForm.Profiles.cs`: profile load/save.
- `MainForm.Accounts.cs`, `MainForm.Theme.cs`, `MainForm.Reporting.cs`: focused UI capabilities.

Keep new UI features in the appropriate partial file rather than growing the root class again.
