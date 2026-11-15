# load2 — MailLoadTester

Authorized **SMTP / email load-testing** tool. The current release version is maintained in [`VERSION`](VERSION).

## For users

- [Build, publish and installer guide](README-BUILD.md)
- [Documentation index](docs/README.md)

## For developers

- [Architecture and development guide](docs/DEVELOPMENT.md)
- [Maintenance contract](README-MAINTENANCE.md)
- [AI-assisted development guide](docs/AI-GUIDE.md)

## Project

- Core: `src/MailLoadTester.Core`
- Windows Forms GUI: `src/MailLoadTester.Gui`
- Tests: `tests/MailLoadTester.Tests`

Build and test with the .NET 8 SDK. The GitHub Actions workflow builds Core/tests on Ubuntu and the WinForms GUI on Windows.

**Source-of-truth rule:** current source code and tests define actual behavior. Historical audit files explain past changes and may not describe the current implementation.
